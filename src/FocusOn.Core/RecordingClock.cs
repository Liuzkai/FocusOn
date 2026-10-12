using System.Diagnostics;

namespace FocusOn;

public sealed class RecordingClock
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly Func<long> monotonicTicks;

    public RecordingClock(Func<long>? monotonicTicks = null) => this.monotonicTicks = monotonicTicks ?? (() => stopwatch.Elapsed.Ticks);

    public RecordingTime Now()
    {
        lock (gate)
        {
            // Local is cached; refresh it so a system time-zone change is observable.
            TimeZoneInfo.ClearCachedData();
            return new RecordingTime(DateTimeOffset.UtcNow, monotonicTicks(), TimeZoneInfo.Local.Id);
        }
    }
}
