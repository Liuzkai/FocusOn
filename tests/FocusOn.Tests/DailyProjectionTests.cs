using Xunit;

namespace FocusOn.Tests;

public sealed class DailyProjectionTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Test+08", TimeSpan.FromHours(8), "Test+08", "Test+08");
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 23, 59, 0, TimeSpan.FromHours(8));
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Fact]
    public void Saved_segment_crossing_local_midnight_has_one_minute_in_each_daily_log_and_total()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "跨日文档"), At(0));
            session.Finish(At(120));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var firstDay = reopened.GetDay(new DateOnly(2026, 10, 9), Zone);
        var secondDay = reopened.GetDay(new DateOnly(2026, 10, 10), Zone);
        var first = Assert.Single(firstDay.Intervals);
        var second = Assert.Single(secondDay.Intervals);

        Assert.Equal(Start.ToUniversalTime(), first.StartUtc);
        Assert.Equal(Start.AddMinutes(1).ToUniversalTime(), first.EndUtc);
        Assert.Equal(first.EndUtc, second.StartUtc);
        Assert.Equal(Start.AddMinutes(2).ToUniversalTime(), second.EndUtc);
        Assert.Equal(TimeSpan.FromMinutes(1), first.Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), second.Duration);
        Assert.Equal(first.Duration, Assert.Single(firstDay.Totals).Duration);
        Assert.Equal(second.Duration, Assert.Single(secondDay.Totals).Duration);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("跨日文档", second.WindowTitle);
        Assert.False(first.IsCurrent);
        Assert.False(second.IsCurrent);
    }

    [Fact]
    public void Live_segment_is_current_only_in_its_present_day_and_does_not_rewrite_saved_history()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "reader", "阅读器", "跨日阅读"), At(0));
        session.Checkpoint(At(30));

        var previous = session.GetDay(new DateOnly(2026, 10, 9), Zone, At(120));
        var today = session.GetDay(new DateOnly(2026, 10, 10), Zone, At(120));
        Assert.False(Assert.Single(previous.Intervals).IsCurrent);
        Assert.True(Assert.Single(today.Intervals).IsCurrent);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Totals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(today.Totals).Duration);

        using var reopened = new RecordingSession(DatabasePath);
        var saved = Assert.Single(reopened.GetDay(new DateOnly(2026, 10, 9), Zone).Intervals);
        Assert.Equal(TimeSpan.FromSeconds(30), saved.Duration);
        Assert.Equal(Start.AddSeconds(30).ToUniversalTime(), saved.EndUtc);
        Assert.Empty(reopened.GetDay(new DateOnly(2026, 10, 10), Zone).Intervals);
    }

    [Fact]
    public void Exact_midnight_switch_belongs_to_next_day_without_duplicate_usage()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "编辑器", "前一天"), At(0));
        session.Observe(new(2, "browser", "浏览器", "后一天"), At(60));
        session.Finish(At(120));

        var previous = session.GetDay(new DateOnly(2026, 10, 9), Zone);
        var next = session.GetDay(new DateOnly(2026, 10, 10), Zone);
        Assert.Equal("editor", Assert.Single(previous.Intervals).AppKey);
        Assert.Equal("browser", Assert.Single(next.Intervals).AppKey);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Totals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(next.Totals).Duration);
        Assert.Equal(Assert.Single(previous.Intervals).EndUtc, Assert.Single(next.Intervals).StartUtc);
    }

    [Theory]
    [InlineData(IntervalKind.ManualPause)]
    [InlineData(IntervalKind.Sleep)]
    [InlineData(IntervalKind.Locked)]
    [InlineData(IntervalKind.DisplayOff)]
    [InlineData(IntervalKind.Unrecorded)]
    public void Non_usage_intervals_share_day_clipping_without_entering_software_totals(IntervalKind kind)
    {
        using var session = new RecordingSession(DatabasePath);
        session.SetConditions(new(kind == IntervalKind.ManualPause, kind == IntervalKind.Sleep,
            kind == IntervalKind.Locked, kind == IntervalKind.DisplayOff, Available: kind != IntervalKind.Unrecorded),
            new(1, "editor", "编辑器", "不应归属软件"), At(0));
        session.Finish(At(120));

        foreach (var date in new[] { new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 10) })
        {
            var day = session.GetDay(date, Zone);
            var interval = Assert.Single(day.Intervals);
            Assert.Equal(kind, interval.Kind);
            Assert.Equal(TimeSpan.FromMinutes(1), interval.Duration);
            Assert.Empty(day.Totals);
            Assert.Null(interval.AppName);
            Assert.Null(interval.WindowTitle);
        }
    }

    [Fact]
    public void Skipped_local_midnight_uses_first_valid_instant_as_shared_daily_boundary()
    {
        var cuba = TimeZoneInfo.FindSystemTimeZoneById("Cuba Standard Time");
        var start = new DateTimeOffset(2026, 3, 8, 4, 59, 0, TimeSpan.Zero);
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "reader", "阅读器", "正常夏令时跳变"), new(start, 0, cuba.Id));
            session.Finish(new(start.AddMinutes(2), TimeSpan.FromMinutes(2).Ticks, cuba.Id));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var previous = reopened.GetDay(new DateOnly(2026, 3, 7), cuba);
        var next = reopened.GetDay(new DateOnly(2026, 3, 8), cuba);
        var boundary = new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero);
        Assert.Equal(boundary, previous.EndUtc);
        Assert.Equal(previous.EndUtc, next.StartUtc);
        Assert.Equal(TimeSpan.FromHours(23), next.EndUtc - next.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Intervals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(next.Intervals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Totals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(next.Totals).Duration);
    }

    [Fact]
    public void Repeated_local_midnight_belongs_to_next_day_from_its_first_occurrence()
    {
        var cuba = TimeZoneInfo.FindSystemTimeZoneById("Cuba Standard Time");
        var start = new DateTimeOffset(2026, 11, 1, 3, 59, 0, TimeSpan.Zero);
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "reader", "阅读器", "重复午夜"), new(start, 0, cuba.Id));
            session.Finish(new(start.AddMinutes(62), TimeSpan.FromMinutes(62).Ticks, cuba.Id));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var previous = reopened.GetDay(new DateOnly(2026, 10, 31), cuba);
        var next = reopened.GetDay(new DateOnly(2026, 11, 1), cuba);
        var boundary = new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);
        Assert.Equal(boundary, previous.EndUtc);
        Assert.Equal(previous.EndUtc, next.StartUtc);
        Assert.Equal(TimeSpan.FromHours(25), next.EndUtc - next.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Intervals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(61), Assert.Single(next.Intervals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(previous.Totals).Duration);
        Assert.Equal(TimeSpan.FromMinutes(61), Assert.Single(next.Totals).Duration);
    }

    private static RecordingTime At(int seconds) => new(Start.AddSeconds(seconds).ToUniversalTime(), TimeSpan.FromSeconds(seconds).Ticks, Zone.Id);

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
