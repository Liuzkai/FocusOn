using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FocusOn;

public sealed class WindowsForegroundObserver : IDisposable
{
    private const uint ForegroundEvent = 0x0003;
    private const uint NameChangeEvent = 0x800c;
    private const uint PollMessage = 0x8001;
    private const uint QuitMessage = 0x0012;
    private readonly RecordingClock clock;
    private readonly Action<ForegroundObservation?, RecordingTime> onObservation;
    private readonly Action<RecordingTime, string> onFailure;
    private readonly WinEventCallback callback;
    private readonly Queue<(nint Window, RecordingTime Time, Action<ForegroundObservation?, RecordingTime> Consumer)> pending = new();
    private readonly ConcurrentQueue<Action<ForegroundObservation?, RecordingTime>> snapshots = new();
    private readonly ManualResetEventSlim started = new();
    private Thread? thread;
    private uint threadId;
    private Exception? startupError;
    private bool publishing;
    private volatile bool disposed;

    public WindowsForegroundObserver(RecordingClock clock, Action<ForegroundObservation?, RecordingTime> onObservation,
        Action<RecordingTime, string> onFailure)
    {
        this.clock = clock;
        this.onObservation = onObservation;
        this.onFailure = onFailure;
        callback = HandleEvent;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread is not null) throw new InvalidOperationException("Foreground observation has already started.");
        thread = new Thread(Run) { IsBackground = true, Name = "FocusOn foreground events" };
        thread.Start();
        started.Wait();
        if (startupError is not null) throw new InvalidOperationException("无法启动前台观测。", startupError);
    }

    public void Poll() => RequestSnapshot(onObservation);

    public void RequestSnapshot(Action<ForegroundObservation?, RecordingTime> consumer)
    {
        if (disposed) return;
        snapshots.Enqueue(consumer);
        if (thread is null || !thread.IsAlive || !PostThreadMessage(threadId, PollMessage, 0, 0))
            while (snapshots.TryDequeue(out var waiting)) waiting(null, clock.Now());
    }

    public ForegroundObservation? ReadCurrent() => ReadWindow(GetForegroundWindow());

    private void Run()
    {
        nint foregroundHook = 0;
        nint titleHook = 0;
        GCHandle callbackRoot = GCHandle.Alloc(callback);
        try
        {
            threadId = GetCurrentThreadId();
            PeekMessage(out _, 0, 0, 0, 0); // Create this thread's queue before Poll/Dispose can post to it.
            foregroundHook = SetWinEventHook(ForegroundEvent, ForegroundEvent, 0, callback, 0, 0, 0);
            if (foregroundHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            titleHook = SetWinEventHook(NameChangeEvent, NameChangeEvent, 0, callback, 0, 0, 0);
            if (titleHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            Publish(GetForegroundWindow(), clock.Now());
            started.Set();
            while (!disposed)
            {
                while (!disposed && PeekMessage(out var message, 0, 0, 0, 1))
                {
                    if (message.Message == QuitMessage) return;
                    if (message.Message == PollMessage)
                    {
                        if (snapshots.TryDequeue(out var consumer)) Publish(GetForegroundWindow(), clock.Now(), consumer);
                    }
                    else
                    {
                        TranslateMessage(ref message);
                        DispatchMessage(ref message);
                    }
                }
                // The timeout observes disposal even when posting WM_QUIT fails.
                if (!disposed && MsgWaitForMultipleObjectsEx(0, 0, 250, 0x04ff, 0x0004) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception error)
        {
            if (!started.IsSet) startupError = error;
            else if (!disposed) onFailure(clock.Now(), $"前台观测已中断：{error.Message}");
        }
        finally
        {
            // Windows requires unhooking on the thread that installed each hook.
            if (titleHook != 0) UnhookWinEvent(titleHook);
            if (foregroundHook != 0) UnhookWinEvent(foregroundHook);
            callbackRoot.Free();
            started.Set();
        }
    }

    private void HandleEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (disposed) return;
        var time = clock.Now();
        if (eventType == NameChangeEvent && (objectId != 0 || childId != 0 || window != GetForegroundWindow())) return;
        try { Publish(window, time); }
        catch (Exception error)
        {
            // Managed exceptions must never escape a native callback.
            try { onFailure(time, $"前台观测失败：{error.Message}"); }
            catch { }
        }
    }

    private void Publish(nint window, RecordingTime time, Action<ForegroundObservation?, RecordingTime>? consumer = null)
    {
        pending.Enqueue((window, time, consumer ?? onObservation));
        if (publishing) return;
        publishing = true;
        try
        {
            while (pending.TryDequeue(out var item))
                if (!disposed) item.Consumer(ReadWindow(item.Window), item.Time);
        }
        finally { publishing = false; }
    }

    private static ForegroundObservation? ReadWindow(nint window)
    {
        if (window == 0 || !IsWindow(window)) return null;
        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0) return null;
        string? key = null;
        string? name = null;
        nint process = OpenProcess(0x1000, false, processId);
        if (process != 0)
        {
            try
            {
                var path = new StringBuilder(32768);
                uint size = (uint)path.Capacity;
                if (QueryFullProcessImageName(process, 0, path, ref size))
                {
                    string fullPath = Path.GetFullPath(path.ToString());
                    name = Path.GetFileNameWithoutExtension(fullPath);
                    key = "exe:" + fullPath.ToUpperInvariant();
                }
            }
            catch (ArgumentException) { }
            finally { CloseHandle(process); }
        }
        if (key is null)
        {
            try
            {
                using var target = Process.GetProcessById((int)processId);
                name = target.ProcessName;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException) { }
        }
        if (name is not null && (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("RuntimeBroker", StringComparison.OrdinalIgnoreCase) ||
                                name.Equals("dllhost", StringComparison.OrdinalIgnoreCase)))
            (key, name) = (null, null);
        string? title = ReadTitle(window, processId == (uint)Environment.ProcessId);
        GetWindowThreadProcessId(window, out uint stillProcessId);
        if (!IsWindow(window) || stillProcessId != processId) return null;
        return new ForegroundObservation(window.ToInt64(), key, name, title, processId);
    }

    private static string? ReadTitle(nint window, bool ownProcess)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int length;
            if (ownProcess)
            {
                if (SendMessageTimeout(window, 0x000e, 0, 0, 0x23, 100, out nuint result) == 0) return null;
                length = checked((int)result);
            }
            else length = GetWindowTextLength(window);
            if (length <= 0 || length > 32767) return null;
            var title = new StringBuilder(length + 2);
            int copied;
            if (ownProcess)
            {
                // A busy FocusOn UI must not block the observer or its shutdown.
                if (SendTextTimeout(window, 0x000d, (nuint)title.Capacity, title, 0x23, 100, out nuint result) == 0) return null;
                copied = checked((int)result);
            }
            else copied = GetWindowText(window, title, title.Capacity);
            if (copied == 0) return null;
            if (copied < title.Capacity - 1) return title.ToString();
        }
        return null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (thread is not null)
        {
            started.Wait();
            if (thread.IsAlive) PostThreadMessage(threadId, QuitMessage, 0, 0);
            if (Thread.CurrentThread == thread) return;
            if (!thread.Join(TimeSpan.FromSeconds(5))) return;
        }
        started.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventCallback(nint hook, uint eventType, nint window, int objectId, int childId, uint eventThread, uint eventTime);

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

    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint window);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)] private static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", CharSet = CharSet.Unicode)] private static extern nint SendTextTimeout(nint window, uint message, nuint wParam, StringBuilder text, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint handles, uint timeout, uint wakeMask, uint flags);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out MessageData message, nint window, uint min, uint max, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref MessageData message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern nint DispatchMessage(ref MessageData message);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
