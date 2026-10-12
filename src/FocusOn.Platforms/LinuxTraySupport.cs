namespace FocusOn;

/// <summary>Detect the StatusNotifier tray actually used by Avalonia 11.3 on Linux.</summary>
public static class LinuxTraySupport
{
    public static bool IsAvailable(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;
        try
        {
            const string service = "org.kde.StatusNotifierWatcher";
            const string path = "/StatusNotifierWatcher";
            if (CommandRunner.Run("busctl", "--user", "get-property", service, path, service, "IsStatusNotifierHostRegistered") != "b true") return false;
            var items = CommandRunner.Run("busctl", "--user", "get-property", service, path, service, "RegisteredStatusNotifierItems");
            var prefix = "org.kde.StatusNotifierItem-" + processId + "-";
            if (items.Contains("\"" + prefix, StringComparison.Ordinal)) return true;
            // Some watchers canonicalize a registered well-known name to its unique owner.
            var names = CommandRunner.Run("busctl", "--user", "--no-pager", "--no-legend", "list");
            foreach (var line in names.Split('\n'))
            {
                var name = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (name?.StartsWith(prefix, StringComparison.Ordinal) != true) continue;
                var owner = CommandRunner.Run("busctl", "--user", "call", "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetNameOwner", "s", name);
                var unique = owner.Split('"').ElementAtOrDefault(1);
                if (unique is not null && items.Contains("\"" + unique + "/StatusNotifierItem\"", StringComparison.Ordinal)) return true;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException) { /* Absence/timeout is an unavailable tray, never permission to hide. */ }
        return false;
    }
}
