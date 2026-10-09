using Xunit;

namespace FocusOn.Tests;

public sealed class RecordingSessionTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Fact]
    public void Short_foreground_switch_is_saved_and_reopened_with_daily_totals()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "main.cs"), At(0));
            session.Observe(new(2, "wechat", "微信", "微信"), At(10));
            session.Observe(new(1, "editor", "编辑器", "main.cs"), At(12));
            session.Finish(At(22));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc);

        Assert.Collection(day.Intervals,
            interval => AssertUsage(interval, "editor", 0, 10),
            interval => AssertUsage(interval, "wechat", 10, 12),
            interval => AssertUsage(interval, "editor", 12, 22));
        Assert.Collection(day.Totals,
            total => { Assert.Equal("editor", total.AppKey); Assert.Equal(TimeSpan.FromSeconds(20), total.Duration); },
            total => { Assert.Equal("wechat", total.AppKey); Assert.Equal(TimeSpan.FromSeconds(2), total.Duration); });
        Assert.Equal(3, day.Intervals.Select(interval => interval.Id).Distinct().Count());
    }

    [Fact]
    public void Window_and_title_changes_split_usage_but_repeated_observations_do_not()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "文档 A"), At(0));
        session.Observe(new(2, "editor", "编辑器", "文档 A"), At(5));
        session.Observe(new(2, "editor", "编辑器", "文档 B"), At(8));
        session.Observe(new(2, "editor", "编辑器", "文档 B"), At(9));
        session.Finish(At(12));

        var day = session.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval => { AssertUsage(interval, "editor", 0, 5); Assert.Equal("文档 A", interval.WindowTitle); },
            interval => { AssertUsage(interval, "editor", 5, 8); Assert.Equal("文档 A", interval.WindowTitle); },
            interval => { AssertUsage(interval, "editor", 8, 12); Assert.Equal("文档 B", interval.WindowTitle); });
        Assert.Equal(TimeSpan.FromSeconds(12), Assert.Single(day.Totals).Duration);
    }

    [Fact]
    public void Reading_without_input_keeps_one_twenty_minute_segment_and_live_tail_is_not_a_save()
    {
        using var session = new RecordingSession(DatabasePath);
        var window = new ForegroundObservation(1, "reader", "阅读器", "文章");
        session.Observe(window, At(0));
        session.Checkpoint(At(300));
        session.Observe(window, At(600));
        session.Checkpoint(At(600));

        var day = session.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc, At(1200));
        var interval = Assert.Single(day.Intervals);
        Assert.Equal(TimeSpan.FromMinutes(20), interval.Duration);
        Assert.True(interval.IsCurrent);
        Assert.Equal(TimeSpan.FromMinutes(20), Assert.Single(day.Totals).Duration);

        using (var reopened = new RecordingSession(DatabasePath))
        {
            var saved = Assert.Single(reopened.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc).Intervals);
            Assert.Equal(interval.Id, saved.Id);
            Assert.Equal(TimeSpan.FromMinutes(10), saved.Duration);
        }

        session.Finish(At(1200));
        Assert.Equal(TimeSpan.FromMinutes(20), Assert.Single(session.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc).Intervals).Duration);
    }

    [Fact]
    public void Unconfirmed_foreground_is_unrecorded_but_missing_identity_and_title_keep_usage()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(null, At(0));
        var unavailable = session.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc, At(5));
        Assert.Equal("未记录", unavailable.Status);
        Assert.Equal(IntervalKind.Unrecorded, Assert.Single(unavailable.Intervals).Kind);
        Assert.Empty(unavailable.Totals);

        session.Observe(new(1, null, "无权验证的名称", null), At(10));
        session.Observe(new(2, "browser", "浏览器", "  "), At(20));
        session.Finish(At(30));

        var day = session.GetDay(DateOnly.FromDateTime(Start.Date), TimeZoneInfo.Utc);
        Assert.Collection(day.Intervals,
            interval =>
            {
                Assert.Equal(IntervalKind.Unrecorded, interval.Kind);
                Assert.Equal(TimeSpan.FromSeconds(10), interval.Duration);
                Assert.Null(interval.AppKey);
                Assert.Null(interval.AppName);
                Assert.Null(interval.WindowTitle);
            },
            interval =>
            {
                Assert.Equal(IntervalKind.Usage, interval.Kind);
                Assert.Equal("未知软件", interval.AppName);
                Assert.Equal("标题不可用", interval.WindowTitle);
                Assert.Equal(TimeSpan.FromSeconds(10), interval.Duration);
            },
            interval =>
            {
                AssertUsage(interval, "browser", 20, 30);
                Assert.Equal("浏览器", interval.AppName);
                Assert.Equal("标题不可用", interval.WindowTitle);
            });
        Assert.Equal(2, day.Totals.Count);
        Assert.All(day.Totals, total => Assert.Equal(TimeSpan.FromSeconds(10), total.Duration));
    }

    private static RecordingTime At(double seconds) => new(Start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");

    private static void AssertUsage(RecordedInterval interval, string appKey, int startSeconds, int endSeconds)
    {
        Assert.Equal(IntervalKind.Usage, interval.Kind);
        Assert.Equal(appKey, interval.AppKey);
        Assert.Equal(Start.AddSeconds(startSeconds), interval.StartUtc);
        Assert.Equal(Start.AddSeconds(endSeconds), interval.EndUtc);
        Assert.Equal(TimeSpan.FromSeconds(endSeconds - startSeconds), interval.Duration);
        Assert.False(interval.IsCurrent);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
