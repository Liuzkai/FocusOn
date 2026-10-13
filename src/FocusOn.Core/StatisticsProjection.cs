namespace FocusOn;

public sealed record StatisticsRequest(DateOnly StartDate, DateOnly EndDate, TimeSpan? StartTime = null, TimeSpan? EndTime = null)
{
    public bool IsPeriod => StartTime is not null;
}
public sealed record StatisticsPeriod(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string Label);
public sealed record StatisticsPlan(StatisticsRequest Request, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    DateTimeOffset SnapshotUtc, IReadOnlyList<StatisticsPeriod> Periods);
public sealed record StatisticsAllocation(RecordedInterval Interval, DateTimeOffset AllocationStartUtc,
    DateTimeOffset AllocationEndUtc, long DurationTicks)
{
    public static StatisticsAllocation From(RecordedInterval interval) => new(interval, interval.StartUtc, interval.EndUtc, interval.Duration.Ticks);
}
public sealed record StatisticsBucket(StatisticsPeriod Period, IReadOnlyList<AppTotal> Apps)
{
    public TimeSpan Duration => TimeSpan.FromTicks(Apps.Sum(x => x.Duration.Ticks));
}
public sealed record StatisticsView(StatisticsPlan Plan, IReadOnlyList<AppTotal> Apps, IReadOnlyList<StatisticsBucket> Buckets, string? Warning = null)
{
    public TimeSpan Duration => TimeSpan.FromTicks(Apps.Sum(x => x.Duration.Ticks));
}

public static class StatisticsProjection
{
    public static StatisticsPlan Plan(StatisticsRequest request, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
        if (request.EndDate < request.StartDate || request.EndDate > today || request.EndDate.DayNumber - request.StartDate.DayNumber >= 366)
            throw new ArgumentException("请选择不晚于今天且不超过366天的日期范围。");
        if ((request.StartTime is null) != (request.EndTime is null)) throw new ArgumentException("请填写完整的开始和结束时间。");
        DateTimeOffset start, end;
        if (request.StartTime is { } from && request.EndTime is { } to)
        {
            if (request.StartDate != request.EndDate || from < TimeSpan.Zero || to > TimeSpan.FromDays(1) || from >= to)
                throw new ArgumentException("时段须在同一天内，开始时间早于结束时间；结束可为24:00。");
            start = DailyProjection.LocalBoundary(request.StartDate.ToDateTime(TimeOnly.MinValue).Add(from), zone);
            end = to == TimeSpan.FromDays(1) ? DailyProjection.Bounds(request.EndDate, zone).EndUtc
                : DailyProjection.LocalBoundary(request.EndDate.ToDateTime(TimeOnly.MinValue).Add(to), zone, true);
        }
        else
        {
            start = DailyProjection.Bounds(request.StartDate, zone).StartUtc;
            end = DailyProjection.Bounds(request.EndDate, zone).EndUtc;
        }
        if (end > nowUtc) end = nowUtc;
        if (end < start) end = start;
        var periods = new List<StatisticsPeriod>();
        if (request.IsPeriod)
        {
            var offsetChanged = TimeZoneInfo.ConvertTime(start, zone).Offset != TimeZoneInfo.ConvertTime(end, zone).Offset;
            for (var at = start; at < end; at = at.AddHours(1))
            {
                var next = at.AddHours(1); if (next > end) next = end;
                var label = TimeZoneInfo.ConvertTime(at, zone).ToString(offsetChanged ? "HH:mm zzz" : "HH:mm");
                periods.Add(new(at, next, label));
            }
        }
        else
        {
            for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
            {
                var bounds = DailyProjection.Bounds(date, zone);
                var last = bounds.EndUtc < end ? bounds.EndUtc : end;
                periods.Add(new(bounds.StartUtc, last, date.ToString(request.StartDate.Year == request.EndDate.Year ? "MM-dd" : "yy-MM-dd")));
            }
        }
        return new(request, start, end, nowUtc, periods);
    }

    public static StatisticsView Create(StatisticsPlan plan, IReadOnlyList<StatisticsAllocation> allocations, string? warning = null)
    {
        var totals = new Dictionary<string, (string Name, long Ticks)>(StringComparer.Ordinal);
        var buckets = plan.Periods.Select(_ => new Dictionary<string, (string Name, long Ticks)>(StringComparer.Ordinal)).ToArray();
        foreach (var source in allocations.Where(x => x.Interval.Kind == IntervalKind.Usage))
        {
            var interval = source.Interval;
            var start = interval.StartUtc > plan.StartUtc ? interval.StartUtc : plan.StartUtc;
            var end = interval.EndUtc < plan.EndUtc ? interval.EndUtc : plan.EndUtc;
            var wall = (source.AllocationEndUtc - source.AllocationStartUtc).Ticks;
            if (end <= start || wall <= 0 || source.DurationTicks <= 0) continue;
            var key = interval.AppKey ?? "unknown"; var name = interval.AppName ?? "未知软件";
            // Find the first intersecting bucket; long ranges do not scan every day for each short record.
            var low = 0; var high = plan.Periods.Count;
            while (low < high) { var middle = (low + high) / 2; if (plan.Periods[middle].EndUtc <= start) low = middle + 1; else high = middle; }
            for (var index = low; index < buckets.Length && plan.Periods[index].StartUtc < end; index++)
            {
                var period = plan.Periods[index];
                var from = start > period.StartUtc ? start : period.StartUtc;
                var to = end < period.EndUtc ? end : period.EndUtc;
                if (to <= from) continue;
                var first = (long)((Int128)source.DurationTicks * (from - source.AllocationStartUtc).Ticks / wall);
                var last = (long)((Int128)source.DurationTicks * (to - source.AllocationStartUtc).Ticks / wall);
                var ticks = last - first;
                if (ticks <= 0) continue;
                Add(buckets[index], key, name, ticks); Add(totals, key, name, ticks);
            }
        }
        return new(plan, Rows(totals), plan.Periods.Select((period, i) => new StatisticsBucket(period, Rows(buckets[i]))).ToArray(), warning);
    }
    private static void Add(Dictionary<string, (string Name, long Ticks)> values, string key, string name, long ticks)
    {
        values.TryGetValue(key, out var previous); values[key] = (name, previous.Ticks + ticks);
    }
    private static AppTotal[] Rows(Dictionary<string, (string Name, long Ticks)> values) => values
        .Select(x => new AppTotal(x.Key, x.Value.Name, TimeSpan.FromTicks(x.Value.Ticks)))
        .OrderByDescending(x => x.Duration).ThenBy(x => x.AppKey, StringComparer.Ordinal).ToArray();
}
