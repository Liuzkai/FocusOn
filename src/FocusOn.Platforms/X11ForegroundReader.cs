using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

namespace FocusOn;

/// <summary>Reads only EWMH foreground metadata. Never enumerates window contents or input.</summary>
public sealed class X11ForegroundReader : IDisposable
{
    private readonly nint display;
    private readonly nuint root;
    private readonly Dictionary<string, nuint> atoms = new();
    private static readonly ConcurrentDictionary<nint, byte> ReaderDisplays = new();
    private static nint previousErrorHandler;
    private static readonly XErrorHandler ErrorHandler = HandleError;
    private static unsafe int HandleError(nint connection, nint error)
    {
        if (ReaderDisplays.ContainsKey(connection) || previousErrorHandler == 0) return 0;
        // Chain the ABI pointer directly: an Avalonia managed callback can have a different
        // delegate type even though its native signature is identical.
        return ((delegate* unmanaged[Cdecl]<nint, nint, int>)previousErrorHandler)(connection, error);
    }
    private static readonly object initialization = new();
    private static bool initialized;
    private bool disposed;

    public X11ForegroundReader()
    {
        lock (initialization)
        {
            if (!initialized)
            {
                XInitThreads();
                previousErrorHandler = XSetErrorHandler(ErrorHandler);
                initialized = true;
            }
        }
        display = XOpenDisplay(null);
        if (display == 0) throw new InvalidOperationException("X11 显示不可用；请在已登录的桌面会话中运行 FocusOn。");
        ReaderDisplays.TryAdd(display, 0);
        root = XDefaultRootWindow(display);
    }

    public ForegroundObservation? Read()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var window = ActiveWindow();
        if (window <= 1 || window == root || XGetWindowAttributes(display, window, out var attributes) == 0 || attributes.MapState != 2) return null;
        var process = Number(window, "_NET_WM_PID");
        var title = Text(window, "_NET_WM_NAME") ?? Text(window, "WM_NAME");
        string? key = null, name = null;
        if (process is > 0 and <= int.MaxValue)
        {
            try
            {
                var path = File.ResolveLinkTarget($"/proc/{process}/exe", returnFinalTarget: true)?.FullName;
                if (path is not null) { key = "exe:" + path; name = Path.GetFileName(path); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        if (ActiveWindow() != window) return null;
        return new ForegroundObservation((long)window, key, name, title, (uint)process);
    }

    private nuint ActiveWindow()
    {
        var window = Number(root, "_NET_ACTIVE_WINDOW");
        if (window != 0) return window;
        XGetInputFocus(display, out window, out _);
        return window;
    }

    private nuint Atom(string name)
    {
        if (!atoms.TryGetValue(name, out var atom)) atoms[name] = atom = XInternAtom(display, name, false);
        return atom;
    }

    private nuint Number(nuint window, string property)
    {
        var value = Property(window, property, out var format, out var count);
        try { return value != 0 && count > 0 && format == 32 ? (nuint)Marshal.ReadIntPtr(value) : 0; }
        finally { if (value != 0) XFree(value); }
    }

    private string? Text(nuint window, string property)
    {
        var value = Property(window, property, out var format, out var count);
        try
        {
            if (value == 0 || format != 8 || count == 0) return null;
            var bytes = new byte[Math.Min((ulong)count, 32768)];
            Marshal.Copy(value, bytes, 0, bytes.Length);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }
        finally { if (value != 0) XFree(value); }
    }

    private nint Property(nuint window, string property, out int format, out nuint count)
    {
        var result = XGetWindowProperty(display, window, Atom(property), 0, 8192, false, 0,
            out _, out format, out count, out _, out var value);
        if (result == 0) return value;
        if (value != 0) XFree(value);
        count = 0;
        return 0;
    }

    public void Dispose() { if (!disposed) { disposed = true; XCloseDisplay(display); ReaderDisplays.TryRemove(display, out _); } }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowAttributes
    {
        public int X, Y, Width, Height, BorderWidth, Depth;
        public nint Visual;
        public nuint Root;
        public int Class, BitGravity, WinGravity, BackingStore;
        public nuint BackingPlanes, BackingPixel;
        public int SaveUnder;
        public nuint Colormap;
        public int MapInstalled, MapState;
        public nint AllEventMasks, YourEventMask, DoNotPropagateMask;
        public int OverrideRedirect;
        public nint Screen;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int XErrorHandler(nint display, nint error);
    [DllImport("libX11.so.6")] private static extern int XInitThreads();
    [DllImport("libX11.so.6")] private static extern nint XSetErrorHandler(XErrorHandler handler);
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern nuint XInternAtom(nint display, string name, [MarshalAs(UnmanagedType.Bool)] bool onlyIfExists);
    [DllImport("libX11.so.6")] private static extern int XGetInputFocus(nint display, out nuint focus, out int revert);
    [DllImport("libX11.so.6")] private static extern int XGetWindowAttributes(nint display, nuint window, out WindowAttributes attributes);
    [DllImport("libX11.so.6")] private static extern int XGetWindowProperty(nint display, nuint window, nuint property, nint offset, nint length, [MarshalAs(UnmanagedType.Bool)] bool delete, nuint requestedType, out nuint actualType, out int actualFormat, out nuint count, out nuint after, out nint data);
    [DllImport("libX11.so.6")] private static extern int XFree(nint data);
}
