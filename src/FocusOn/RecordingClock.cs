using System.Diagnostics;

namespace FocusOn;

public sealed class RecordingClock
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly object gate = new();

    public RecordingTime Now()
    {
        lock (gate)
        {
            // Local is cached; refresh it so a Windows time-zone change is observable.
            TimeZoneInfo.ClearCachedData();
            return new RecordingTime(DateTimeOffset.UtcNow, stopwatch.Elapsed.Ticks, TimeZoneInfo.Local.Id);
        }
    }
}
