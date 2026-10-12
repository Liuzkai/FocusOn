using System.Security;
using Microsoft.Win32;

namespace FocusOn;

public static class StartupRegistration
{
    public static void Ensure(string executablePath)
    {
        if (!File.Exists(executablePath)) throw new FileNotFoundException("自启程序位置不存在。", executablePath);
        if (!Path.GetFileNameWithoutExtension(executablePath).Equals("FocusOn", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("自启需要发布的 FocusOn 程序。", nameof(executablePath));
        if (OperatingSystem.IsWindows())
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            run.SetValue("FocusOn", BuildCommand(executablePath), RegistryValueKind.String);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "org.focuson.app.plist");
            WriteAtomic(path, MacLaunchAgent(executablePath));
        }
        else if (OperatingSystem.IsLinux())
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var config = !string.IsNullOrEmpty(xdg) && Path.IsPathFullyQualified(xdg) ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            WriteAtomic(Path.Combine(config, "autostart", "focuson.desktop"), LinuxDesktopEntry(executablePath));
        }
        else throw new PlatformNotSupportedException();
    }

    private static void WriteAtomic(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, text); File.Move(temporary, path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string BuildCommand(string executablePath)
    {
        var absolute = executablePath is { Length: > 3 } &&
            ((char.IsAsciiLetter(executablePath[0]) && executablePath[1] == ':' && executablePath[2] == '\\')
             || executablePath.StartsWith(@"\\", StringComparison.Ordinal));
        if (!absolute || executablePath.Any(c => c < 32 || c is '"' or '<' or '>' or '|' or '*' or '?') ||
            !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("自启需要固定位置的 Windows 程序路径。", nameof(executablePath));
        var command = $"\"{executablePath}\" --startup";
        if (command.Length > 260) throw new ArgumentException("自启命令超过 Windows Run 的 260 字符限制。", nameof(executablePath));
        return command;
    }

    public static string LinuxDesktopEntry(string path, bool startup = true)
    {
        ValidateUnixExecutable(path);
        var escaped = path.Replace("\\", "\\\\\\\\").Replace("\"", "\\\\\"").Replace("`", "\\\\`").Replace("$", "\\\\$").Replace("%", "%%");
        var icon = Path.Combine(Path.GetDirectoryName(path)!, "icons", "hicolor", "256x256", "apps", "focuson.png").Replace("\\", "\\\\");
        return $"[Desktop Entry]\nType=Application\nName=FocusOn\nComment=Offline foreground app usage and titled timers\nExec=\"{escaped}\"{(startup ? " --startup" : string.Empty)}\nIcon={icon}\nTerminal=false\nCategories=Utility;Office;\nStartupWMClass=FocusOn\n" + (startup ? "X-GNOME-Autostart-enabled=true\n" : string.Empty);
    }

    public static string MacLaunchAgent(string path)
    {
        ValidateUnixExecutable(path);
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n<plist version=\"1.0\"><dict><key>Label</key><string>org.focuson.app</string><key>ProgramArguments</key><array><string>{SecurityElement.Escape(path)}</string><string>--startup</string></array><key>RunAtLoad</key><true/></dict></plist>\n";
    }

    private static void ValidateUnixExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Any(c => c < 32))
            throw new ArgumentException("自启需要固定位置的完整程序路径。", nameof(path));
    }
}
