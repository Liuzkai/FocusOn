using System.Windows;
using System.Windows.Threading;

namespace FocusOn;

public partial class App : Application
{
    private RecordingRuntime? runtime;
    private WindowsForegroundObserver? observer;
    private WindowsSessionObserver? sessionObserver;
    private volatile WindowsSessionState systemState = new(false, false, false, false);
    private volatile bool manualPaused = false;
    private DispatcherTimer? checkpoint;
    private volatile bool exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var clock = new RecordingClock();
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusOn");
            // Keep development smoke runs out of the user's activity database.
            if (e.Args.Length == 2 && e.Args[0] == "--data-dir") dataDirectory = Path.GetFullPath(e.Args[1]);
            Directory.CreateDirectory(dataDirectory);
            runtime = new RecordingRuntime(Path.Combine(dataDirectory, "focuson.db"), clock);
            await runtime.Initialized;
            observer = new WindowsForegroundObserver(clock, runtime.Observe);
            sessionObserver = new WindowsSessionObserver(state =>
            {
                systemState = state;
                var saved = ApplyConditions();
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
                try { await ApplyConditions(); }
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

    private Task ApplyConditions()
    {
        if (exiting || observer is null || runtime is null) return Task.CompletedTask;
        var state = systemState;
        var conditions = new RecordingConditions(manualPaused, state.Sleeping, state.Locked, state.DisplayOff, state.Available);
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        observer.RequestSnapshot(async (observation, at) =>
        {
            try { await runtime.SetConditions(conditions, observation, at); saved.TrySetResult(); }
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
