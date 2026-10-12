using System.Globalization;
using System.Text.Json;
using Xunit;

namespace FocusOn.Tests;

public sealed class LanguagePreferenceTests
{
    [Fact]
    public void Saved_language_overrides_system_language_after_restart_and_preserves_other_settings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal("en", LanguagePreference.Load(directory, CultureInfo.GetCultureInfo("fr-FR")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"theme\":\"dark\"}");
            LanguagePreference.Save(directory, "zh-CN");
            Assert.Equal("zh-CN", LanguagePreference.Load(directory, CultureInfo.GetCultureInfo("en-US")));
            LanguagePreference.Save(directory, "en");
            Assert.Equal("en", LanguagePreference.Load(directory, CultureInfo.GetCultureInfo("zh-TW")));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "settings.json")));
            Assert.Equal("dark", json.RootElement.GetProperty("theme").GetString());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Invalid_preferences_fall_back_to_the_system_language_without_touching_data()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-settings-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "settings.json"); File.WriteAllText(path, "broken JSON");
            Assert.Equal("zh-CN", LanguagePreference.Load(directory, CultureInfo.GetCultureInfo("zh-CN")));
            Assert.Equal("en", LanguagePreference.Load(directory, CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal("broken JSON", File.ReadAllText(path));
            Assert.Throws<ArgumentException>(() => LanguagePreference.Save(directory, "unsupported"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
