using System.Text.Json;

namespace FocusOn;

public sealed record AppPreferences(string Theme = "dark", bool StartAtLogin = true)
{
    public static AppPreferences Load(string directory)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json")));
            var root = json.RootElement;
            var theme = root.TryGetProperty("theme", out var t) && t.GetString() is "light" ? "light" : "dark";
            var startup = !root.TryGetProperty("startAtLogin", out var s) || s.ValueKind != JsonValueKind.False;
            return new(theme, startup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return new(); }
    }
    public void Save(string directory)
    {
        if (Theme is not ("light" or "dark")) throw new ArgumentException("Unsupported theme.");
        SettingsFile.Set(directory, "theme", Theme, "startAtLogin", StartAtLogin);
    }
}

internal static class SettingsFile
{
    public static void Set(string directory, string key, object value, string? otherKey = null, object? otherValue = null)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        var settings = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path)) ?? [] : [];
        settings[key] = JsonSerializer.SerializeToElement(value);
        if (otherKey is not null) settings[otherKey] = JsonSerializer.SerializeToElement(otherValue);
        WriteAtomic(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void WriteAtomic(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, contents); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
