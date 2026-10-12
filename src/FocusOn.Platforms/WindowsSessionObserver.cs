using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FocusOn;

public sealed class WindowsSessionObserver : ISessionObserver
{
    private static readonly Guid SessionDisplayStatus = new("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    private readonly Action<SessionState> onState;
    private readonly WindowProcedure windowProcedure;
    private readonly ManualResetEventSlim started = new();
    private readonly object gate = new();
    private SessionState state = new(false, false, false, false);
    private Thread? thread;
    private Exception? startupError;
    private uint threadId;
    private bool sleeping;
    private bool locked;
    private bool displayOff;
    private bool lockKnown;
    private bool displayKnown;
    private bool sessionConnected;
    private bool notificationsReady;
    private volatile bool disposed;

    public WindowsSessionObserver(Action<SessionState> onState)
    {
        this.onState = onState;
        windowProcedure = HandleMessage;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread is not null) throw new InvalidOperationException("Session observation has already started.");
        thread = new Thread(Run) { IsBackground = true, Name = "FocusOn session events" };
        thread.Start();
        started.Wait();
        if (startupError is not null) throw new InvalidOperationException("无法启动系统状态观测。", startupError);
    }

    public SessionState ReadCurrentState()
    {
        lock (gate) return state;
    }

    private void Run()
    {
        string className = "FocusOnSessionObserver-" + Guid.NewGuid().ToString("N");
        nint instance = GetModuleHandle(null);
        nint window = 0;
        nint powerRegistration = 0;
        nint suspendRegistration = 0;
        bool sessionRegistered = false;
        bool classRegistered = false;
        var procedureRoot = GCHandle.Alloc(windowProcedure);
        try
        {
            threadId = GetCurrentThreadId();
            PeekMessage(out _, 0, 0, 0, 0);
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(windowProcedure),
                Instance = instance,
                ClassName = className
            };
            if (RegisterClassEx(ref windowClass) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            classRegistered = true;
            // An invisible top-level window receives broadcasts; HWND_MESSAGE does not.
            window = CreateWindowEx(0x80, className, "FocusOn session observer", 0, 0, 0, 0, 0, 0, 0, instance, 0);
            if (window == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!WTSRegisterSessionNotification(window, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            sessionRegistered = true;
            RefreshLockState();
            var displayGuid = SessionDisplayStatus;
            powerRegistration = RegisterPowerSettingNotification(window, ref displayGuid, 0);
            if (powerRegistration == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            suspendRegistration = RegisterSuspendResumeNotification(window, 0);
            if (suspendRegistration == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            notificationsReady = true;
            Publish(force: true);
            started.Set();
            while (!disposed)
            {
                while (!disposed && PeekMessage(out var message, 0, 0, 0, 1))
                {
                    if (message.Message == 0x0012) return;
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
                if (!disposed && MsgWaitForMultipleObjectsEx(0, 0, 250, 0x04ff, 0x0004) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception error)
        {
            notificationsReady = false;
            if (!started.IsSet) startupError = error;
            else if (!disposed) Publish();
        }
        finally
        {
            if (suspendRegistration != 0) UnregisterSuspendResumeNotification(suspendRegistration);
            if (powerRegistration != 0) UnregisterPowerSettingNotification(powerRegistration);
            if (sessionRegistered) WTSUnRegisterSessionNotification(window);
            if (window != 0) DestroyWindow(window);
            if (classRegistered) UnregisterClass(className, instance);
            procedureRoot.Free();
            started.Set();
        }
    }

    private nint HandleMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        if (!disposed)
        {
            try
            {
                if (message == 0x02b1)
                {
                    RefreshLockState();
                    switch ((uint)wParam)
                    {
                        case 7: locked = true; lockKnown = true; break;
                        case 8: locked = false; lockKnown = true; break;
                    }
                    Publish();
                    return 0;
                }
                if (message == 0x0218)
                {
                    switch ((uint)wParam)
                    {
                        case 4:
                            sleeping = true;
                            break;
                        case 7:
                        case 18:
                            sleeping = false;
                            RefreshLockState();
                            break;
                        case 0x8013:
                            if (lParam != 0 && Marshal.PtrToStructure<Guid>(lParam) == SessionDisplayStatus && Marshal.ReadInt32(lParam, 16) == 4)
                            {
                                int display = Marshal.ReadInt32(lParam, 20);
                                displayKnown = display is 0 or 1 or 2;
                                if (displayKnown) displayOff = display == 0;
                            }
                            break;
                    }
                    Publish();
                    return 1;
                }
            }
            catch
            {
                // Neither a failed state read nor consumer error may escape native WindowProc.
                notificationsReady = false;
                try { Publish(); }
                catch { }
            }
        }
        return DefWindowProc(window, message, wParam, lParam);
    }

    private void RefreshLockState()
    {
        lockKnown = false;
        sessionConnected = false;
        if (!WTSQuerySessionInformation(0, -1, 25, out nint information, out uint bytes)) return;
        try
        {
            if (bytes < Marshal.SizeOf<SessionInfoPrefix>()) return;
            var session = Marshal.PtrToStructure<SessionInfoPrefix>(information);
            if (session.Level != 1 || session.SessionFlags is not (0 or 1)) return;
            locked = session.SessionFlags == 0;
            lockKnown = true;
            sessionConnected = session.SessionState == 0;
        }
        finally { WTSFreeMemory(information); }
    }

    private void Publish(bool force = false)
    {
        var next = new SessionState(sleeping, locked, displayOff,
            notificationsReady && lockKnown && displayKnown && sessionConnected);
        lock (gate)
        {
            if (!force && state == next) return;
            state = next;
        }
        if (!disposed) onState(next);
    }

    public bool IsStopped => thread is null || !thread.IsAlive;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (thread is not null)
        {
            started.Wait();
            if (thread.IsAlive) PostThreadMessage(threadId, 0x0012, 0, 0);
            if (Thread.CurrentThread == thread || !thread.Join(TimeSpan.FromSeconds(5))) return;
        }
        started.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string? ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MessageData
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    // Windows x64 WTSINFOEXW.Data is aligned to 8 by its LARGE_INTEGER members.
    // Read only the state prefix; the returned buffer's user/domain strings are unused.
    [StructLayout(LayoutKind.Explicit, Size = 20)]
    private struct SessionInfoPrefix
    {
        [FieldOffset(0)] public uint Level;
        [FieldOffset(12)] public int SessionState;
        [FieldOffset(16)] public int SessionFlags;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] private static extern nint DefWindowProc(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClass(string className, nint instance);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint RegisterPowerSettingNotification(nint window, ref Guid setting, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterPowerSettingNotification(nint registration);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint RegisterSuspendResumeNotification(nint window, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterSuspendResumeNotification(nint registration);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out MessageData message, nint window, uint min, uint max, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref MessageData message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern nint DispatchMessage(ref MessageData message);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint timeout, uint wakeMask, uint flags);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? module);
    [DllImport("wtsapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSRegisterSessionNotification(nint window, uint flags);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSUnRegisterSessionNotification(nint window);
    [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSQuerySessionInformation(nint server, int sessionId, int infoClass, out nint information, out uint bytes);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(nint information);
}
