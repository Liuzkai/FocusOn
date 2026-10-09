using Xunit;

namespace FocusOn.Tests;

public sealed class StopConditionsTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = DateOnly.FromDateTime(Start.Date);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Fact]
    public void Unlock_cannot_clear_manual_pause_and_resume_uses_the_fresh_foreground()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            var editor = new ForegroundObservation(1, "editor", "编辑器", "文档 A");
            session.Observe(editor, At(0));
            session.SetConditions(new(true, false, false, false), editor, At(10));
            session.SetConditions(new(true, false, true, false), editor, At(20));
            session.SetConditions(new(true, false, false, false), new(2, "browser", "浏览器", "旧网页"), At(30));
            session.Observe(new(2, "browser", "浏览器", "暂停期间变化"), At(40));
            session.SetConditions(new(false, false, false, false), new(3, "browser", "浏览器", "恢复时的实际网页"), At(50));
            session.Finish(At(60));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, IntervalKind.Usage, 0, 10, "editor"),
            interval => AssertInterval(interval, IntervalKind.ManualPause, 10, 50),
            interval =>
            {
                AssertInterval(interval, IntervalKind.Usage, 50, 60, "browser");
                Assert.Equal("恢复时的实际网页", interval.WindowTitle);
            });
        Assert.Equal(2, day.Totals.Count);
        Assert.All(day.Totals, total => Assert.Equal(TimeSpan.FromSeconds(10), total.Duration));
    }

    [Fact]
    public void Waking_while_locked_cannot_resume_software_time()
    {
        using var session = new RecordingSession(DatabasePath);
        var editor = new ForegroundObservation(1, "editor", "编辑器", "文档 A");
        session.Observe(editor, At(0));
        session.SetConditions(new(false, true, true, false), editor, At(10));
        Assert.Equal("睡眠", session.GetDay(Date, TimeZoneInfo.Utc, At(15)).Status);
        session.SetConditions(new(false, false, true, false), new(2, "browser", "浏览器", "锁屏中"), At(20));
        Assert.Equal("锁屏", session.GetDay(Date, TimeZoneInfo.Utc, At(25)).Status);
        session.Observe(new(2, "browser", "浏览器", "锁屏中标题变化"), At(30));
        session.SetConditions(new(false, false, false, false), new(3, "browser", "浏览器", "已解锁"), At(40));
        session.Finish(At(50));

        var day = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, IntervalKind.Usage, 0, 10, "editor"),
            interval => AssertInterval(interval, IntervalKind.Sleep, 10, 20),
            interval => AssertInterval(interval, IntervalKind.Locked, 20, 40),
            interval => AssertInterval(interval, IntervalKind.Usage, 40, 50, "browser"));
        Assert.All(day.Totals, total => Assert.Equal(TimeSpan.FromSeconds(10), total.Duration));
    }

    [Fact]
    public void Overlapping_stop_reasons_follow_priority_and_unreliable_collection_never_counts_usage()
    {
        using var session = new RecordingSession(DatabasePath);
        var editor = new ForegroundObservation(1, "editor", "编辑器", "文档 A");
        session.SetConditions(new(true, true, true, true), editor, At(0));
        session.Observe(new(2, "browser", "浏览器", "暂停中切换"), At(5));
        session.SetConditions(new(false, true, true, true), editor, At(10));
        session.SetConditions(new(false, false, true, true), editor, At(20));
        session.SetConditions(new(false, false, false, true), editor, At(30));
        Assert.Equal("屏幕已关闭", session.GetDay(Date, TimeZoneInfo.Utc, At(35)).Status);
        session.SetConditions(new(false, false, false, true, Available: false), editor, At(40));
        Assert.Equal("未记录", session.GetDay(Date, TimeZoneInfo.Utc, At(45)).Status);
        session.SetConditions(new(false, false, false, true), editor, At(50));
        session.SetConditions(new(false, false, false, false), null, At(60));
        session.SetConditions(new(false, false, false, false), new(3, "browser", "浏览器", "实际恢复"), At(70));
        session.Finish(At(80));

        var day = session.GetDay(Date, TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => AssertInterval(interval, IntervalKind.ManualPause, 0, 10),
            interval => AssertInterval(interval, IntervalKind.Sleep, 10, 20),
            interval => AssertInterval(interval, IntervalKind.Locked, 20, 30),
            interval => AssertInterval(interval, IntervalKind.DisplayOff, 30, 40),
            interval => AssertInterval(interval, IntervalKind.Unrecorded, 40, 50),
            interval => AssertInterval(interval, IntervalKind.DisplayOff, 50, 60),
            interval => AssertInterval(interval, IntervalKind.Unrecorded, 60, 70),
            interval => AssertInterval(interval, IntervalKind.Usage, 70, 80, "browser"));
        var total = Assert.Single(day.Totals);
        Assert.Equal("browser", total.AppKey);
        Assert.Equal(TimeSpan.FromSeconds(10), total.Duration);
    }

    private static RecordingTime At(int seconds) => new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");

    private static void AssertInterval(RecordedInterval interval, IntervalKind kind, int start, int end, string? appKey = null)
    {
        Assert.Equal(kind, interval.Kind);
        Assert.Equal(Start.AddSeconds(start), interval.StartUtc);
        Assert.Equal(Start.AddSeconds(end), interval.EndUtc);
        Assert.Equal(TimeSpan.FromSeconds(end - start), interval.Duration);
        Assert.Equal(appKey, interval.AppKey);
        if (kind != IntervalKind.Usage)
        {
            Assert.Null(interval.AppName);
            Assert.Null(interval.WindowTitle);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
