using System.Windows;
using System.Windows.Threading;

namespace FocusOn;

public partial class App : Application
{
    private RecordingRuntime? runtime;
    private WindowsForegroundObserver? observer;
    private DispatcherTimer? checkpoint;
    private bool exiting;

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
            observer.Start();
            checkpoint = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            checkpoint.Tick += (_, _) => observer.Poll();
            checkpoint.Start();
            var window = new MainWindow(runtime.ReadDayAsync);
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

    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        checkpoint?.Stop();
        (MainWindow as MainWindow)?.StopRefresh();
        observer?.Dispose();
        try { if (runtime is not null) await runtime.FinishAsync(); }
        catch (Exception error) { MessageBox.Show($"最后一段未能保存：{error.Message}", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Warning); }
        runtime?.Dispose();
        Shutdown();
    }
}
