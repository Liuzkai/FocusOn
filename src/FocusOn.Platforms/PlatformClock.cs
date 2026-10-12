using System.Runtime.InteropServices;

namespace FocusOn;

public static class PlatformClock
{
    public static RecordingClock Create()
    {
        if (OperatingSystem.IsLinux())
        {
            // CLOCK_MONOTONIC (Stopwatch on Linux) excludes suspend. CLOCK_BOOTTIME
            // includes it, so a normal sleep is not confused with a wall-clock jump.
            static long Read()
            {
                if (ClockGetTime(7, out var time) != 0) throw new InvalidOperationException("无法读取系统连续计时器。");
                return checked(time.Seconds * TimeSpan.TicksPerSecond + time.Nanoseconds / 100);
            }
            var started = Read(); return new(() => Read() - started);
        }
        if (OperatingSystem.IsMacOS())
        {
            if (MachTimebaseInfo(out var scale) != 0 || scale.Denominator == 0) throw new InvalidOperationException("无法读取 macOS 连续计时器。");
            long Read() => checked((long)((UInt128)MachContinuousTime() * scale.Numerator / scale.Denominator / 100));
            var started = Read(); return new(() => Read() - started);
        }
        // Windows QPC includes sleep states.
        return new RecordingClock();
    }

    [StructLayout(LayoutKind.Sequential)] private struct TimeSpec { public long Seconds, Nanoseconds; }
    [StructLayout(LayoutKind.Sequential)] private struct Timebase { public uint Numerator, Denominator; }
    [DllImport("libc", EntryPoint = "clock_gettime")] private static extern int ClockGetTime(int clock, out TimeSpec time);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_continuous_time")] private static extern ulong MachContinuousTime();
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_timebase_info")] private static extern int MachTimebaseInfo(out Timebase scale);
}
