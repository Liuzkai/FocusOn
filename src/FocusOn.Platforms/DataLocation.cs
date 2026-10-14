using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace FocusOn;

public static class DataLocation
{
    public static string Resolve(string defaultDirectory)
    {
        var path = Path.Combine(defaultDirectory, "data-location.json");
        if (!File.Exists(path)) return defaultDirectory;
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var directory = json.RootElement.GetProperty("directory").GetString();
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw new IOException("数据目录配置无效，请检查 data-location.json；原始记录没有被修改。");
        return Path.GetFullPath(directory);
    }
    public static void Save(string defaultDirectory, string directory) => SettingsFile.WriteAtomic(
        Path.Combine(defaultDirectory, "data-location.json"), JsonSerializer.Serialize(new { directory = Path.GetFullPath(directory) }));
}

/// <summary>Caller must save and close its recorder before Copy; the source is never deleted.</summary>
public sealed class DataDirectoryMigration : IDisposable
{
    private readonly SingleInstance destination;
    private readonly List<string> created = [];
    private bool committed;
    public string Source { get; }
    public string Target { get; }
    public DataDirectoryMigration(string source, string target)
    {
        Source = Canonical(source); Target = Canonical(target);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Source.Equals(Target, comparison) || Source.StartsWith(Target + Path.DirectorySeparatorChar, comparison) || Target.StartsWith(Source + Path.DirectorySeparatorChar, comparison))
            throw new IOException("请选择与当前目录互不包含的空目录。");
        if (Directory.Exists(Target) && Directory.EnumerateFileSystemEntries(Target).Any(p => Path.GetFileName(p) != ".focuson.lock"))
            throw new IOException("目标目录必须为空；不会覆盖或合并已有记录。");
        destination = new SingleInstance(Target);
        if (!destination.IsPrimary) { destination.Dispose(); throw new IOException("目标数据目录正在使用。"); }
        // Recheck after acquiring the writer lock, before doing any recording shutdown.
        if (Directory.EnumerateFileSystemEntries(Target).Any(p => Path.GetFileName(p) != ".focuson.lock"))
        { destination.Dispose(); throw new IOException("目标目录必须为空；不会覆盖或合并已有记录。"); }
    }
    public void CopyAndCommit(string? defaultDirectory)
    {
        CopyDirectory(Source, Target, true);
        var db = Path.Combine(Source, "focuson.db");
        if (File.Exists(db))
        {
            var output = Path.Combine(Target, "focuson.db"); created.Add(output);
            using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = output, Pooling = false }.ToString());
            source.Open(); target.Open(); source.BackupDatabase(target);
            using var check = target.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
            if (check.ExecuteScalar() as string != "ok") throw new IOException("复制后的数据库校验失败；仍使用原数据目录。");
        }
        if (defaultDirectory is not null) DataLocation.Save(defaultDirectory, Target);
        committed = true;
    }
    private void CopyDirectory(string source, string target, bool root)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            var info = new FileInfo(path);
            if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("数据目录内含链接，无法安全迁移。");
            var name = Path.GetFileName(path);
            if (root && name is ".focuson.lock" or "data-location.json" or "focuson.db" or "focuson.db-wal" or "focuson.db-shm") continue;
            var output = Path.Combine(target, name);
            if (Directory.Exists(path)) { Directory.CreateDirectory(output); created.Add(output); CopyDirectory(path, output, false); }
            else
            {
                created.Add(output); File.Copy(path, output, overwrite: false);
                using var a = File.OpenRead(path); using var b = File.OpenRead(output);
                if (!SHA256.HashData(a).SequenceEqual(SHA256.HashData(b))) throw new IOException("数据文件复制校验失败；仍使用原数据目录。");
            }
        }
    }
    private static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new IOException("请选择完整数据目录路径。");
        var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!; var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (Directory.Exists(current)) current = new DirectoryInfo(current).ResolveLinkTarget(true)?.FullName ?? current;
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    public void Dispose()
    {
        if (!committed)
            foreach (var path in created.AsEnumerable().Reverse())
                try { if (Directory.Exists(path)) Directory.Delete(path); else File.Delete(path); } catch (IOException) { }
        destination.Dispose();
    }
}
