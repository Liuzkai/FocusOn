using System.Windows;
using System.Windows.Threading;

namespace FocusOn;

public partial class App : Application
{
    private RecordingRuntime? runtime;
    private WindowsForegroundObserver? observer;
    private WindowsSessionObserver? sessionObserver;
    private RecordingClock? recordingClock;
    private SingleInstance? singleInstance;
    private TrayController? tray;
    private volatile bool manualPaused;
    private DispatcherTimer? checkpoint;
    private volatile bool exiting;
    private bool pendingOpen;
    private readonly object controlGate = new();
    private long systemVersion;
    private long manualVersion;
    private long observationCutoff = long.MaxValue;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var clock = recordingClock = new RecordingClock();
            var combined = e.Args.Length == 3 && e.Args[0] == "--startup" && e.Args[1] == "--data-dir";
            var isolated = combined || (e.Args.Length == 2 && e.Args[0] == "--data-dir");
            var startup = combined || (e.Args.Length == 1 && e.Args[0] == "--startup");
            if (e.Args.Length != 0 && !isolated && !startup)
                throw new ArgumentException("支持的启动参数：--startup 或 --data-dir <目录>。");
            var dataDirectory = isolated ? Path.GetFullPath(e.Args[^1]) :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusOn");
            singleInstance = new SingleInstance(dataDirectory);
            if (!singleInstance.IsPrimary)
            {
                singleInstance.SignalExisting();
                singleInstance.Dispose();
                singleInstance = null;
                Shutdown();
                return;
            }
            singleInstance.Listen(() =>
            {
                if (exiting || Dispatcher.HasShutdownStarted) return;
                try { Dispatcher.BeginInvoke(new Action(ShowMainWindow)); }
                catch (InvalidOperationException) { /* Shutdown can race an activation signal. */ }
            });
            Directory.CreateDirectory(dataDirectory);
            runtime = new RecordingRuntime(Path.Combine(dataDirectory, "focuson.db"), clock);
            await runtime.Initialized;
            observer = new WindowsForegroundObserver(clock, (observation, at) =>
            {
                lock (controlGate)
                    if (at.MonotonicTicks <= observationCutoff) runtime.Observe(observation, at);
            }, (at, reason) =>
            {
                lock (controlGate)
                    if (at.MonotonicTicks <= observationCutoff) runtime.MarkUnreliable(at, reason);
            });
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
            var window = new MainWindow(ReadDayAsync, DeleteDayAsync);
            window.PauseRequested += async (_, _) => await ChangeManualPauseAsync(!manualPaused);
            window.Closing += (_, args) =>
            {
                if (exiting) return;
                args.Cancel = true;
                window.Hide();
            };
            MainWindow = window;
            tray = new TrayController(ShowMainWindow,
                () => _ = ChangeManualPauseAsync(true), () => _ = ChangeManualPauseAsync(false),
                () => _ = ExitAsync());
            if (!isolated && !startup)
            {
                try { StartupRegistration.Ensure(Environment.ProcessPath!); }
                catch (Exception error)
                {
                    MessageBox.Show($"记录已开始，但未能设置登录自启：{error.Message}", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            await window.StartRefreshAsync();
            if (!startup || pendingOpen) ShowMainWindow();
        }
        catch (Exception error)
        {
            MessageBox.Show($"无法开始记录：{error.Message}\n请检查本地数据目录是否可写。", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Error);
            await ExitAsync();
        }
    }

    private void ShowMainWindow()
    {
        if (exiting) return;
        if (MainWindow is not MainWindow window) { pendingOpen = true; return; }
        pendingOpen = false;
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private async Task<DayView> ReadDayAsync(DateOnly day)
    {
        try
        {
            var view = await runtime!.ReadDayAsync(day);
            tray?.Update(view.Status, manualPaused);
            return view;
        }
        catch
        {
            tray?.Update("记录异常", manualPaused);
            throw;
        }
    }

    private async Task ChangeManualPauseAsync(bool paused)
    {
        if (exiting) return;
        manualPaused = paused;
        (MainWindow as MainWindow)?.SetPauseState(paused);
        try { await ApplyManualPause(paused); }
        catch (Exception) { /* The runtime exposes saving faults in the next status refresh. */ }
    }

    private async Task DeleteDayAsync(DateOnly day)
    {
        if (exiting || runtime is null) return;
        await runtime.DeleteDayAsync(day);
        try
        {
            var resumed = false;
            while (!resumed && !exiting)
            {
                long version;
                WindowsSessionState state;
                lock (controlGate)
                {
                    version = systemVersion;
                    state = sessionObserver!.ReadCurrentState();
                }
                await WithForegroundSnapshot(async (observation, at) =>
                {
                    Task<bool> save;
                    lock (controlGate)
                    {
                        if (exiting || version != systemVersion || state != sessionObserver.ReadCurrentState()) return;
                        var system = new RecordingConditions(false, state.Sleeping, state.Locked, state.DisplayOff, state.Available);
                        save = runtime.ResumeAfterDeleteAsync(observation, system, at);
                    }
                    resumed = await save;
                });
            }
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"删除已提交，但恢复记录失败：{error.Message}。已删内容不会恢复。", error);
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
        RecordingTime? stopAt;
        lock (controlGate)
        {
            if (exiting) return;
            stopAt = recordingClock?.Now();
            exiting = true;
            observationCutoff = stopAt?.MonotonicTicks ?? -1;
        }
        checkpoint?.Stop();
        (MainWindow as MainWindow)?.StopRefresh();
        sessionObserver?.Dispose();
        observer?.Dispose();
        lock (controlGate) observationCutoff = -1;
        try
        {
            if (runtime is not null && stopAt is RecordingTime at)
            {
                if (observer?.IsStopped == false || sessionObserver?.IsStopped == false)
                    runtime.MarkUnreliable(at, "观测停止未完成；未保存尾段不计入软件使用时间。");
                var warning = await runtime.FinishAsync(at);
                if (warning is not null)
                    MessageBox.Show($"退出前记录存在异常：{warning}\n无法确认的范围保留为未记录。", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception error) { MessageBox.Show($"最后一段未能保存：{error.Message}", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Warning); }
        runtime?.Dispose();
        tray?.Dispose();
        singleInstance?.Dispose();
        Shutdown();
    }
}
