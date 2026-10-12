using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FocusOn;

if (args is ["--second-instance", var lockDirectory])
{
    using var duplicate = new SingleInstance(lockDirectory);
    if (duplicate.IsPrimary) throw new Exception("Second process acquired the writer lock.");
    duplicate.SignalExisting();
    Console.WriteLine("SECONDARY_ACTIVATED");
    return;
}

if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Run this native X11 check on Linux with Xvfb or an isolated X11 desktop.");
var directory = Path.Combine(Path.GetTempPath(), "FocusOn-native-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    using (var primary = new SingleInstance(directory))
    {
        if (!primary.IsPrimary) throw new Exception("Primary could not acquire the writer lock.");
        using var activated = new ManualResetEventSlim(); primary.Listen(activated.Set);
        var process = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") process.ArgumentList.Add(typeof(Program).Assembly.Location);
        process.ArgumentList.Add("--second-instance"); process.ArgumentList.Add(directory);
        using var duplicate = Process.Start(process)!;
        var output = await duplicate.StandardOutput.ReadToEndAsync(); var error = await duplicate.StandardError.ReadToEndAsync();
        await duplicate.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (duplicate.ExitCode != 0 || !output.Contains("SECONDARY_ACTIVATED") || !activated.Wait(TimeSpan.FromSeconds(5)))
            throw new Exception("Cross-process activation failed: " + error);
    }
    Console.WriteLine("PASS: cross-process writer exclusion and activation");

    using var windows = new NativeWindows();
    var clock = PlatformClock.Create();
    var database = Path.Combine(directory, "focuson.db");
    using var runtime = new RecordingRuntime(database, clock); await runtime.Initialized;
    var awaited = new ConcurrentDictionary<string, TaskCompletionSource<ForegroundObservation>>();
    string? failure = null;
    using (var observer = new LinuxForegroundObserver(clock, (observation, at) =>
    {
        runtime.Observe(observation, at);
        if (observation?.WindowTitle is { } title && awaited.TryGetValue(title, out var found)) found.TrySetResult(observation);
    }, (_, reason) => failure = reason))
    {
        observer.Start();
        async Task SetWindow(nuint window, string title)
        {
            var completion = awaited[title] = new(TaskCreationOptions.RunContinuationsAsynchronously);
            windows.Activate(window, title);
            var observed = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (observed.WindowId != (long)window || observed.ProcessId != (uint)Environment.ProcessId || string.IsNullOrWhiteSpace(observed.AppKey))
                throw new Exception("Native foreground identity does not match the actual X11 window.");
            await Task.Delay(350);
        }
        await SetWindow(windows.First, "FocusOn 原生窗口一");
        await SetWindow(windows.First, "FocusOn 标题变化");
        await SetWindow(windows.Second, "FocusOn 原生窗口二");
        observer.Dispose();
        await runtime.FinishAsync(clock.Now());
        var view = await runtime.ReadDayAsync(DateOnly.FromDateTime(DateTime.Today));
        var titles = view.Intervals.Where(x => x.Kind == IntervalKind.Usage).Select(x => x.WindowTitle).ToArray();
        if (!new[] { "FocusOn 原生窗口一", "FocusOn 标题变化", "FocusOn 原生窗口二" }.SequenceEqual(titles))
            throw new Exception("Actual window/title changes were not saved in order: " + string.Join(", ", titles));
        if (view.Totals.Count != 1 || view.Totals[0].Duration <= TimeSpan.Zero || failure is not null)
            throw new Exception("Native capture produced no trusted usage duration: " + failure);
        windows.ActivateMissing();
        using var staleReader = new X11ForegroundReader();
        if (staleReader.Read() is not null) throw new Exception("A stale EWMH window ID must not count as unknown software usage.");
    }
    runtime.Dispose();
    using var reopened = new RecordingSession(database);
    var history = reopened.GetDay(DateOnly.FromDateTime(DateTime.Today), TimeZoneInfo.Local);
    if (history.Intervals.Count(x => x.Kind == IntervalKind.Usage) != 3) throw new Exception("Native history did not survive reopening SQLite.");
    Console.WriteLine("PASS: X11 active windows, Unicode title change, executable identity, stale-window rejection, SQLite save and reopen (3 usage segments)");
}
finally { Directory.Delete(directory, recursive: true); }

sealed class NativeWindows : IDisposable
{
    private readonly nint display;
    private readonly nuint root;
    public nuint First { get; }
    public nuint Second { get; }
    public NativeWindows()
    {
        XInitThreads(); display = XOpenDisplay(null);
        if (display == 0) throw new Exception("X11 test display is not reachable.");
        root = XDefaultRootWindow(display);
        First = XCreateSimpleWindow(display, root, 10, 10, 320, 160, 0, 0, 0x292929);
        Second = XCreateSimpleWindow(display, root, 350, 10, 320, 160, 0, 0, 0x303030);
        foreach (var window in new[] { First, Second })
        {
            XMapWindow(display, window);
            XChangeNumber(display, window, XInternAtom(display, "_NET_WM_PID", false), 6, 32, 0, [(nuint)Environment.ProcessId], 1);
        }
        XSync(display, false);
    }
    public void Activate(nuint window, string title)
    {
        var bytes = Encoding.UTF8.GetBytes(title);
        XChangeText(display, window, XInternAtom(display, "_NET_WM_NAME", false), XInternAtom(display, "UTF8_STRING", false), 8, 0, bytes, bytes.Length);
        XChangeNumber(display, root, XInternAtom(display, "_NET_ACTIVE_WINDOW", false), 33, 32, 0, [window], 1);
        XSetInputFocus(display, window, 1, 0); XSync(display, false);
    }
    public void ActivateMissing()
    {
        XChangeNumber(display, root, XInternAtom(display, "_NET_ACTIVE_WINDOW", false), 33, 32, 0, [0xffffff], 1); XSync(display, false);
    }
    public void Dispose()
    {
        XDeleteProperty(display, root, XInternAtom(display, "_NET_ACTIVE_WINDOW", false));
        XDestroyWindow(display, First); XDestroyWindow(display, Second); XCloseDisplay(display);
    }
    [DllImport("libX11.so.6")] private static extern int XInitThreads();
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? display);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern nuint XCreateSimpleWindow(nint display, nuint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
    [DllImport("libX11.so.6")] private static extern int XMapWindow(nint display, nuint window);
    [DllImport("libX11.so.6")] private static extern nuint XInternAtom(nint display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);
    [DllImport("libX11.so.6", EntryPoint = "XChangeProperty")] private static extern int XChangeNumber(nint display, nuint window, nuint property, nuint type, int format, int mode, nuint[] data, int count);
    [DllImport("libX11.so.6", EntryPoint = "XChangeProperty")] private static extern int XChangeText(nint display, nuint window, nuint property, nuint type, int format, int mode, byte[] data, int count);
    [DllImport("libX11.so.6")] private static extern int XSetInputFocus(nint display, nuint window, int revert, nuint time);
    [DllImport("libX11.so.6")] private static extern int XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);
    [DllImport("libX11.so.6")] private static extern int XDeleteProperty(nint display, nuint window, nuint property);
    [DllImport("libX11.so.6")] private static extern int XDestroyWindow(nint display, nuint window);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
}
