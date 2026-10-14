using Xunit;

namespace FocusOn.Tests;

public sealed class PollingObserverTests
{
    [Fact]
    public async Task Slow_native_lookup_fences_missing_time_and_pauses_the_independent_timer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-sampling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var clock = new RecordingClock();
        try
        {
            using var runtime = new RecordingRuntime(Path.Combine(directory, "focuson.db"), clock);
            await runtime.Initialized; await runtime.ChangeTimerAsync(TimerAction.Start, "reading");
            var sampled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            using var observer = new DelayedReader(clock, started, release, (observation, at) =>
            {
                runtime.Observe(observation, at); sampled.TrySetResult();
            }, runtime.Interrupt);
            observer.Start();
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            await Task.Delay(2200); release.Set();
            await sampled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var view = await runtime.ReadDayAsync(DateOnly.FromDateTime(DateTime.Today));
            Assert.False(view.Timer!.IsRunning); Assert.Equal(TimeSpan.Zero, view.Timer.Duration);
            Assert.Empty(view.Totals);
            observer.Dispose(); await runtime.FinishAsync(clock.Now());
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class DelayedReader(RecordingClock clock, ManualResetEventSlim started, ManualResetEventSlim release,
        Action<ForegroundObservation?, RecordingTime> observed, Action<RecordingTime, string> interrupted)
        : PollingForegroundObserver(clock, observed, (_, error) => throw new Exception(error), interrupted)
    {
        private int reads;
        protected override ForegroundObservation? ReadForeground()
        {
            // Control exactly one delayed lookup. A later valid sample would legitimately
            // resume usage while a busy runner is scheduling the assertion continuation.
            if (Interlocked.Increment(ref reads) > 1) return null;
            started.Set(); release.Wait(TimeSpan.FromSeconds(5)); return new(1, "editor", "Editor", "document");
        }
    }
}
