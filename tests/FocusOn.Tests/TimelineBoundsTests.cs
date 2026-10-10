using Xunit;

namespace FocusOn.Tests;

public sealed class TimelineBoundsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static RecordedInterval Interval(long id, IntervalKind kind, int from, int to) =>
        new(id, kind, Start.AddSeconds(from), Start.AddSeconds(to), TimeSpan.FromSeconds(to - from), null, null, null);
    private static DayView Day(params RecordedInterval[] records) => new(new(2026, 10, 9), Start, Start.AddDays(1), records, [], "", null);

    [Fact]
    public void Recorded_extent_excludes_outer_gaps_includes_stop_and_timer_and_grows_with_latest_record()
    {
        var day = Day(Interval(1, IntervalKind.Unrecorded, 0, 100), Interval(2, IntervalKind.Usage, 100, 200),
            Interval(3, IntervalKind.ManualPause, 200, 300), Interval(-1, IntervalKind.TaskTimer, 150, 400));
        Assert.Equal((Start.AddSeconds(100), Start.AddSeconds(400)), TimelineBounds.For(day, false));
        Assert.Equal((Start, Start.AddDays(1)), TimelineBounds.For(day, true));
        var grown = day with { Intervals = [.. day.Intervals, Interval(4, IntervalKind.Usage, 300, 450)] };
        Assert.Equal(Start.AddSeconds(450), TimelineBounds.For(grown, false).End);
    }

    [Fact]
    public void Empty_or_subsecond_day_has_nonzero_extent_and_full_mode_respects_real_day_length()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), TimelineBounds.For(Day(), false).End - Start);
        var day = Day(Interval(1, IntervalKind.Usage, 86399, 86400));
        Assert.Equal((Start.AddSeconds(86398), Start.AddDays(1)), TimelineBounds.For(day, false));
        var shortened = Day() with { EndUtc = Start.AddHours(23) };
        Assert.Equal((Start, Start.AddHours(23)), TimelineBounds.For(shortened, true));
    }
}
