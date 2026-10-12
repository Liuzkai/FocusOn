using Avalonia;

namespace FocusOn;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        SingleInstance? instance = null;
        try
        {
            var (directory, _, _) = PlatformPaths.ParseArguments(args);
            instance = new SingleInstance(directory);
            // A duplicate must activate and leave before opening any native GUI resources.
            if (!instance.IsPrimary) { instance.SignalExisting(); return 0; }
            return DesktopRendering.Configure(AppBuilder.Configure(() => new App(instance))
                .UsePlatformDetect().WithInterFont().LogToTrace()).StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error) { Console.Error.WriteLine("FocusOn: " + error.Message); return 1; }
        finally { instance?.Dispose(); }
    }

    public static AppBuilder BuildAvaloniaApp() => DesktopRendering.Configure(AppBuilder.Configure<App>()
        .UsePlatformDetect().WithInterFont().LogToTrace());
}
