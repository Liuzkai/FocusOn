using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using FocusOn;
using System.Text.Json;

DesktopRendering.Configure(AppBuilder.Configure<SmokeApp>().UsePlatformDetect().WithInterFont().LogToTrace()).StartWithClassicDesktopLifetime(args);
return SmokeApp.Result;

sealed class SmokeApp : Application
{
    public static int Result { get; private set; } = 1;
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://FocusOn/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _ = CheckAsync(desktop);
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private async Task CheckAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-desktop-smoke-" + Guid.NewGuid().ToString("N"));
        RecordingController? recording = null; TrayController? tray = null; WindowResidency? residency = null; SingleInstance? instance = null;
        MainWindow? window = null;
        try
        {
            Localizer.SetLanguage("en");
            instance = new SingleInstance(directory); Require(instance.IsPrimary, "Primary writer lock unavailable.");
            recording = new RecordingController(directory); await recording.StartAsync();
            window = new MainWindow(recording.ReadDayAsync, recording.DeleteDayAsync, recording.ChangeTimerAsync, language => LanguagePreference.Save(directory, language));
            desktop.MainWindow = window;
            residency = new WindowResidency(window, window.SetTrayAvailable);
            tray = new TrayController(residency.Restore, () => { }, () => { }, () => { });
            tray.AvailabilityChanged += residency.SetTrayAvailable;
            window.Show(); await window.StartRefreshAsync();
            await recording.ChangeTimerAsync(TimerAction.Start, "Native desktop timer");
            for (var attempt = 0; attempt < 15 && !tray.IsAvailable; attempt++) { await tray.CheckAvailabilityAsync(); await Task.Delay(200); }
            var expectTray = !OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("FOCUSON_EXPECT_TRAY") == "1";
            Require(!expectTray || tray.IsAvailable, "Native tray was expected but could not be confirmed.");
            residency.SetTrayAvailable(tray.IsAvailable);
            window.Close(); Require(window.IsVisible != tray.IsAvailable, "Close residency did not match real tray availability.");
            Require(!recording.ManualPaused, "Close paused recording.");
            using (var duplicate = new SingleInstance(directory)) Require(!duplicate.IsPrimary, "Close released writer lock.");
            await Task.Delay(450);
            var day = await recording.ReadDayAsync(DateOnly.FromDateTime(DateTime.Today));
            Require(day.Timer is { IsRunning: true } && day.Timer.Duration > TimeSpan.Zero, "Timer did not run while resident.");
            residency.Restore(); Require(window.IsVisible, "Tray restore did not show the same window.");
            if (Environment.GetEnvironmentVariable("FOCUSON_TRAY_LOSS_FILE") is { Length: > 0 } signal)
            {
                window.Close(); Require(!window.IsVisible, "Tray close did not hide.");
                File.WriteAllText(signal, "stop");
                for (var attempt = 0; attempt < 30 && tray.IsAvailable; attempt++) { await Task.Delay(200); await tray.CheckAvailabilityAsync(); }
                Require(!tray.IsAvailable && window.IsVisible, "Loss of tray host stranded hidden window.");
            }
            window.FindControl<Avalonia.Controls.ComboBox>("LanguagePicker")!.SelectedIndex = 0;
            Require(LanguagePreference.Load(directory) == "zh-CN", "Native language preference not persisted.");
            window.FindControl<Avalonia.Controls.ComboBox>("LanguagePicker")!.SelectedIndex = 1;
            Require(LanguagePreference.Load(directory) == "en", "English preference not persisted.");
            var platformWarning = day.Warning;
            var trustedMac = OperatingSystem.IsMacOS() ? MacPermissions.IsAccessibilityTrusted : (bool?)null;
            if (OperatingSystem.IsMacOS())
            {
                var clock = PlatformClock.Create(); var before = clock.Now(); await Task.Delay(50); var after = clock.Now();
                Require(after.MonotonicTicks > before.MonotonicTicks, "Mac native continuous clock did not advance.");
                using var session = new MacSessionObserver(_ => { }); session.Start(); await Task.Delay(800);
                Require(session.ReadCurrentState().Available, "Mac native session/display query unavailable: " + session.ReadCurrentState().Warning);
            }
            residency.BeginExit(); window.StopRefresh(); await recording.DisposeAsync(); recording = null;
            tray.Dispose(); tray = null; instance.Dispose(); instance = null;
            window.Close();
            using var reopened = new RecordingSession(Path.Combine(directory, "focuson.db"));
            var saved = reopened.GetDay(DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local);
            Require(saved.Timer is { IsRunning: false } && saved.Timer.Duration > TimeSpan.Zero, "Exit failed to save/pause timer.");
            using var nextInstance = new SingleInstance(directory); Require(nextInstance.IsPrimary, "Exit leaked the writer lock.");
            Console.WriteLine(JsonSerializer.Serialize(new { result = "PASS", nativeRuntime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                trayConfirmed = expectTray, macAccessibilityTrusted = trustedMac, platformWarning,
                checks = new[] { "native window", "close residency", "restore", "running timer", "single writer", "persisted languages", "exit/reopen SQLite", "resource release" } }));
            Result = 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); }
        finally
        {
            window?.StopRefresh(); residency?.BeginExit();
            if (recording is not null) await recording.DisposeAsync();
            tray?.Dispose(); residency?.Dispose(); instance?.Dispose(); window?.Close();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            desktop.Shutdown(Result);
        }
    }
}
