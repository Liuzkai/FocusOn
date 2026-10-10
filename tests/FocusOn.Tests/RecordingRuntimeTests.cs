using Xunit;

namespace FocusOn.Tests;

public sealed class RecordingRuntimeTests
{
    [Fact]
    public async Task Exit_commits_the_captured_stop_boundary_instead_of_the_later_worker_time()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-exit-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "focuson.db");
        try
        {
            var clock = new RecordingClock();
            var start = clock.Now() with { Utc = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero) };
            using (var runtime = new RecordingRuntime(path, clock))
            {
                await runtime.Initialized;
                runtime.Observe(new ForegroundObservation(1, "exe:TEST", "Test", "before exit"), start);
                var cutoff = start with { Utc = start.Utc.AddSeconds(1), MonotonicTicks = start.MonotonicTicks + TimeSpan.TicksPerSecond };
                Assert.Null(await runtime.FinishAsync(cutoff));
            }
            using var reopened = new RecordingSession(path);
            var day = DateOnly.FromDateTime(start.Utc.LocalDateTime);
            var view = reopened.GetDay(day, TimeZoneInfo.Local);
            Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(view.Totals).Duration);
            Assert.Equal(TimeSpan.FromSeconds(1), Assert.Single(view.Intervals).EndUtc - view.Intervals[0].StartUtc);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
