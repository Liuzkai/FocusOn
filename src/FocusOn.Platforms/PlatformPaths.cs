namespace FocusOn;

public static class PlatformPaths
{
    public static string DataDirectory => GetDataDirectory(
        OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"));

    public static string GetDataDirectory(string platform, string userHome, string localData, string? xdgDataHome) => platform switch
    {
        "windows" => Path.Combine(localData, "FocusOn"),
        "macos" => Path.Combine(userHome, "Library", "Application Support", "FocusOn"),
        "linux" => Path.Combine(!string.IsNullOrEmpty(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome)
            ? xdgDataHome : Path.Combine(userHome, ".local", "share"), "FocusOn"),
        _ => throw new PlatformNotSupportedException(platform)
    };

    public static (string DataDirectory, bool Isolated, bool Startup) ParseArguments(string[] args)
    {
        var combined = args.Length == 3 && args[0] == "--startup" && args[1] == "--data-dir";
        var isolated = combined || (args.Length == 2 && args[0] == "--data-dir");
        var startup = combined || (args.Length == 1 && args[0] == "--startup");
        if (args.Length != 0 && !isolated && !startup)
            throw new ArgumentException("支持的启动参数：--startup 或 --data-dir <目录>。");
        if (isolated && string.IsNullOrWhiteSpace(args[^1])) throw new ArgumentException("数据目录不能为空。");
        return (isolated ? Path.GetFullPath(args[^1]) : DataDirectory, isolated, startup);
    }
}
