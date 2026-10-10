using Xunit;

namespace FocusOn.Tests;

public sealed class RangeFilterTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");

    [Fact]
    public void Range_only_selects_intersecting_daily_records_without_reclipping_or_changing_totals()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "编辑器", "文档"), At(Start, 0));
            session.Observe(new(2, "wechat", "微信", "两秒切换"), At(Start, 10));
            session.Observe(new(1, "editor", "编辑器", "文档"), At(Start, 12));
            session.Finish(At(Start, 22));
        }

        using var reopened = new RecordingSession(DatabasePath);
        var day = reopened.GetDay(new DateOnly(2026, 10, 9), TimeZoneInfo.Utc);
        var bounded = day.FilterRange(Start.AddSeconds(10), Start.AddSeconds(12));
        var inside = day.FilterRange(Start.AddSeconds(11), Start.AddSeconds(11.5));
        Assert.Same(day.Intervals[1], Assert.Single(bounded));
        var record = Assert.Single(inside);
        Assert.Same(day.Intervals[1], record);
        Assert.Equal(Start.AddSeconds(10), record.StartUtc);
        Assert.Equal(Start.AddSeconds(12), record.EndUtc);
        Assert.Equal(TimeSpan.FromSeconds(2), record.Duration);
        Assert.Equal(TimeSpan.FromSeconds(20), day.Totals.Single(x => x.AppKey == "editor").Duration);
        Assert.Equal(TimeSpan.FromSeconds(2), day.Totals.Single(x => x.AppKey == "wechat").Duration);
        Assert.Equal(3, day.Intervals.Count);
        Assert.Empty(day.FilterRange(Start.AddSeconds(10), Start.AddSeconds(10)));
    }

    [Fact]
    public void Filtering_a_midnight_projected_record_keeps_its_daily_projection_and_full_day_total()
    {
        var start = new DateTimeOffset(2026, 10, 9, 23, 59, 0, TimeSpan.Zero);
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "reader", "阅读器", "跨日阅读"), At(start, 0));
        session.Finish(At(start, 120));
        var day = session.GetDay(new DateOnly(2026, 10, 10), TimeZoneInfo.Utc);
        var record = Assert.Single(day.FilterRange(start.AddSeconds(90), start.AddSeconds(105)));
        Assert.Equal(start.AddSeconds(60), record.StartUtc);
        Assert.Equal(start.AddSeconds(120), record.EndUtc);
        Assert.Equal(TimeSpan.FromMinutes(1), record.Duration);
        Assert.Equal(TimeSpan.FromMinutes(1), Assert.Single(day.Totals).Duration);
    }

    private static RecordingTime At(DateTimeOffset start, double seconds) => new(start.AddSeconds(seconds), TimeSpan.FromSeconds(seconds).Ticks, "UTC");

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
