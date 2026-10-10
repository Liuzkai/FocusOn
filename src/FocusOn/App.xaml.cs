using System.Windows;
using System.Windows.Threading;

namespace FocusOn;

public partial class App : Application
{
    private RecordingRuntime? runtime;
    private WindowsForegroundObserver? observer;
    private WindowsSessionObserver? sessionObserver;
    private RecordingClock? recordingClock;
    private volatile bool manualPaused = false;
    private DispatcherTimer? checkpoint;
    private volatile bool exiting;
    private readonly object controlGate = new();
    private long systemVersion;
    private long manualVersion;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var clock = recordingClock = new RecordingClock();
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusOn");
            // Keep development smoke runs out of the user's activity database.
            if (e.Args.Length == 2 && e.Args[0] == "--data-dir") dataDirectory = Path.GetFullPath(e.Args[1]);
            Directory.CreateDirectory(dataDirectory);
            runtime = new RecordingRuntime(Path.Combine(dataDirectory, "focuson.db"), clock);
            await runtime.Initialized;
            observer = new WindowsForegroundObserver(clock, runtime.Observe, runtime.MarkUnreliable);
            sessionObserver = new WindowsSessionObserver(state =>
            {
                var saved = ApplySystemConditions(state);
                if (state.Sleeping)
                {
                    // Windows allows a short suspend handling window; save before returning.
                    try { saved.Wait(TimeSpan.FromMilliseconds(1500)); }
                    catch (AggregateException) { }
                }
            });
            sessionObserver.Start();
            observer.Start();
            checkpoint = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            checkpoint.Tick += (_, _) => observer.Poll();
            checkpoint.Start();
            var window = new MainWindow(runtime.ReadDayAsync);
            window.PauseRequested += async (_, _) =>
            {
                manualPaused = !manualPaused;
                window.SetPauseState(manualPaused);
                try { await ApplyManualPause(manualPaused); }
                catch (Exception) { /* The runtime exposes saving faults in the next status refresh. */ }
            };
            MainWindow = window;
            window.Closed += async (_, _) => await ExitAsync();
            window.Show();
        }
        catch (Exception error)
        {
            MessageBox.Show($"无法开始记录：{error.Message}\n请检查本地数据目录是否可写。", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Error);
            await ExitAsync();
        }
    }

    private Task ApplySystemConditions(WindowsSessionState state)
    {
        var conditions = new RecordingConditions(false, state.Sleeping, state.Locked, state.DisplayOff, state.Available);
        long version;
        lock (controlGate)
        {
            version = ++systemVersion;
            // A stop boundary needs no title lookup, which can be delayed until after wake.
            if (!state.Available || state.Sleeping || state.Locked || state.DisplayOff)
                return runtime is null || recordingClock is null || exiting ? Task.CompletedTask :
                    runtime.SetSystemConditions(conditions, null, recordingClock.Now());
        }
        return WithForegroundSnapshot((observation, at) =>
        {
            lock (controlGate)
                return version != systemVersion || exiting ? Task.CompletedTask :
                    runtime!.SetSystemConditions(conditions, observation, at);
        });
    }

    private Task ApplyManualPause(bool paused)
    {
        long version;
        lock (controlGate)
        {
            version = ++manualVersion;
            if (paused)
                return runtime is null || recordingClock is null || exiting ? Task.CompletedTask :
                    runtime.SetManualPaused(true, null, recordingClock.Now());
        }
        return WithForegroundSnapshot((observation, at) =>
        {
            lock (controlGate)
                return version != manualVersion || exiting ? Task.CompletedTask :
                    runtime!.SetManualPaused(false, observation, at);
        });
    }

    private Task WithForegroundSnapshot(Func<ForegroundObservation?, RecordingTime, Task> save)
    {
        if (exiting || observer is null || runtime is null) return Task.CompletedTask;
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.RequestSnapshot(async (observation, at) =>
        {
            try { await save(observation, at); saved.TrySetResult(); }
            catch (Exception error) { saved.TrySetException(error); }
        });
        return saved.Task;
    }

    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        checkpoint?.Stop();
        (MainWindow as MainWindow)?.StopRefresh();
        sessionObserver?.Dispose();
        observer?.Dispose();
        try { if (runtime is not null) await runtime.FinishAsync(); }
        catch (Exception error) { MessageBox.Show($"最后一段未能保存：{error.Message}", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Warning); }
        runtime?.Dispose();
        Shutdown();
    }
}
