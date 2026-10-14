using Microsoft.Data.Sqlite;
using Xunit;

namespace FocusOn.Tests;

public sealed class SettingsStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "FocusOn-settings-storage-" + Guid.NewGuid().ToString("N"));
    private string Dir(string name) => Path.Combine(root, name);
    [Fact]
    public void Preferences_preserve_language_and_unknown_settings_and_reopen_light_and_disabled_startup()
    {
        Directory.CreateDirectory(Dir("source")); var path = Path.Combine(Dir("source"), "settings.json");
        File.WriteAllText(path, "{\"language\":\"en\",\"custom\":42}");
        new AppPreferences("light", false).Save(Dir("source"));
        Assert.Equal(new AppPreferences("light", false), AppPreferences.Load(Dir("source")));
        Assert.Equal("en", LanguagePreference.Load(Dir("source")));
        LanguagePreference.Save(Dir("source"), "zh-CN");
        Assert.Contains("42", File.ReadAllText(path)); Assert.Equal(new AppPreferences("light", false), AppPreferences.Load(Dir("source")));
        File.WriteAllText(path, "corrupt"); Assert.Equal(new AppPreferences(), AppPreferences.Load(Dir("source")));
        Assert.Throws<System.Text.Json.JsonException>(() => new AppPreferences("light").Save(Dir("source")));
        Assert.Equal("corrupt", File.ReadAllText(path));
    }
    [Fact]
    public void Migration_copies_real_saved_usage_timer_preferences_and_deletion_without_touching_source()
    {
        var start = new DateTimeOffset(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);
        RecordingTime At(int minute) => new(start.AddMinutes(minute), TimeSpan.FromMinutes(minute).Ticks, "UTC");
        using (var session = new RecordingSession(Path.Combine(Dir("source"), "focuson.db")))
        {
            session.Observe(new(1, "editor", "Editor", "Private title"), At(0)); session.ChangeTimer(TimerAction.Start, "User timer", At(0));
            session.Finish(At(90)); session.DeleteDay(new(2026,10,12), TimeZoneInfo.Utc, At(120));
        }
        LanguagePreference.Save(Dir("source"), "en"); new AppPreferences("light", false).Save(Dir("source"));
        File.WriteAllText(Path.Combine(Dir("source"), "app-colors.json"), "colors");
        var original = File.ReadAllBytes(Path.Combine(Dir("source"), "focuson.db"));
        string destination;
        using (var move = new DataDirectoryMigration(Dir("source"), Dir("target")))
        {
            destination = move.Target;
            using var duplicate = new SingleInstance(Dir("target")); Assert.False(duplicate.IsPrimary);
            move.CopyAndCommit(Dir("default"));
        }
        Assert.Equal(destination, DataLocation.Resolve(Dir("default")));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(Dir("source"), "focuson.db")));
        Assert.Equal("en", LanguagePreference.Load(Dir("target"))); Assert.Equal(new AppPreferences("light", false), AppPreferences.Load(Dir("target")));
        Assert.Equal("colors", File.ReadAllText(Path.Combine(Dir("target"), "app-colors.json")));
        using var reopened = new RecordingSession(Path.Combine(Dir("target"), "focuson.db"));
        Assert.Empty(reopened.GetDay(new(2026,10,12), TimeZoneInfo.Utc).Intervals);
        var day = reopened.GetDay(new(2026,10,13), TimeZoneInfo.Utc);
        Assert.Equal(TimeSpan.FromHours(1), Assert.Single(day.Totals).Duration);
        Assert.Contains(day.Intervals, i => i.WindowTitle == "Private title"); Assert.True(day.Timer is { IsRunning: false });
    }
    [Fact]
    public void Migration_reads_the_WAL_and_rejects_nonempty_nested_alias_and_active_destinations()
    {
        Directory.CreateDirectory(Dir("source"));
        using var connection = new SqliteConnection("Data Source=" + Path.Combine(Dir("source"), "focuson.db") + ";Pooling=False"); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE evidence(value TEXT); INSERT INTO evidence VALUES('in-wal');"; command.ExecuteNonQuery();
        using (var move = new DataDirectoryMigration(Dir("source"), Dir("target"))) move.CopyAndCommit(null);
        using var result = new SqliteConnection("Data Source=" + Path.Combine(Dir("target"), "focuson.db") + ";Pooling=False"); result.Open();
        using var read = result.CreateCommand(); read.CommandText = "SELECT value FROM evidence"; Assert.Equal("in-wal", read.ExecuteScalar());
        Assert.Throws<IOException>(() => new DataDirectoryMigration(Dir("source"), Dir("target")));
        Assert.Throws<IOException>(() => new DataDirectoryMigration(Dir("source"), Dir("source/nested")));
        using var busy = new SingleInstance(Dir("busy")); Assert.Throws<IOException>(() => new DataDirectoryMigration(Dir("source"), Dir("busy")));
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(Dir("alias"), Dir("source")); Assert.Throws<IOException>(() => new DataDirectoryMigration(Dir("source"), Dir("alias/nested")));
            Directory.CreateSymbolicLink(Dir("parent-alias"), root);
            Directory.CreateSymbolicLink(Dir("indirect-alias"), Dir("parent-alias/source"));
            Assert.Throws<IOException>(() => new DataDirectoryMigration(Dir("source"), Dir("indirect-alias/nested")));
        }
    }
    [Fact]
    public void Failed_database_copy_does_not_redirect_and_removes_only_its_own_copied_files()
    {
        Directory.CreateDirectory(Dir("source")); File.WriteAllText(Path.Combine(Dir("source"), "focuson.db"), "not SQLite");
        LanguagePreference.Save(Dir("source"), "en");
        using (var move = new DataDirectoryMigration(Dir("source"), Dir("target"))) Assert.Throws<SqliteException>(() => move.CopyAndCommit(Dir("default")));
        Assert.Equal(Dir("default"), DataLocation.Resolve(Dir("default")));
        Assert.Equal("not SQLite", File.ReadAllText(Path.Combine(Dir("source"), "focuson.db")));
        Assert.DoesNotContain(Directory.GetFiles(Dir("target")), p => Path.GetFileName(p) != ".focuson.lock");
        DataLocation.Save(Dir("default"), Dir("source"));
        var isolated = PlatformPaths.ParseArguments(["--data-dir", Dir("isolated")]); Assert.Equal(Dir("isolated"), isolated.DataDirectory);
        Assert.Equal(Dir("source"), DataLocation.Resolve(Dir("default")));
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
