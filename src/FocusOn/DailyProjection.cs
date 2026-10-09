namespace FocusOn;

public static class DailyProjection
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) Bounds(DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var start = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        return (new DateTimeOffset(start), new DateTimeOffset(end));
    }

    public static RecordedInterval? Clip(RecordedInterval interval, DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var clippedStart = interval.StartUtc > startUtc ? interval.StartUtc : startUtc;
        var clippedEnd = interval.EndUtc < endUtc ? interval.EndUtc : endUtc;
        var wallTicks = (interval.EndUtc - interval.StartUtc).Ticks;
        if (clippedEnd <= clippedStart || wallTicks <= 0 || interval.Duration <= TimeSpan.Zero) return null;

        // 分配累计端点再相减，连续日期的取整余量仍归属同一个原始片段。
        var startDuration = (long)((Int128)interval.Duration.Ticks * (clippedStart - interval.StartUtc).Ticks / wallTicks);
        var endDuration = (long)((Int128)interval.Duration.Ticks * (clippedEnd - interval.StartUtc).Ticks / wallTicks);
        return interval with
        {
            StartUtc = clippedStart,
            EndUtc = clippedEnd,
            Duration = TimeSpan.FromTicks(endDuration - startDuration),
            IsCurrent = interval.IsCurrent && interval.EndUtc < endUtc
        };
    }
}
