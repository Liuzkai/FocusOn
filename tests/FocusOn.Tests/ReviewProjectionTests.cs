using Xunit;

namespace FocusOn.Tests;

public sealed class ReviewProjectionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn-review-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "focuson.db");
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Review+08", TimeSpan.FromHours(8), "Review+08", "Review+08");
    private static readonly DateOnly Date = new(2026, 10, 13);
    private static readonly DateTimeOffset Midnight = new(2026, 10, 13, 0, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(6)] [InlineData(8)] [InlineData(12)] [InlineData(24)]
    public void Hours_are_elapsed_lookbacks_across_midnight_and_Today_starts_at_local_midnight(int hours)
    {
        var now = Midnight.AddMinutes(30).ToUniversalTime();
        var plan = ReviewProjection.Plan(new(Date, hours), now, Zone);
        Assert.Equal(now, plan.EndUtc);
        Assert.Equal(TimeSpan.FromHours(hours), plan.EndUtc - plan.StartUtc);
        Assert.Equal(new[] { Date.AddDays(-1), Date }, plan.Dates);
        var today = ReviewProjection.Plan(new(Date, null), now, Zone);
        Assert.Equal(Midnight.ToUniversalTime(), today.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(30), today.EndUtc - today.StartUtc);
        Assert.Equal(Date, Assert.Single(today.Dates));
    }

    [Theory]
    [InlineData(2026, 3, 8, 23)]
    [InlineData(2026, 11, 1, 25)]
    public void Historical_Today_respects_DST_day_length_but_24h_is_always_elapsed(int year, int month, int day, int dayHours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var date = new DateOnly(year, month, day);
        var tomorrow = DailyProjection.Bounds(date.AddDays(1), zone).EndUtc;
        var today = ReviewProjection.Plan(new(date, null), tomorrow, zone);
        var rolling = ReviewProjection.Plan(new(date, 24), tomorrow, zone);
        Assert.Equal(TimeSpan.FromHours(dayHours), today.EndUtc - today.StartUtc);
        Assert.Equal(TimeSpan.FromHours(24), rolling.EndUtc - rolling.StartUtc);
        Assert.Equal(today.EndUtc, rolling.EndUtc);
        Assert.NotEqual(today.StartUtc, rolling.StartUtc);
    }

    [Fact]
    public void Rolling_24h_can_require_three_calendar_dates_at_a_spring_DST_boundary()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var now = new DateTimeOffset(2026, 3, 9, 0, 30, 0, TimeSpan.FromHours(-4));
        var plan = ReviewProjection.Plan(new(new(2026, 3, 9), 24), now, zone);
        Assert.Equal(new[] { new DateOnly(2026, 3, 7), new(2026, 3, 8), new(2026, 3, 9) }, plan.Dates);
        Assert.Equal(new DateTimeOffset(2026, 3, 7, 23, 30, 0, TimeSpan.FromHours(-5)), plan.StartUtc);
    }

    [Fact]
    public void SQLite_cross_midnight_usage_and_timer_join_once_with_exact_duration_and_daily_totals()
    {
        var start = Midnight.AddMinutes(-1).ToUniversalTime();
        var duration = TimeSpan.FromMinutes(2).Add(TimeSpan.FromTicks(1));
        var finish = new RecordingTime(start.AddMinutes(2), duration.Ticks, Zone.Id);
        using (var session = new RecordingSession(DatabasePath))
        {
            var at = new RecordingTime(start, 0, Zone.Id);
            session.Observe(new(1, "reader", "Reader", "Original title"), at);
            session.ChangeTimer(TimerAction.Start, "Original timer", at);
            session.Finish(finish);
        }
        using var reopened = new RecordingSession(DatabasePath);
        var plan = ReviewProjection.Plan(new(Date, 1), finish.Utc.AddMinutes(20), Zone);
        var days = plan.Dates.Select(date => reopened.GetDay(date, Zone)).ToArray();
        var view = ReviewProjection.Create(plan, days);
        var usage = Assert.Single(view.Intervals, x => x.Kind == IntervalKind.Usage);
        var timer = Assert.Single(view.Intervals, x => x.Kind == IntervalKind.TaskTimer);
        foreach (var interval in new[] { usage, timer })
        {
            Assert.Equal(start, interval.StartUtc); Assert.Equal(finish.Utc, interval.EndUtc);
            Assert.Equal(duration, interval.Duration); Assert.False(interval.IsCurrent);
        }
        Assert.Equal("Original title", usage.WindowTitle); Assert.Equal("Original timer", timer.WindowTitle);
        Assert.Equal(usage.Duration.Ticks, days.Sum(x => x.Totals.Sum(total => total.Duration.Ticks)));
        Assert.Equal(TimeSpan.FromMinutes(1).Add(TimeSpan.FromTicks(1)), Assert.Single(view.Daily.Totals).Duration);
        Assert.Equal(view.Daily, days.Single(x => x.Date == Date));
        Assert.Equal(2, view.Intervals.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void Live_cross_day_read_keeps_current_identity_and_read_only_checkpoint_history()
    {
        var start = Midnight.AddMinutes(-20).ToUniversalTime();
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "reader", "Reader", "Live"), new(start, 0, Zone.Id));
        session.Checkpoint(new(start.AddMinutes(10), TimeSpan.FromMinutes(10).Ticks, Zone.Id));
        var live = new RecordingTime(Midnight.AddMinutes(10).ToUniversalTime(), TimeSpan.FromMinutes(30).Ticks, Zone.Id);
        var plan = ReviewProjection.Plan(new(Date, 1), live.Utc, Zone);
        var view = ReviewProjection.Create(plan, plan.Dates.Select(date => session.GetDay(date, Zone, live)).ToArray());
        var interval = Assert.Single(view.Intervals);
        Assert.True(interval.IsCurrent); Assert.Equal(start, interval.StartUtc); Assert.Equal(live.Utc, interval.EndUtc);
        Assert.Equal(TimeSpan.FromMinutes(30), interval.Duration);
        Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(view.Daily.Totals).Duration);
        using var persisted = new RecordingSession(DatabasePath);
        Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(persisted.GetDay(Date.AddDays(-1), Zone).Totals).Duration);
        Assert.Empty(persisted.GetDay(Date, Zone).Intervals);
    }

    [Fact]
    public void Deleting_previous_date_never_resurrects_its_part_in_a_rolling_review()
    {
        var start = Midnight.AddMinutes(-20).ToUniversalTime();
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "reader", "Reader", "Across midnight"), new(start, 0, Zone.Id));
        var at = new RecordingTime(Midnight.AddMinutes(10).ToUniversalTime(), TimeSpan.FromMinutes(30).Ticks, Zone.Id);
        session.Finish(at); session.DeleteDay(Date.AddDays(-1), Zone, at);
        var plan = ReviewProjection.Plan(new(Date, 1), at.Utc, Zone);
        var review = ReviewProjection.Create(plan, plan.Dates.Select(date => session.GetDay(date, Zone)).ToArray());
        Assert.Empty(session.GetDay(Date.AddDays(-1), Zone).Intervals);
        var remaining = Assert.Single(review.Intervals);
        Assert.Equal(Midnight.ToUniversalTime(), remaining.StartUtc);
        Assert.Equal(TimeSpan.FromMinutes(10), remaining.Duration);
        Assert.Equal(remaining.Duration, Assert.Single(review.Daily.Totals).Duration);
    }

    [Fact]
    public async Task Runtime_reads_multiple_days_with_one_clock_snapshot()
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var midnight = DailyProjection.Bounds(date, TimeZoneInfo.Local).StartUtc;
        var start = midnight.AddMinutes(-10);
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "reader", "Reader", "Atomic snapshot"), new(start, 0, TimeZoneInfo.Local.Id));
            session.Finish(new(midnight.AddMinutes(10), TimeSpan.FromMinutes(20).Ticks, TimeZoneInfo.Local.Id));
        }
        var reads = 0;
        using var runtime = new RecordingRuntime(DatabasePath, new RecordingClock(() => { Interlocked.Increment(ref reads); return 0; }));
        await runtime.Initialized;
        var review = await runtime.ReadReviewAsync(new(date, 24, midnight.AddHours(-1)));
        Assert.Equal(1, reads); Assert.Equal(date, review.Daily.Date);
        Assert.Equal(TimeSpan.FromMinutes(20), Assert.Single(review.Intervals, x => x.Kind == IntervalKind.Usage).Duration);
        Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(review.Daily.Totals).Duration);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
