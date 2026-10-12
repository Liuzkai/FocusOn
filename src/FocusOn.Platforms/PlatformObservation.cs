namespace FocusOn;

public sealed record SessionState(bool Sleeping, bool Locked, bool DisplayOff, bool Available, string? Warning = null)
{
    public RecordingConditions Conditions => new(false, Sleeping, Locked, DisplayOff, Available);
}

public interface IForegroundObserver : IDisposable
{
    void Start();
    void Poll();
    void RequestSnapshot(Action<ForegroundObservation?, RecordingTime> consumer);
    bool IsStopped { get; }
}

public interface ISessionObserver : IDisposable
{
    void Start();
    SessionState ReadCurrentState();
    bool IsStopped { get; }
}

public static class PlatformObserver
{
    public static IForegroundObserver CreateForeground(RecordingClock clock,
        Action<ForegroundObservation?, RecordingTime> observed, Action<RecordingTime, string> failed, Action<RecordingTime, string>? interrupted = null)
    {
        if (OperatingSystem.IsWindows()) return new WindowsForegroundObserver(clock, observed, failed);
        if (OperatingSystem.IsMacOS()) return new MacForegroundObserver(clock, observed, failed, interrupted);
        if (OperatingSystem.IsLinux()) return new LinuxForegroundObserver(clock, observed, failed, interrupted);
        throw new PlatformNotSupportedException("FocusOn 支持 Windows、Linux 与 macOS。");
    }

    public static ISessionObserver CreateSession(Action<SessionState> changed)
    {
        if (OperatingSystem.IsWindows()) return new WindowsSessionObserver(changed);
        if (OperatingSystem.IsMacOS()) return new MacSessionObserver(changed);
        if (OperatingSystem.IsLinux()) return new LinuxSessionObserver(changed);
        throw new PlatformNotSupportedException("FocusOn 支持 Windows、Linux 与 macOS。");
    }
}
