using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

try
{
    if (args.Length < 2) throw new ArgumentException("Usage: FocusOn.Build <repository> <build|test|publish|package-check|smoke> [runtime] [package-name]");
    var root = Path.GetFullPath(args[0]);
    Directory.SetCurrentDirectory(root);
    var sdk = Environment.GetEnvironmentVariable("FOCUSON_DOTNET") ?? "dotnet";
    const string app = "src/FocusOn/FocusOn.csproj";
    var runtime = args.Length > 2 && args[2].Length > 0 && args[2] != "-" ? args[2] : RuntimeInformation.RuntimeIdentifier;
    switch (args[1])
    {
        case "build": Run(sdk, "build", app, "-c", "Release", "--nologo"); break;
        case "test":
            Run(sdk, "test", "tests/FocusOn.Tests/FocusOn.Tests.csproj", "-c", "Release", "--nologo");
            Run(sdk, "test", "tests/FocusOn.UiTests/FocusOn.UiTests.csproj", "-c", "Release", "--nologo");
            break;
        case "smoke": Run(sdk, "run", "--project", "tests/FocusOn.NativeSmoke/FocusOn.NativeSmoke.csproj", "-c", "Release"); break;
        case "publish": case "package-check":
            if (!new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" }.Contains(runtime))
                throw new ArgumentException("Runtime must be win-x64, win-arm64, linux-x64, linux-arm64, osx-x64 or osx-arm64.");
            var validation = args[1] == "package-check";
            var dirty = Capture("git", "status", "--porcelain").Length != 0;
            if (!validation && dirty) throw new InvalidOperationException("请先提交或处理工作区修改再发布；发布清单必须对应已提交的源版本。");
            var name = args.Length > 3 && args[3].Length > 0 && args[3] != "-" ? args[3] : "FocusOn-" + runtime;
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^FocusOn-[A-Za-z0-9-]+$")) throw new ArgumentException("Invalid package name.");
            var parent = Path.Combine(root, "artifacts", validation ? "validation" : "release");
            Directory.CreateDirectory(parent);
            var package = Path.Combine(parent, name);
            // Build into a fresh staging directory; replace outputs only after all validation succeeds.
            var staging = Path.Combine(parent, ".staging-" + Guid.NewGuid().ToString("N"));
            var destination = runtime.StartsWith("osx-") ? Path.Combine(staging, "FocusOn.app", "Contents", "MacOS") : staging;
            try
            {
                Run(sdk, "publish", app, "-c", "Release", "-r", runtime, "--self-contained", "true", "-o", destination, "--nologo");
                var exe = runtime.StartsWith("win-") ? "FocusOn.exe" : "FocusOn";
                foreach (var required in new[] { exe, "FocusOn.dll", "FocusOn.Core.dll", "FocusOn.Platforms.dll", "FocusOn.deps.json", "FocusOn.runtimeconfig.json", "Avalonia.Controls.dll", "Microsoft.Data.Sqlite.dll" })
                    if (!File.Exists(Path.Combine(destination, required))) throw new IOException("发布包缺少 " + required);
                using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "FocusOn.runtimeconfig.json")));
                var frameworks = configuration.RootElement.GetProperty("runtimeOptions").GetProperty("includedFrameworks");
                if (!frameworks.EnumerateArray().Any(x => x.GetProperty("name").GetString() == "Microsoft.NETCore.App")) throw new IOException("发布包未包含自包含运行时。");
                using var dependencies = JsonDocument.Parse(File.ReadAllText(Path.Combine(destination, "FocusOn.deps.json")));
                if (!dependencies.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!.EndsWith("/" + runtime)) throw new IOException("发布包架构错误。");
                var sqlite = runtime.StartsWith("win-") ? "e_sqlite3.dll" : runtime.StartsWith("osx-") ? "libe_sqlite3.dylib" : "libe_sqlite3.so";
                var skia = runtime.StartsWith("win-") ? "libSkiaSharp.dll" : runtime.StartsWith("osx-") ? "libSkiaSharp.dylib" : "libSkiaSharp.so";
                foreach (var native in new[] { sqlite, skia })
                    if (!File.Exists(Path.Combine(destination, native))) throw new IOException("发布包缺少本地库 " + native);
                if (runtime.StartsWith("osx-") && !File.Exists(Path.Combine(destination, "libAvaloniaNative.dylib"))) throw new IOException("发布包缺少 macOS 界面本地库。");
                if (!OperatingSystem.IsWindows() && !runtime.StartsWith("win-")) File.SetUnixFileMode(Path.Combine(destination, exe), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                if (runtime.StartsWith("osx-"))
                {
                    var contents = Path.GetDirectoryName(destination)!;
                    Directory.CreateDirectory(Path.Combine(contents, "Resources"));
                    File.WriteAllText(Path.Combine(contents, "Info.plist"), """
                        <?xml version="1.0" encoding="UTF-8"?>
                        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                        <plist version="1.0"><dict>
                        <key>CFBundleExecutable</key><string>FocusOn</string>
                        <key>CFBundleIdentifier</key><string>org.focuson.app</string>
                        <key>CFBundleName</key><string>FocusOn</string>
                        <key>CFBundleDisplayName</key><string>FocusOn</string>
                        <key>CFBundlePackageType</key><string>APPL</string>
                        <key>CFBundleVersion</key><string>1.0.0</string>
                        <key>CFBundleShortVersionString</key><string>1.0.0</string>
                        <key>LSMinimumSystemVersion</key><string>15.0</string>
                        <key>NSHighResolutionCapable</key><true/>
                        <key>CFBundleIconFile</key><string>focuson.icns</string>
                        </dict></plist>
                        """);
                    var png = File.ReadAllBytes(Path.Combine(root, "src", "FocusOn", "Assets", "focuson.png"));
                    using var icon = File.Create(Path.Combine(contents, "Resources", "focuson.icns"));
                    icon.Write(Encoding.ASCII.GetBytes("icns")); WriteBigEndian(icon, png.Length + 16);
                    icon.Write(Encoding.ASCII.GetBytes("icp6")); WriteBigEndian(icon, png.Length + 8); icon.Write(png);
                    icon.Close();
                    if (OperatingSystem.IsMacOS())
                    {
                        Run("codesign", "--force", "--deep", "--sign", "-", Path.Combine(staging, "FocusOn.app"));
                        Run("codesign", "--verify", "--deep", "--strict", Path.Combine(staging, "FocusOn.app"));
                    }
                }
                File.Copy(Path.Combine(root, "docs", "usage.md"), Path.Combine(staging, "使用说明.md"));
                File.Copy(Path.Combine(root, "docs", "platforms.md"), Path.Combine(staging, "平台说明.md"));
                File.Copy(Path.Combine(root, "README.md"), Path.Combine(staging, "README.md"));
                File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(staging, "LICENSE"));
                CopyDirectory(Path.Combine(root, "docs", "licenses"), Path.Combine(staging, "licenses"));
                if (runtime.StartsWith("linux-")) CopyDirectory(Path.Combine(root, "platforms", "gnome"), Path.Combine(staging, "gnome"));
                var records = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).Select(path => new
                {
                    path = Path.GetRelativePath(staging, path).Replace('\\', '/'), bytes = new FileInfo(path).Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                }).ToArray();
                var manifest = new { sourceCommit = validation && dirty ? null : Capture("git", "rev-parse", "HEAD"),
                    baseCommit = Capture("git", "rev-parse", "HEAD"), buildKind = validation ? "development-validation" : "release", runtime, selfContained = true, files = records };
                File.WriteAllText(Path.Combine(staging, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                if (Directory.Exists(package)) Directory.Delete(package, true);
                Directory.Move(staging, package);
                var archive = package + (runtime.StartsWith("win-") ? ".zip" : ".tar.gz");
                if (File.Exists(archive)) File.Delete(archive);
                if (runtime.StartsWith("win-")) ZipFile.CreateFromDirectory(package, archive, CompressionLevel.Optimal, includeBaseDirectory: true);
                else
                {
                    using var output = File.Create(archive); using var gzip = new GZipStream(output, CompressionLevel.Optimal);
                    TarFile.CreateFromDirectory(package, gzip, includeBaseDirectory: true);
                }
                File.WriteAllText(archive + ".sha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))) + "  " + Path.GetFileName(archive) + "\n");
                Console.WriteLine($"已核对自包含文件、架构与清单：{archive}");
                if (validation) Console.WriteLine("开发验证产物；不是已提交源版本的正式发布。");
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            break;
        default: throw new ArgumentException("Unknown build action: " + args[1]);
    }
}
catch (Exception error) { Console.Error.WriteLine(error.Message); Environment.ExitCode = 1; }

static void Run(string command, params string[] arguments)
{
    if (command == Environment.GetEnvironmentVariable("FOCUSON_DOTNET")) arguments = [.. arguments, "--property:RestoreLockedMode=true"];
    using var process = Process.Start(Start(command, arguments)) ?? throw new IOException("无法启动 " + command);
    process.WaitForExit(); if (process.ExitCode != 0) throw new IOException(command + " 退出码 " + process.ExitCode);
}
static string Capture(string command, params string[] arguments)
{
    var start = Start(command, arguments); start.RedirectStandardOutput = true;
    using var process = Process.Start(start)!; var text = process.StandardOutput.ReadToEnd(); process.WaitForExit();
    if (process.ExitCode != 0) throw new IOException("无法读取 " + command + " 的结果。"); return text.Trim();
}
static ProcessStartInfo Start(string command, string[] arguments)
{
    var start = new ProcessStartInfo(command) { UseShellExecute = false };
    foreach (var argument in arguments) start.ArgumentList.Add(argument); return start;
}
static void WriteBigEndian(Stream stream, int value)
{
    Span<byte> bytes = stackalloc byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, value); stream.Write(bytes);
}
static void CopyDirectory(string source, string destination)
{
    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
    }
}
