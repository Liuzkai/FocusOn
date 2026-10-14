using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Globalization;
using System.Diagnostics;
using Avalonia.Styling;

namespace FocusOn;

public partial class App : Application
{
    public App() { }
    public App(SingleInstance instance) { singleInstance = instance; }
    private RecordingController? controller;
    private SingleInstance? singleInstance;
    private TrayController? tray;
    private WindowResidency? residency;
    private IActivatableLifetime? activation;
    private MainWindow? window;
    private IClassicDesktopStyleApplicationLifetime? desktop;
    private readonly DispatcherTimer checkpoint = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool exiting, pendingOpen;

    public override void Initialize()
    {
        Localizer.SetLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en");
        AvaloniaXamlLoader.Load(this);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime) return;
        desktop = lifetime; desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        activation = this.TryGetFeature<IActivatableLifetime>();
        if (activation is not null) activation.Activated += OnActivated;
        desktop.ShutdownRequested += async (_, e) => { if (exiting) return; e.Cancel = true; await ExitAsync(); };
        _ = StartAsync(lifetime.Args ?? []);
    }

    private async Task StartAsync(string[] args)
    {
        try
        {
            var (directory, isolated, startup) = PlatformPaths.ParseArguments(args);
            Localizer.SetLanguage(LanguagePreference.Load(directory));
            singleInstance ??= new SingleInstance(directory);
            if (!singleInstance.IsPrimary) { singleInstance.SignalExisting(); singleInstance.Dispose(); singleInstance = null; desktop!.Shutdown(); return; }
            var preferences = AppPreferences.Load(directory);
            RequestedThemeVariant = preferences.Theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark;
            ReviewTheme.Apply(Resources, false, preferences.Theme);
            ReviewTheme.LoadApplicationColors(directory);
            singleInstance.Listen(() => Dispatcher.UIThread.Post(ShowMainWindow));
            controller = new RecordingController(directory);
            window = new MainWindow(controller.ReadDayAsync, controller.DeleteDayAsync, controller.ChangeTimerAsync,
                language => LanguagePreference.Save(directory, language), readReview: controller.ReadReviewAsync, readStatistics: controller.ReadStatisticsAsync);
            window.ConfigureSettings(directory, isolated, preferences, next =>
            {
                if (!isolated && next.StartAtLogin != preferences.StartAtLogin)
                {
                    if (next.StartAtLogin) StartupRegistration.Ensure(Environment.ProcessPath!); else StartupRegistration.Disable();
                }
                try { next.Save(directory); }
                catch
                {
                    if (!isolated && next.StartAtLogin != preferences.StartAtLogin)
                    { if (preferences.StartAtLogin) StartupRegistration.Ensure(Environment.ProcessPath!); else StartupRegistration.Disable(); }
                    throw;
                }
                preferences = next;
            }, target => RelocateAsync(directory, target, isolated), installer => SaveAndLaunchAsync(isolated ? directory : null, installer, null));
            window.LanguageChanged += () => tray?.RefreshLanguage();
            desktop!.MainWindow = window;
            using (var resource = AssetLoader.Open(new Uri("avares://FocusOn/Assets/focuson.png"))) window.Icon = new WindowIcon(resource);
            window.PauseRequested += async (_, _) => await SetPausedAsync(!controller.ManualPaused);
            window.ExitRequested += async (_, _) => await ExitAsync();
            window.DayRefreshed += view => tray?.Update(view.Status, controller.ManualPaused);
            residency = new WindowResidency(window, window.SetTrayAvailable);
            tray = new TrayController(ShowMainWindow, () => _ = SetPausedAsync(true), () => _ = SetPausedAsync(false), () => _ = ExitAsync());
            tray.AvailabilityChanged += residency.SetTrayAvailable;
            await tray.CheckAvailabilityAsync();
            residency.SetTrayAvailable(tray.IsAvailable);
            await controller.StartAsync();
            checkpoint.Tick += (_, _) => controller.Poll(); checkpoint.Start();
            if (!isolated && !startup)
            {
                try { if (preferences.StartAtLogin) StartupRegistration.Ensure(Environment.ProcessPath!); else StartupRegistration.Disable(); }
                catch (Exception error) { window.Show(); await MessageDialog.ShowAsync(window, "记录已启动，但未能设置登录自启：" + error.Message); }
            }
            await window.StartRefreshAsync();
            if (startup && !pendingOpen && tray.IsAvailable) window.Hide();
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

    private async Task RelocateAsync(string directory, string target, bool isolated)
    {
        if (exiting || window is null) return;
        using var migration = new DataDirectoryMigration(directory, target);
        if (!await MessageDialog.ConfirmAsync(window, Localizer.Get("保存并复制记录到：{0}。校验完成后重启，原目录保留为备份。独立计时将暂停。是否继续？", migration.Target), "保存并重启")) return;
        await SaveAndLaunchAsync(isolated ? directory : null, null, migration);
    }
    private async Task SaveAndLaunchAsync(string? isolatedDirectory, string? installer, DataDirectoryMigration? migration)
    {
        if (exiting || window is null) return;
        exiting = true; checkpoint.Stop(); window.StopRefresh(); window.IsEnabled = false;
        string? errorMessage = null;
        try
        {
            if (controller is not null) await controller.DisposeAsync();
            if (migration is not null)
            {
                await Task.Run(() => migration.CopyAndCommit(isolatedDirectory is null ? PlatformPaths.DataDirectory : null));
                if (isolatedDirectory is not null) isolatedDirectory = migration.Target;
            }
        }
        catch (Exception error)
        {
            errorMessage = Localizer.Get("操作失败，仍使用原数据目录：{0}", Localizer.Translate(error.Message));
            installer = null;
        }
        migration?.Dispose();
        residency?.BeginExit(); tray?.Dispose(); singleInstance?.Dispose();
        window.IsEnabled = true;
        if (errorMessage is not null) await MessageDialog.ShowAsync(window, errorMessage);
        try
        {
            var process = new ProcessStartInfo(installer ?? Environment.ProcessPath!) { UseShellExecute = installer is not null };
            if (installer is null)
            {
                if (Path.GetFileNameWithoutExtension(process.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) process.ArgumentList.Add(typeof(App).Assembly.Location);
                if (isolatedDirectory is not null) { process.ArgumentList.Add("--data-dir"); process.ArgumentList.Add(isolatedDirectory); }
            }
            using var launched = Process.Start(process) ?? throw new IOException("无法启动应用或安装程序。");
        }
        catch (Exception error) { await MessageDialog.ShowAsync(window, Localizer.Get("无法启动，请手动打开 FocusOn：{0}", error.Message)); }
        finally { residency?.Dispose(); desktop?.Shutdown(); }
    }

    private void OnActivated(object? sender, ActivatedEventArgs args)
    {
        if (args.Kind == ActivationKind.Reopen) ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (exiting) return;
        if (window is null) { pendingOpen = true; return; }
        pendingOpen = false; activation?.TryLeaveBackground();
        if (residency is not null) residency.Restore();
        else { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
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
        exiting = true; residency?.BeginExit(); checkpoint.Stop(); window?.StopRefresh();
        try { if (controller is not null) await controller.DisposeAsync(); }
        catch (Exception error) { if (window is not null) { window.Show(); await MessageDialog.ShowAsync(window, "退出前记录未能可靠保存：" + error.Message); } }
        finally
        {
            if (activation is not null) activation.Activated -= OnActivated;
            tray?.Dispose(); residency?.Dispose(); singleInstance?.Dispose(); desktop?.Shutdown();
        }
    }
}
