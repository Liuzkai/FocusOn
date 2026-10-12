using System.Runtime.InteropServices;

namespace FocusOn;

internal static class X11DisplayState
{
    public static bool IsOff()
    {
        var display = XOpenDisplay(null);
        if (display == 0) throw new InvalidOperationException("X11 显示不可用。");
        try { return DPMSQueryExtension(display, out _, out _) != 0 && DPMSInfo(display, out var level, out var enabled) != 0 && enabled != 0 && level != 0; }
        finally { XCloseDisplay(display); }
    }
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libXext.so.6")] private static extern int DPMSInfo(nint display, out ushort level, out byte enabled);
    [DllImport("libXext.so.6")] private static extern int DPMSQueryExtension(nint display, out int firstEvent, out int firstError);
}
