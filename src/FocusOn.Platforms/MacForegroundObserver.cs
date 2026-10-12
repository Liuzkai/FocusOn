namespace FocusOn;

public sealed class MacForegroundObserver : PollingForegroundObserver
{
    private bool loaded;
    public MacForegroundObserver(RecordingClock clock, Action<ForegroundObservation?, RecordingTime> observed,
        Action<RecordingTime, string> failed, Action<RecordingTime, string>? interrupted = null)
        : base(clock, observed, failed, interrupted) { }

    protected override ForegroundObservation? ReadForeground()
    {
        if (!loaded) { MacNative.LoadAppKit(); loaded = true; }
        if (!MacNative.AXIsProcessTrusted())
            throw new InvalidOperationException("macOS：请在系统设置 → 隐私与安全性 → 辅助功能中授权 FocusOn，再重新启动。标题计时与回看仍可使用。");
        var pool = MacNative.Send(MacNative.Class("NSAutoreleasePool"), "new");
        try
        {
            var workspace = MacNative.Send(MacNative.Class("NSWorkspace"), "sharedWorkspace");
            var app = MacNative.Send(workspace, "frontmostApplication");
            if (app == 0) return null;
            var pid = (int)MacNative.Send(app, "processIdentifier");
            if (pid <= 0) return null;
            var application = MacNative.AXUIElementCreateApplication(pid);
            nint window = 0, title = 0;
            try
            {
                MacNative.AXUIElementSetMessagingTimeout(application, 0.3f);
                var attribute = MacNative.String("AXFocusedWindow");
                try
                {
                    if (MacNative.AXUIElementCopyAttributeValue(application, attribute, out window) != 0 || window == 0) return null;
                }
                finally { MacNative.CFRelease(attribute); }
                attribute = MacNative.String("AXTitle");
                try { MacNative.AXUIElementCopyAttributeValue(window, attribute, out title); }
                finally { MacNative.CFRelease(attribute); }
                var bundle = MacNative.Text(MacNative.Send(app, "bundleIdentifier"));
                var url = MacNative.Send(app, "executableURL");
                var path = url == 0 ? null : MacNative.Text(MacNative.Send(url, "path"));
                var key = !string.IsNullOrEmpty(bundle) ? "bundle:" + bundle : path is null ? null : "exe:" + path;
                var name = MacNative.Text(MacNative.Send(app, "localizedName"));
                var windowId = unchecked((long)MacNative.CFHash(window));
                var current = MacNative.Send(workspace, "frontmostApplication");
                if (current == 0 || (int)MacNative.Send(current, "processIdentifier") != pid) return null;
                return new(windowId, key, name, MacNative.Text(title), (uint)pid);
            }
            finally
            {
                if (title != 0) MacNative.CFRelease(title);
                if (window != 0) MacNative.CFRelease(window);
                MacNative.CFRelease(application);
            }
        }
        finally { MacNative.Send(pool, "drain"); }
    }
}
