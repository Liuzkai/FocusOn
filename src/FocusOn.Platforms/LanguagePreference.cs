using System.Globalization;
using System.Text.Json;

namespace FocusOn;

public static class LanguagePreference
{
    public static string Load(string directory, CultureInfo? systemCulture = null)
    {
        var fallback = (systemCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en";
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json")));
            return json.RootElement.TryGetProperty("language", out var value) && value.GetString() is "zh-CN" or "en" ? value.GetString()! : fallback;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { return fallback; }
    }

    public static void Save(string directory, string language)
    {
        if (language is not ("zh-CN" or "en")) throw new ArgumentException("Unsupported interface language.", nameof(language));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Preserve future settings owned by other parts of the application.
            var settings = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path)) ?? [] : [];
            settings["language"] = JsonSerializer.SerializeToElement(language);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
