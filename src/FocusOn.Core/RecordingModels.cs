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

public sealed record TitleTimerState(string Title, TimeSpan Duration, bool IsRunning,
    TimeSpan? TargetDuration = null, bool IsCompleted = false, long Id = 0)
{
    public bool IsCountdown => TargetDuration is not null;
    public TimeSpan Remaining => TargetDuration is { } target ? TimeSpan.FromTicks(Math.Max(0, target.Ticks - Duration.Ticks)) : TimeSpan.Zero;
    public double Progress => TargetDuration is { Ticks: > 0 } target ? Math.Clamp((double)Duration.Ticks / target.Ticks, 0, 1) : 0;
}

public enum TimerAction { Start, Pause, Resume, Stop, StartCountdown }

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
