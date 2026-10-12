namespace FocusOn;

public static class TimelineBounds
{
    public static (DateTimeOffset Start, DateTimeOffset End) For(DayView day, bool fullDay)
    {
        if (fullDay) return (day.StartUtc, day.EndUtc);
        var recorded = day.Intervals.Where(x => x.Kind != IntervalKind.Unrecorded && x.EndUtc > x.StartUtc).ToArray();
        if (recorded.Length == 0) return (day.StartUtc, day.StartUtc.AddSeconds(2));
        var first = recorded.Min(x => x.StartUtc);
        var last = recorded.Max(x => x.EndUtc);
        if (last - first < TimeSpan.FromSeconds(2))
        {
            last = first.AddSeconds(2);
            if (last > day.EndUtc) { last = day.EndUtc; first = last.AddSeconds(-2); }
        }
        return (first, last);
    }
}
