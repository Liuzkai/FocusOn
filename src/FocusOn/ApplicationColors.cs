using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media;

namespace FocusOn;

// Keep assignments independent of sorting, filters, dates and process identities.
public sealed class ApplicationColors
{
    private readonly Dictionary<string, SolidColorBrush> assigned = new(StringComparer.Ordinal);
    private readonly string? path;
    private bool canSave = true;
    private static readonly Color[] Candidates = Enumerable.Range(0, 144)
        .Select(i => HslColor.FromHsl((i % 72) * 5, .65, i < 72 ? .76 : .84).ToRgb()).ToArray();
    private static readonly (double L, double A, double B)[] CandidateLabs = Candidates.Select(Lab).ToArray();

    public ApplicationColors(string? directory = null)
    {
        if (directory is null) return;
        path = Path.Combine(directory, "app-colors.json");
        try
        {
            if (!File.Exists(path)) return;
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? throw new JsonException("Invalid color preferences.");
            foreach (var (key, value) in saved)
            {
                if (value is null || !Color.TryParse(value, out var color) || color.A != 255 || Contrast(color) < 4.5)
                    throw new JsonException("Invalid color preference.");
                assigned.Add(key, new(color));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preserve an unreadable/corrupt file. Cosmetic preferences must not stop recording.
            assigned.Clear(); canSave = false;
            Trace.WriteLine("FocusOn: unable to load app colors; using session assignments.");
        }
    }

    public IBrush Get(string? key)
    {
        key ??= "unknown";
        if (!assigned.ContainsKey(key)) Register([key]);
        return assigned[key];
    }

    public void Register(IEnumerable<string?> keys)
    {
        var changed = false;
        foreach (var key in keys.Select(x => x ?? "unknown").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (assigned.ContainsKey(key)) continue;
            uint hash = 2166136261;
            foreach (var character in key) hash = unchecked((hash ^ character) * 16777619);
            var origin = (int)(hash % Candidates.Length);
            var previous = assigned.Values.Select(x => Lab(x.Color)).ToArray();
            var best = origin; var distance = -1d;
            for (var offset = 0; offset < Candidates.Length; offset++)
            {
                var index = (origin + offset) % Candidates.Length;
                var score = previous.Length == 0 ? 0 : previous.Min(x => Distance(x, CandidateLabs[index]));
                if (score > distance + 1e-12) { distance = score; best = index; }
            }
            assigned.Add(key, new(Candidates[best])); changed = true;
        }
        if (changed) Save();
    }

    private void Save()
    {
        if (path is null || !canSave) return;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(assigned.ToDictionary(x => x.Key, x => x.Value.Color.ToString()),
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Trace.WriteLine("FocusOn: unable to save app colors; keeping session assignments."); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private static double Linear(byte channel)
    {
        var c = channel / 255d;
        return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
    }
    private static double Contrast(Color color)
    {
        var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        return (luminance + .05) / (.2126 * Linear(0x30) + .7152 * Linear(0x30) + .0722 * Linear(0x34) + .05);
    }
    private static (double L, double A, double B) Lab(Color color)
    {
        var r = Linear(color.R); var g = Linear(color.G); var b = Linear(color.B);
        var l = Math.Cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b);
        var m = Math.Cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b);
        var s = Math.Cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
        return (.2104542553 * l + .7936177850 * m - .0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + .4505937099 * s,
            .0259040371 * l + .7827717662 * m - .8086757660 * s);
    }
    private static double Distance((double L, double A, double B) x, (double L, double A, double B) y)
        => Math.Pow(x.L - y.L, 2) + Math.Pow(x.A - y.A, 2) + Math.Pow(x.B - y.B, 2);
}
