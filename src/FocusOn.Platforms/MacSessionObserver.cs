using System.Runtime.InteropServices;

namespace FocusOn;

public sealed class MacSessionObserver : ISessionObserver
{
    private readonly Action<SessionState> changed;
    private readonly PowerCallback powerCallback;
    private readonly ManualResetEventSlim stopping = new();
    private readonly object gate = new();
    private SessionState state = new(false, false, false, false);
    private Thread? worker;
    private uint power;
    private bool sleeping;
    private bool disposed;

    public MacSessionObserver(Action<SessionState> changed) { this.changed = changed; powerCallback = PowerChanged; }
    public void Start()
    {
        if (worker is not null) throw new InvalidOperationException("会话观测已经启动。");
        worker = new Thread(Run) { IsBackground = true, Name = "FocusOn macOS power events" };
        worker.Start();
    }

    private void Run()
    {
        nint port = 0, mode = 0;
        uint notifier = 0;
        try
        {
            power = IORegisterForSystemPower(0, out port, powerCallback, out notifier);
            if (power == 0) throw new InvalidOperationException("macOS 电源通知注册失败。");
            mode = MacNative.String("kCFRunLoopDefaultMode");
            CFRunLoopAddSource(CFRunLoopGetCurrent(), IONotificationPortGetRunLoopSource(port), mode);
            while (!stopping.IsSet)
            {
                Publish(ReadState());
                CFRunLoopRunInMode(mode, 0.5, true);
            }
        }
        catch (Exception error) { Publish(new(false, false, false, false, "无法确认 macOS 会话状态：" + error.Message)); }
        finally
        {
            if (notifier != 0) IODeregisterForSystemPower(ref notifier);
            if (power != 0) IOServiceClose(power);
            if (port != 0) IONotificationPortDestroy(port);
            if (mode != 0) MacNative.CFRelease(mode);
        }
    }

    private SessionState ReadState()
    {
        var dictionary = MacNative.CGSessionCopyCurrentDictionary();
        if (dictionary == 0) return new(sleeping, false, false, false);
        try
        {
            var lockedValue = MacNative.DictionaryValue(dictionary, "CGSSessionScreenIsLocked");
            // Use the SDK macro's actual CFString value, not the macro's C name.
            var onConsole = MacNative.SessionOnConsoleValue(dictionary);
            var locked = lockedValue != 0 && MacNative.CFBooleanGetValue(lockedValue);
            var available = onConsole != 0 && MacNative.CFBooleanGetValue(onConsole);
            if (MacNative.CGGetOnlineDisplayList(0, null, out var count) != 0 || count is 0 or > 256)
                return new(sleeping, locked, false, false, "无法确认 macOS 屏幕状态。");
            var displays = new uint[count];
            if (MacNative.CGGetOnlineDisplayList(count, displays, out var actual) != 0 || actual == 0 || actual > count)
                return new(sleeping, locked, false, false, "无法确认 macOS 屏幕状态。");
            // Turning off one of several monitors must not stop the whole user session.
            var displayOff = displays.Take((int)actual).All(id => MacNative.CGDisplayIsAsleep(id) != 0);
            return new(sleeping, locked, displayOff, available);
        }
        finally { MacNative.CFRelease(dictionary); }
    }

    private void PowerChanged(nint reference, uint service, uint message, nint argument)
    {
        try
        {
            if (message == 0xe0000280) { sleeping = true; Publish(ReadCurrentState() with { Sleeping = true }); }
            if (message == 0xe0000300) { sleeping = false; Publish(ReadState()); }
        }
        catch (Exception error) { try { Publish(new(sleeping, false, false, false, error.Message)); } catch { } }
        finally
        {
            if (message is 0xe0000270 or 0xe0000280) IOAllowPowerChange(power, argument);
        }
    }

    private void Publish(SessionState next)
    {
        lock (gate) { if (next == state) return; state = next; }
        if (!stopping.IsSet) changed(next);
    }
    public SessionState ReadCurrentState() { lock (gate) return state; }
    public bool IsStopped => worker is null || !worker.IsAlive;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping.Set();
        if (worker is not null && Thread.CurrentThread != worker) worker.Join();
        stopping.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void PowerCallback(nint reference, uint service, uint message, nint argument);
    [DllImport(MacNative.IOKit)] private static extern uint IORegisterForSystemPower(nint reference, out nint port, PowerCallback callback, out uint notifier);
    [DllImport(MacNative.IOKit)] private static extern nint IONotificationPortGetRunLoopSource(nint port);
    [DllImport(MacNative.IOKit)] private static extern void IONotificationPortDestroy(nint port);
    [DllImport(MacNative.IOKit)] private static extern int IODeregisterForSystemPower(ref uint notifier);
    [DllImport(MacNative.IOKit)] private static extern int IOServiceClose(uint connection);
    [DllImport(MacNative.IOKit)] private static extern int IOAllowPowerChange(uint connection, nint notification);
    [DllImport(MacNative.CoreFoundation)] private static extern nint CFRunLoopGetCurrent();
    [DllImport(MacNative.CoreFoundation)] private static extern void CFRunLoopAddSource(nint runLoop, nint source, nint mode);
    [DllImport(MacNative.CoreFoundation)] private static extern int CFRunLoopRunInMode(nint mode, double seconds, [MarshalAs(UnmanagedType.I1)] bool returnAfterSourceHandled);
}
