namespace FocusOn;

public static class DailyProjection
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) Bounds(DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return (DayStart(day, zone), DayStart(day.AddDays(1), zone));
    }

    private static DateTimeOffset DayStart(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            var invalid = local;
            do { local = local.AddHours(1); } while (zone.IsInvalidTime(local));
            // 找到缺失午夜后的首个有效时刻，不能假定跳变恰好是一小时。
            while (local.Ticks - invalid.Ticks > 1)
            {
                var middle = new DateTime(invalid.Ticks + (local.Ticks - invalid.Ticks) / 2, DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(middle)) invalid = middle;
                else local = middle;
            }
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
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
