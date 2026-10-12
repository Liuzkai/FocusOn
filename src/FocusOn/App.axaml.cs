using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

namespace FocusOn;

public partial class App : Application
{
    private RecordingController? controller;
    private SingleInstance? singleInstance;
    private TrayController? tray;
    private MainWindow? window;
    private IClassicDesktopStyleApplicationLifetime? desktop;
    private readonly DispatcherTimer checkpoint = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool exiting, pendingOpen;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime) return;
        desktop = lifetime; desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        desktop.ShutdownRequested += async (_, e) => { if (exiting) return; e.Cancel = true; await ExitAsync(); };
        _ = StartAsync(lifetime.Args ?? []);
    }

    private async Task StartAsync(string[] args)
    {
        try
        {
            var (directory, isolated, startup) = PlatformPaths.ParseArguments(args);
            singleInstance = new SingleInstance(directory);
            if (!singleInstance.IsPrimary) { singleInstance.SignalExisting(); singleInstance.Dispose(); singleInstance = null; desktop!.Shutdown(); return; }
            singleInstance.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));
            controller = new RecordingController(directory);
            window = new MainWindow(controller.ReadDayAsync, controller.DeleteDayAsync, controller.ChangeTimerAsync);
            desktop!.MainWindow = window;
            using (var resource = AssetLoader.Open(new Uri("avares://FocusOn/Assets/focuson.png"))) window.Icon = new WindowIcon(resource);
            window.PauseRequested += async (_, _) => await SetPausedAsync(!controller.ManualPaused);
            window.ExitRequested += async (_, _) => await ExitAsync();
            window.DayRefreshed += view => tray?.Update(view.Status, controller.ManualPaused);
            window.Closing += (_, e) =>
            {
                if (exiting) return;
                e.Cancel = true;
                if (OperatingSystem.IsLinux()) window.WindowState = WindowState.Minimized;
                else window.Hide();
            };
            tray = new TrayController(ShowMainWindow, () => _ = SetPausedAsync(true), () => _ = SetPausedAsync(false), () => _ = ExitAsync());
            await controller.StartAsync();
            checkpoint.Tick += (_, _) => controller.Poll(); checkpoint.Start();
            if (!isolated && !startup)
            {
                try { StartupRegistration.Ensure(Environment.ProcessPath!); }
                catch (Exception error) { window.Show(); await MessageDialog.ShowAsync(window, "记录已启动，但未能设置登录自启：" + error.Message); }
            }
            await window.StartRefreshAsync();
            if (startup && !pendingOpen)
            {
                if (OperatingSystem.IsLinux()) { window.WindowState = WindowState.Minimized; window.Show(); }
            }
            else ShowMainWindow();
        }
        catch (Exception error)
        {
            window ??= new MainWindow(_ => Task.FromException<DayView>(error), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
            desktop!.MainWindow = window; window.Show();
            await MessageDialog.ShowAsync(window, "无法开始记录：" + error.Message);
            await ExitAsync();
        }
    }

    private void ShowMainWindow()
    {
        if (exiting) return;
        if (window is null) { pendingOpen = true; return; }
        pendingOpen = false; window.Show(); window.WindowState = WindowState.Normal; window.Activate();
    }
    private async Task SetPausedAsync(bool paused)
    {
        if (controller is null || exiting) return;
        try { await controller.SetManualPausedAsync(paused); window?.SetPauseState(paused); await window!.RefreshAsync(); }
        catch (Exception error) { if (window is not null) window.ShowWarning("记录控制失败：" + error.Message); }
    }
    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true; checkpoint.Stop(); window?.StopRefresh();
        try { if (controller is not null) await controller.DisposeAsync(); }
        catch (Exception error) { if (window is not null) { window.Show(); await MessageDialog.ShowAsync(window, "退出前记录未能可靠保存：" + error.Message); } }
        tray?.Dispose(); singleInstance?.Dispose(); desktop?.Shutdown();
    }
}
