namespace FocusOn;

/// <summary>Serializes platform snapshots and control boundaries without depending on a UI toolkit.</summary>
public sealed class RecordingController : IAsyncDisposable
{
    private readonly RecordingClock clock = PlatformClock.Create();
    private readonly object gate = new();
    private readonly RecordingRuntime runtime;
    private IForegroundObserver? observer;
    private ISessionObserver? session;
    private long systemVersion, manualVersion;
    private long cutoff = long.MaxValue;
    private bool stopped;
    private bool suspendObserved;
    private volatile string? foregroundWarning;
    public bool ManualPaused { get; private set; }

    public RecordingController(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        runtime = new RecordingRuntime(Path.Combine(dataDirectory, "focuson.db"), clock);
    }

    public async Task StartAsync()
    {
        await runtime.Initialized;
        await runtime.SetSystemConditions(new(false, false, false, false, false), null, clock.Now());
        observer = PlatformObserver.CreateForeground(clock, (observation, at) =>
        {
            lock (gate)
            {
                if (at.MonotonicTicks > cutoff) return;
                if (session?.ReadCurrentState().Sleeping == false) suspendObserved = false;
                if (observation is not null) foregroundWarning = null;
                runtime.Observe(observation, at);
            }
        }, (at, reason) =>
        {
            lock (gate)
            {
                if (at.MonotonicTicks > cutoff) return;
                foregroundWarning = reason;
                runtime.MarkUnreliable(at, reason);
            }
        }, (at, reason) =>
        {
            lock (gate)
            {
                if (at.MonotonicTicks > cutoff) return;
                if (suspendObserved) { suspendObserved = false; return; }
                runtime.Interrupt(at, reason);
            }
        });
        session = PlatformObserver.CreateSession(state =>
        {
            if (state.Sleeping) lock (gate) suspendObserved = true;
            var saved = ApplySystem(state);
            if (state.Sleeping)
            {
                try { saved.Wait(TimeSpan.FromMilliseconds(1500)); }
                catch (AggregateException) { }
            }
            else _ = ObserveControlAsync(saved);
        });
        session.Start();
        observer.Start();
    }

    private async Task ObserveControlAsync(Task saved)
    {
        try { await saved.ConfigureAwait(false); }
        catch (Exception error) { foregroundWarning = error.Message; }
    }

    public void Poll() { if (!stopped) observer?.Poll(); }
    public async Task<DayView> ReadDayAsync(DateOnly day)
    {
        return AddPlatformWarning(await runtime.ReadDayAsync(day));
    }
    public async Task<ReviewView> ReadReviewAsync(ReviewRequest request)
    {
        var view = await runtime.ReadReviewAsync(request);
        return view with { Daily = AddPlatformWarning(view.Daily) };
    }
    private DayView AddPlatformWarning(DayView view)
    {
        var state = session?.ReadCurrentState();
        var warning = foregroundWarning ?? state?.Warning;
        if (warning is null) return view;
        return view with { Status = "前台采集不可用", Warning = string.Join("\n", new[] { view.Warning, warning }.Where(x => !string.IsNullOrWhiteSpace(x))) };
    }

    public Task ChangeTimerAsync(TimerAction action, string? title) => runtime.ChangeTimerAsync(action, title);

    public Task SetManualPausedAsync(bool paused)
    {
        long version;
        lock (gate)
        {
            if (stopped) return Task.CompletedTask;
            ManualPaused = paused;
            version = ++manualVersion;
            if (paused) return runtime.SetManualPaused(true, null, clock.Now());
        }
        return WithSnapshot((observation, at) =>
        {
            lock (gate) return stopped || version != manualVersion ? Task.CompletedTask : runtime.SetManualPaused(false, observation, at);
        });
    }

    private Task ApplySystem(SessionState state)
    {
        long version;
        lock (gate)
        {
            if (stopped) return Task.CompletedTask;
            version = ++systemVersion;
            if (!state.Available || state.Sleeping || state.Locked || state.DisplayOff)
                return runtime.SetSystemConditions(state.Conditions, null, clock.Now());
        }
        return WithSnapshot((observation, at) =>
        {
            lock (gate) return stopped || version != systemVersion ? Task.CompletedTask : runtime.SetSystemConditions(state.Conditions, observation, at);
        });
    }

    public async Task DeleteDayAsync(DateOnly day)
    {
        await runtime.DeleteDayAsync(day);
        var resumed = false;
        while (!resumed && !stopped)
        {
            long version;
            SessionState state;
            lock (gate) { version = systemVersion; state = session!.ReadCurrentState(); }
            await WithSnapshot(async (observation, at) =>
            {
                Task<bool>? saved;
                lock (gate) saved = stopped || version != systemVersion ? null : runtime.ResumeAfterDeleteAsync(observation, state.Conditions, at);
                if (saved is not null) resumed = await saved;
            });
        }
    }

    private Task WithSnapshot(Func<ForegroundObservation?, RecordingTime, Task> save)
    {
        if (stopped || observer is null) return Task.CompletedTask;
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.RequestSnapshot(async (observation, at) =>
        {
            try { await save(observation, at); result.TrySetResult(); }
            catch (Exception error) { result.TrySetException(error); }
        });
        return result.Task;
    }

    public async ValueTask DisposeAsync()
    {
        RecordingTime at;
        lock (gate)
        {
            if (stopped) return;
            at = clock.Now();
            stopped = true;
            cutoff = at.MonotonicTicks;
        }
        // Native input/menu callbacks may still own their platform event lock.
        // Yield before joining observers so their final native read can finish.
        // The saved boundary remains the original 'at', not the cleanup completion.
        await Task.Run(() => { session?.Dispose(); observer?.Dispose(); });
        lock (gate) cutoff = -1;
        try
        {
            var warning = await runtime.FinishAsync(at);
            if (warning is not null) throw new IOException(warning);
        }
        finally { runtime.Dispose(); }
    }
}
