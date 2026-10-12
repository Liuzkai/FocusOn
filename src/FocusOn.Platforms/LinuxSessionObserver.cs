using System.Text.Json;

namespace FocusOn;

public sealed class LinuxSessionObserver(Action<SessionState> changed) : PollingSessionObserver(changed)
{
    protected override SessionState ReadState()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
            || Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland")
        {
            try { return GnomeBridge.Read().State; }
            catch { return new(false, false, false, false, "Wayland：请启用随附的 FocusOn GNOME 扩展以授权前台采集；仍可使用标题计时和回看。"); }
        }
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_ID") ?? "self";
        string properties;
        try { properties = CommandRunner.Run("loginctl", "show-session", session, "--no-pager", "-p", "LockedHint", "-p", "Active"); }
        catch
        {
            return new(false, false, false, false, "Linux：无法确认锁屏和会话状态，请在支持 logind 的本地桌面会话运行。标题计时和历史回看仍可使用。");
        }
        var values = properties.Split('\n').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
        if (!values.TryGetValue("LockedHint", out var locked) || !values.TryGetValue("Active", out var active))
            return new(false, false, false, false, "logind 未提供当前会话状态；请从本地桌面会话启动。");
        var sleeping = false;
        try
        {
            using var json = JsonDocument.Parse(CommandRunner.Run("busctl", "--system", "--json=short", "--timeout=1", "get-property",
                "org.freedesktop.login1", "/org/freedesktop/login1", "org.freedesktop.login1.Manager", "PreparingForSleep"));
            sleeping = json.RootElement.GetProperty("data")[0].GetBoolean();
        }
        catch { /* A scheduling gap independently fences all unobserved suspend time. */ }
        return new(sleeping, locked == "yes", X11DisplayState.IsOff(), active == "yes");
    }
}
