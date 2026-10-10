using Microsoft.Win32;

namespace FocusOn;

public static class StartupRegistration
{
    public static void Ensure(string executablePath)
    {
        var command = BuildCommand(executablePath);
        if (!File.Exists(executablePath)) throw new FileNotFoundException("自启程序位置不存在。", executablePath);
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        run.SetValue("FocusOn", command, RegistryValueKind.String);
    }

    public static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath) ||
            executablePath.Contains('"') ||
            executablePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("自启需要固定位置的完整程序路径。", nameof(executablePath));
        var path = Path.GetFullPath(executablePath);
        if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("自启需要发布的 Windows 程序文件。", nameof(executablePath));
        var command = $"\"{path}\" --startup";
        if (command.Length > 260)
            throw new ArgumentException("自启命令超过 Windows Run 的 260 字符限制，请将程序放在较短的固定路径。", nameof(executablePath));
        return command;
    }
}
