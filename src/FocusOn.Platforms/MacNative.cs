using System.Runtime.InteropServices;

namespace FocusOn;

internal static class MacNative
{
    internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    internal const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    internal const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    internal static void LoadAppKit() => NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
    internal static nint Send(nint instance, string selector) => Message(instance, Selector(selector));
    internal static nint String(string text) => CFStringCreateWithCString(0, text, 0x08000100);
    internal static string? Text(nint value)
    {
        if (value == 0) return null;
        var length = CFStringGetMaximumSizeForEncoding(CFStringGetLength(value), 0x08000100) + 1;
        if (length is < 1 or > 131072) return null;
        var buffer = new byte[(int)length];
        if (!CFStringGetCString(value, buffer, length, 0x08000100)) return null;
        var end = Array.IndexOf(buffer, (byte)0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }
    internal static nint DictionaryValue(nint dictionary, string key)
    {
        var name = String(key);
        try { return CFDictionaryGetValue(dictionary, name); }
        finally { CFRelease(name); }
    }
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")] internal static extern nint Class(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")] private static extern nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern nint Message(nint instance, nint selector);
    [DllImport(CoreFoundation)] internal static extern void CFRelease(nint value);
    [DllImport(CoreFoundation)] internal static extern nuint CFHash(nint value);
    [DllImport(CoreFoundation)] private static extern nint CFStringCreateWithCString(nint allocator, string value, uint encoding);
    [DllImport(CoreFoundation)] private static extern nint CFStringGetLength(nint value);
    [DllImport(CoreFoundation)] private static extern nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);
    [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CFStringGetCString(nint value, byte[] buffer, nint size, uint encoding);
    [DllImport(CoreFoundation)] private static extern nint CFDictionaryGetValue(nint dictionary, nint key);
    [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool CFBooleanGetValue(nint value);
    [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool CFNumberGetValue(nint value, int type, out int result);
    [DllImport(ApplicationServices)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool AXIsProcessTrusted();
    [DllImport(ApplicationServices)] internal static extern nint AXUIElementCreateApplication(int pid);
    [DllImport(ApplicationServices)] internal static extern int AXUIElementCopyAttributeValue(nint element, nint attribute, out nint value);
    [DllImport(ApplicationServices)] internal static extern int AXUIElementSetMessagingTimeout(nint element, float timeout);
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")] internal static extern nint CGSessionCopyCurrentDictionary();
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")] internal static extern int CGGetOnlineDisplayList(uint capacity, [Out] uint[]? displays, out uint count);
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")] internal static extern uint CGDisplayIsAsleep(uint display);
}
