using System.Text.Json;

namespace FocusOn;

public sealed class LinuxForegroundObserver : PollingForegroundObserver
{
    private X11ForegroundReader? x11;
    private readonly bool wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
        || Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland";

    public LinuxForegroundObserver(RecordingClock clock, Action<ForegroundObservation?, RecordingTime> observed,
        Action<RecordingTime, string> failed, Action<RecordingTime, string>? interrupted = null)
        : base(clock, observed, failed, interrupted) { }

    protected override ForegroundObservation? ReadForeground()
    {
        if (wayland)
        {
            try { return GnomeBridge.Read().Observation; }
            catch (Exception error) { throw new InvalidOperationException("Wayland 前台采集需要启用 FocusOn GNOME 扩展；见 docs/platforms.md。" + error.Message, error); }
        }
        x11 ??= new X11ForegroundReader();
        return x11.Read();
    }
    protected override void Close() => x11?.Dispose();
}

internal static class GnomeBridge
{
    public static (ForegroundObservation? Observation, SessionState State) Read()
    {
        var result = CommandRunner.Run("busctl", "--user", "--json=short", "--timeout=1", "call", "org.focuson.Observer",
            "/org/focuson/Observer", "org.focuson.Observer", "Snapshot");
        using var envelope = JsonDocument.Parse(result);
        return Parse(envelope.RootElement.GetProperty("data")[0].GetString()!);
    }

    internal static (ForegroundObservation? Observation, SessionState State) Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var locked = root.GetProperty("locked").GetBoolean();
        var observation = root.TryGetProperty("window", out var window) && window.ValueKind != JsonValueKind.Null
            ? new ForegroundObservation(window.GetProperty("id").GetInt64(), window.GetProperty("key").GetString(),
                window.GetProperty("name").GetString(), window.GetProperty("title").GetString(), window.GetProperty("pid").GetUInt32()) : null;
        return (locked ? null : observation, new SessionState(false, locked, root.GetProperty("displayOff").GetBoolean(), true));
    }
}
