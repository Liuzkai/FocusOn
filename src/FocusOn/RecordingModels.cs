namespace FocusOn;

public readonly record struct RecordingTime(DateTimeOffset Utc, long MonotonicTicks, string TimeZoneId);

public sealed record ForegroundObservation(long WindowId, string? AppKey, string? AppName, string? WindowTitle, uint ProcessId = 0);

public sealed record RecordingConditions(bool ManualPaused, bool Sleeping, bool Locked, bool DisplayOff, bool Available = true);

public enum IntervalKind
{
    Usage,
    ManualPause,
    Sleep,
    Locked,
    DisplayOff,
    Unrecorded,
    TaskTimer
}

public sealed record RecordedInterval(
    long Id,
    IntervalKind Kind,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeSpan Duration,
    string? AppKey,
    string? AppName,
    string? WindowTitle,
    bool IsCurrent = false);

public sealed record AppTotal(string AppKey, string AppName, TimeSpan Duration);

public sealed record TitleTimerState(string Title, TimeSpan Duration, bool IsRunning);

public enum TimerAction { Start, Pause, Resume, Stop }

public sealed record DayView(
    DateOnly Date,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<RecordedInterval> Intervals,
    IReadOnlyList<AppTotal> Totals,
    string Status,
    string? Warning,
    TitleTimerState? Timer = null)
{
    public IReadOnlyList<RecordedInterval> FilterRange(DateTimeOffset startUtc, DateTimeOffset endUtc) => endUtc <= startUtc
        ? []
        : Intervals.Where(interval => interval.StartUtc < endUtc && interval.EndUtc > startUtc).ToArray();
}
