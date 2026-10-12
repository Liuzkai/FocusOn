using System.Xml.Linq;
using Xunit;

namespace FocusOn.Tests;

public sealed class PlatformTests
{
    [Fact]
    public void Data_paths_keep_Windows_data_and_use_the_native_user_locations_on_Unix()
    {
        var user = Path.Combine(Path.GetTempPath(), "alice");
        var xdg = Path.Combine(Path.GetTempPath(), "xdg-data");
        Assert.Equal(Path.Combine("local", "FocusOn"), PlatformPaths.GetDataDirectory("windows", user, "local", null));
        Assert.Equal(Path.Combine(user, "Library", "Application Support", "FocusOn"), PlatformPaths.GetDataDirectory("macos", user, "local", null));
        Assert.Equal(Path.Combine(xdg, "FocusOn"), PlatformPaths.GetDataDirectory("linux", user, "local", xdg));
        Assert.Equal(Path.Combine(user, ".local", "share", "FocusOn"), PlatformPaths.GetDataDirectory("linux", user, "local", "relative"));
    }

    [Fact]
    public void Isolated_hidden_startup_does_not_use_the_real_user_data_directory()
    {
        var (path, isolated, startup) = PlatformPaths.ParseArguments(["--startup", "--data-dir", "qa"]);
        Assert.Equal(Path.GetFullPath("qa"), path); Assert.True(isolated); Assert.True(startup);
        Assert.Throws<ArgumentException>(() => PlatformPaths.ParseArguments(["--data-dir"]));
        Assert.Throws<ArgumentException>(() => PlatformPaths.ParseArguments(["--data-dir", ""]));
    }

    [Fact]
    public void Mac_login_agent_keeps_special_characters_in_a_single_executable_argument()
    {
        var path = "/Applications/Focus & On.app/Contents/MacOS/FocusOn";
        var xml = XDocument.Parse(StartupRegistration.MacLaunchAgent(path));
        Assert.Equal(new[] { path, "--startup" }, xml.Descendants("array").Single().Elements("string").Select(x => x.Value));
        Assert.DoesNotContain("sh", xml.Descendants("array").Single().Value);
    }

    [Fact]
    public void Linux_autostart_does_not_expand_shell_characters_or_desktop_field_codes()
    {
        var text = StartupRegistration.LinuxDesktopEntry("/apps/Focus $On 100%/FocusOn");
        Assert.Contains("Exec=\"/apps/Focus \\\\$On 100%%/FocusOn\" --startup", text);
        Assert.Contains("Terminal=false", text);
        Assert.Throws<ArgumentException>(() => StartupRegistration.LinuxDesktopEntry("relative/FocusOn"));
        Assert.Throws<ArgumentException>(() => StartupRegistration.LinuxDesktopEntry("/apps/FocusOn\nExec=other"));
    }

    [Fact]
    public void Wayland_bridge_reports_the_actual_window_and_hides_metadata_while_locked()
    {
        const string window = "\"window\":{\"id\":22,\"pid\":100,\"key\":\"desktop:editor.desktop\",\"name\":\"编辑器\",\"title\":\"文档\"}";
        var ready = GnomeBridge.Parse("{" + window + ",\"locked\":false,\"displayOff\":false}");
        Assert.Equal("文档", ready.Observation!.WindowTitle); Assert.True(ready.State.Available);
        var locked = GnomeBridge.Parse("{" + window + ",\"locked\":true,\"displayOff\":true}");
        Assert.Null(locked.Observation); Assert.True(locked.State.Locked); Assert.True(locked.State.DisplayOff);
    }

    [Fact]
    public void Sampling_gap_does_not_credit_suspended_time_to_the_last_app_or_title_timer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-gap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "focuson.db");
        var utc = new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);
        RecordingTime At(int seconds) => new(utc.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");
        try
        {
            using (var session = new RecordingSession(database))
            {
                session.Observe(new(1, "editor", "Editor", "document"), At(0));
                session.ChangeTimer(TimerAction.Start, "reading", At(0));
                session.Checkpoint(At(5));
                session.Interrupt(At(60), "采样中断");
                session.Observe(new(2, "browser", "Browser", "page"), At(61));
                session.Checkpoint(At(66));
                var view = session.GetDay(new(2026, 10, 11), TimeZoneInfo.Utc, At(66));
                Assert.Equal(TimeSpan.FromSeconds(10), TimeSpan.FromTicks(view.Totals.Sum(x => x.Duration.Ticks)));
                Assert.Equal(TimeSpan.FromSeconds(5), view.Timer!.Duration); Assert.False(view.Timer.IsRunning);
                Assert.Equal(TimeSpan.FromSeconds(56), Assert.Single(view.Intervals, x => x.Kind == IntervalKind.Unrecorded).Duration);
            }
            using var reopened = new RecordingSession(database);
            Assert.Equal(TimeSpan.FromSeconds(5), reopened.GetDay(new(2026, 10, 11), TimeZoneInfo.Utc).Timer!.Duration);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Unix_data_directories_with_distinct_case_can_have_separate_recorders()
    {
        if (OperatingSystem.IsWindows()) return; // Windows deliberately normalizes case.
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-case-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var first = new SingleInstance(Path.Combine(directory, "data"));
            var caseSensitive = !Directory.Exists(Path.Combine(directory, "DATA"));
            using var second = new SingleInstance(Path.Combine(directory, "DATA"));
            Assert.True(first.IsPrimary); Assert.Equal(caseSensitive, second.IsPrimary);
        }
        finally { Directory.Delete(directory, true); }
    }
}
