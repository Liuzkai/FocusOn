using Xunit;

namespace FocusOn.Tests;

public sealed class StatisticsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FocusOn-stats-" + Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "recording.db");
    private static readonly DateOnly Date = new(2026, 10, 13);
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);
    private static RecordingTime At(double minutes) => new(Start.AddMinutes(minutes), TimeSpan.FromMinutes(minutes).Ticks, "UTC");

    [Fact]
    public void SQLite_multi_day_statistics_keep_empty_days_and_exclude_timers_stops_and_gaps()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "Editor", "Private title"), At(0));
        session.ChangeTimer(TimerAction.Start, "Independent timer", At(0));
        session.Observe(new(2, "browser", "Browser", "Another private title"), At(60));
        session.SetManualPaused(true, null, At(90));
        session.Finish(At(120));
        var view = session.GetStatistics(new(Date.AddDays(-2), Date), TimeZoneInfo.Utc, At(120));
        Assert.Equal(TimeSpan.FromMinutes(90), view.Duration);
        Assert.Equal(new[] { "editor", "browser" }, view.Apps.Select(x => x.AppKey));
        Assert.Equal(TimeSpan.FromMinutes(60), view.Apps[0].Duration);
        Assert.Equal(3, view.Buckets.Count);
        Assert.Equal(new[] { TimeSpan.Zero, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60) }, view.Buckets.Select(x => x.Duration));
        Assert.Equal(view.Duration.Ticks, view.Buckets.Sum(x => x.Duration.Ticks));
        Assert.Empty(view.Buckets[0].Apps);
    }

    [Fact]
    public void Partial_period_and_hour_buckets_use_original_monotonic_allocation_without_tick_loss()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "Editor", "Unchanged"), At(0));
            session.Finish(new(Start.AddHours(3), TimeSpan.FromHours(3).Ticks + 7, "UTC"));
        }
        using var reopened = new RecordingSession(DatabasePath);
        var now = new RecordingTime(Start.AddHours(5), TimeSpan.FromHours(5).Ticks, "UTC");
        var view = reopened.GetStatistics(new(Date, Date, TimeSpan.FromMinutes(15), TimeSpan.FromHours(2)), TimeZoneInfo.Utc, now);
        var totalTicks = TimeSpan.FromHours(3).Ticks + 7;
        var from = (long)((Int128)totalTicks * TimeSpan.FromMinutes(45).Ticks / TimeSpan.FromHours(3).Ticks);
        var to = (long)((Int128)totalTicks * TimeSpan.FromMinutes(150).Ticks / TimeSpan.FromHours(3).Ticks);
        Assert.Equal(to - from, view.Duration.Ticks);
        Assert.Equal(2, view.Buckets.Count);
        Assert.Equal(view.Duration.Ticks, view.Buckets.Sum(x => x.Duration.Ticks));
        Assert.Equal(TimeSpan.FromMinutes(60).Ticks + 3, view.Buckets[0].Duration.Ticks);
        Assert.Equal(TimeSpan.FromMinutes(45).Ticks + 1, view.Buckets[1].Duration.Ticks);
        Assert.Equal(view.Duration, Assert.Single(view.Apps).Duration);
    }

    [Fact]
    public void Date_deletion_and_reopen_preserve_remaining_allocations_without_resurrecting_old_usage()
    {
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "Editor", "Across midnight"), At(0));
            session.Finish(new(Start.AddMinutes(90), TimeSpan.FromMinutes(90).Ticks + 1, "UTC"));
            session.DeleteDay(Date.AddDays(-1), TimeZoneInfo.Utc, At(120));
        }
        using var reopened = new RecordingSession(DatabasePath);
        var view = reopened.GetStatistics(new(Date.AddDays(-1), Date), TimeZoneInfo.Utc, At(150));
        Assert.Equal(TimeSpan.FromHours(1).Ticks + 1, view.Duration.Ticks);
        Assert.Equal(TimeSpan.Zero, view.Buckets[0].Duration);
        Assert.Equal(view.Duration, view.Buckets[1].Duration);
        Assert.Equal(view.Duration, Assert.Single(reopened.GetDay(Date, TimeZoneInfo.Utc).Totals).Duration);
    }

    [Fact]
    public void Live_statistics_use_one_snapshot_and_do_not_persist_the_unsaved_tail()
    {
        using var session = new RecordingSession(DatabasePath);
        session.Observe(new(1, "editor", "Editor", "Live"), At(0)); session.Checkpoint(At(10));
        var view = session.GetStatistics(new(Date.AddDays(-1), Date), TimeZoneInfo.Utc, At(60));
        Assert.Equal(TimeSpan.FromHours(1), view.Duration); Assert.Equal(2, view.Buckets.Count);
        Assert.Equal(TimeSpan.FromMinutes(30), view.Buckets[1].Duration);
        using var persisted = new RecordingSession(DatabasePath);
        Assert.Equal(TimeSpan.FromMinutes(10), persisted.GetStatistics(new(Date.AddDays(-1), Date), TimeZoneInfo.Utc, At(60)).Duration);
        Assert.Empty(persisted.GetDay(Date, TimeZoneInfo.Utc).Totals);
    }

    [Theory]
    [InlineData(3, 8, 23)]
    [InlineData(11, 1, 25)]
    public void Whole_local_day_respects_DST_and_24_00_matches_the_multi_day_boundary(int month, int day, int hours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var date = new DateOnly(2026, month, day);
        var now = DailyProjection.Bounds(date.AddDays(1), zone).EndUtc;
        var whole = StatisticsProjection.Plan(new(date, date, TimeSpan.Zero, TimeSpan.FromDays(1)), now, zone);
        var daily = StatisticsProjection.Plan(new(date, date), now, zone);
        Assert.Equal(TimeSpan.FromHours(hours), whole.EndUtc - whole.StartUtc);
        Assert.Equal(daily.StartUtc, whole.StartUtc); Assert.Equal(daily.EndUtc, whole.EndUtc);
        Assert.Equal(hours, whole.Periods.Count);
        Assert.Equal(hours, whole.Periods.Sum(x => (x.EndUtc - x.StartUtc).TotalHours));
    }

    [Fact]
    public void Repeated_hour_has_distinct_offsets_and_missing_local_period_is_empty()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var fall = new DateOnly(2026, 11, 1); var spring = new DateOnly(2026, 3, 8);
        var repeated = StatisticsProjection.Plan(new(fall, fall, TimeSpan.FromHours(1), TimeSpan.FromHours(2)), DailyProjection.Bounds(fall, zone).EndUtc, zone);
        Assert.Equal(TimeSpan.FromHours(2), repeated.EndUtc - repeated.StartUtc);
        Assert.Equal(new[] { "01:00 -04:00", "01:00 -05:00" }, repeated.Periods.Select(x => x.Label));
        var missing = StatisticsProjection.Plan(new(spring, spring, TimeSpan.FromMinutes(135), TimeSpan.FromMinutes(165)), DailyProjection.Bounds(spring, zone).EndUtc, zone);
        Assert.Equal(missing.StartUtc, missing.EndUtc); Assert.Empty(missing.Periods);
    }

    [Fact]
    public void Period_ending_at_next_days_repeated_midnight_excludes_the_next_calendar_day()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Cuba Standard Time"); var date = new DateOnly(2026, 10, 31);
        var plan = StatisticsProjection.Plan(new(date, date, TimeSpan.Zero, TimeSpan.FromDays(1)), DailyProjection.Bounds(date.AddDays(1), zone).EndUtc, zone);
        Assert.Equal(DailyProjection.Bounds(date, zone).EndUtc, plan.EndUtc);
        Assert.Equal(TimeSpan.FromHours(24), plan.EndUtc - plan.StartUtc);
    }

    [Fact]
    public async Task Runtime_statistics_capture_one_clock_read_and_only_persisted_historical_usage()
    {
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var start = DailyProjection.Bounds(date, TimeZoneInfo.Local).StartUtc.AddHours(9);
        using (var session = new RecordingSession(DatabasePath))
        {
            session.Observe(new(1, "editor", "Editor", "Historical"), new(start, 0, TimeZoneInfo.Local.Id));
            session.Finish(new(start.AddHours(1), TimeSpan.FromHours(1).Ticks, TimeZoneInfo.Local.Id));
        }
        var calls = 0;
        using var runtime = new RecordingRuntime(DatabasePath, new RecordingClock(() => { Interlocked.Increment(ref calls); return 0; }));
        await runtime.Initialized;
        var view = await runtime.ReadStatisticsAsync(new(date, date));
        Assert.Equal(1, calls); Assert.Equal(TimeSpan.FromHours(1), view.Duration);
        Assert.Equal(view.Duration, Assert.Single(view.Buckets).Duration);
        Assert.Equal("editor", Assert.Single(view.Apps).AppKey);
    }

    [Fact]
    public void Future_time_is_not_counted_and_invalid_ranges_are_rejected()
    {
        var now = new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);
        var plan = StatisticsProjection.Plan(new(Date, Date, TimeSpan.FromHours(9), TimeSpan.FromHours(18)), now, TimeZoneInfo.Utc);
        Assert.Equal(now, plan.EndUtc); Assert.Single(plan.Periods);
        Assert.Throws<ArgumentException>(() => StatisticsProjection.Plan(new(Date, Date.AddDays(1)), now, TimeZoneInfo.Utc));
        Assert.Throws<ArgumentException>(() => StatisticsProjection.Plan(new(Date, Date.AddDays(-1)), now, TimeZoneInfo.Utc));
        Assert.Throws<ArgumentException>(() => StatisticsProjection.Plan(new(Date.AddDays(-366), Date), now, TimeZoneInfo.Utc));
        Assert.Throws<ArgumentException>(() => StatisticsProjection.Plan(new(Date, Date, TimeSpan.FromHours(18), TimeSpan.FromHours(9)), now, TimeZoneInfo.Utc));
        Assert.Throws<ArgumentException>(() => StatisticsProjection.Plan(new(Date, Date, TimeSpan.Zero), now, TimeZoneInfo.Utc));
        var future = StatisticsProjection.Plan(new(Date, Date, TimeSpan.FromHours(12), TimeSpan.FromHours(13)), now, TimeZoneInfo.Utc);
        Assert.Equal(future.StartUtc, future.EndUtc); Assert.Empty(future.Periods);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
