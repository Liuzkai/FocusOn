namespace FocusOn;

public sealed record ReviewRequest(DateOnly Date, int? Hours, DateTimeOffset? PreserveStartUtc = null);
public sealed record ReviewPlan(ReviewRequest Request, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
    DateTimeOffset NavigationStartUtc, IReadOnlyList<DateOnly> Dates);
public sealed record ReviewView(DayView Daily, ReviewPlan Plan, IReadOnlyList<RecordedInterval> Intervals);

public static class ReviewProjection
{
    public static ReviewPlan Plan(ReviewRequest request, DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        if (request.Hours is { } hours && hours is not (1 or 2 or 4 or 6 or 8 or 12 or 24))
            throw new ArgumentOutOfRangeException(nameof(request));
        var (dayStart, dayEnd) = DailyProjection.Bounds(request.Date, zone);
        var end = nowUtc < dayEnd ? nowUtc : dayEnd;
        if (end < dayStart) end = dayStart;
        var start = request.Hours is { } span ? end.AddHours(-span) : dayStart;
        var navigation = start < dayStart ? start : dayStart;
        if (request.PreserveStartUtc is { } pinned && pinned < navigation) navigation = pinned;
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(navigation, zone).DateTime);
        var dates = new List<DateOnly>();
        for (var date = first; date <= request.Date; date = date.AddDays(1)) dates.Add(date);
        return new(request, start, end, navigation, dates);
    }

    public static ReviewView Create(ReviewPlan plan, IReadOnlyList<DayView> days)
    {
        var daily = days.Single(x => x.Date == plan.Request.Date);
        if (!plan.Dates.All(date => days.Count(x => x.Date == date) == 1))
            throw new ArgumentException("Missing or duplicate daily projection.", nameof(days));
        var merged = new List<RecordedInterval>();
        foreach (var group in days.SelectMany(x => x.Intervals).GroupBy(x => x.Id))
        {
            RecordedInterval? current = null;
            foreach (var part in group.OrderBy(x => x.StartUtc))
            {
                if (current is null) current = part;
                else
                {
                    if (current.EndUtc != part.StartUtc || current.Kind != part.Kind || current.AppKey != part.AppKey
                        || current.AppName != part.AppName || current.WindowTitle != part.WindowTitle)
                        throw new InvalidOperationException("Conflicting interval identity in daily projections.");
                    current = current with { EndUtc = part.EndUtc, Duration = current.Duration + part.Duration, IsCurrent = part.IsCurrent };
                }
            }
            if (current is not null && current.StartUtc < plan.EndUtc && current.EndUtc > plan.NavigationStartUtc) merged.Add(current);
        }
        return new(daily, plan, merged.OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray());
    }
}
