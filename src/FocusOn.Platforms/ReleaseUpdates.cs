using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FocusOn;

public sealed record ReleaseUpdate(Version Version, string ReleaseUrl, string Name, Uri DownloadUrl, Uri ChecksumUrl, long Bytes, string? Digest);

public sealed class ReleaseUpdates : IDisposable
{
    public const string RepositoryUrl = "https://github.com/Liuzkai/FocusOn";
    private bool disposed;
    private readonly HttpClient client;
    private readonly CancellationTokenSource stopping = new();
    public ReleaseUpdates(HttpMessageHandler? handler = null)
    {
        client = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FocusOn-Desktop/" + CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public static Version CurrentVersion { get; } = new(typeof(ReleaseUpdates).Assembly.GetName().Version!.ToString(3));
    public static string NativeRuntime => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    public async Task<ReleaseUpdate?> CheckAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var response = await client.GetAsync("https://api.github.com/repos/Liuzkai/FocusOn/releases/latest", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var data = await ReadBoundedAsync(response.Content, 2_000_000, timeout.Token);
        return Parse(data, CurrentVersion, NativeRuntime);
    }
    public static ReleaseUpdate? Parse(byte[] data, Version installed, string runtime)
    {
        using var json = JsonDocument.Parse(data); var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString()!;
        if (!Regex.IsMatch(tag, @"\Av\d+\.\d+\.\d+\z") || !Version.TryParse(tag[1..], out var version)) throw new IOException("正式版本信息无效。");
        if (version <= installed) return null;
        var releaseUrl = RepositoryUrl + "/releases/tag/" + tag;
        if (release.GetProperty("html_url").GetString() != releaseUrl) throw new IOException("更新地址不是官方仓库。");
        var name = "FocusOn-" + runtime + (runtime.StartsWith("win-") ? "-Setup.exe" : runtime.StartsWith("linux-") ? ".tar.gz" : ".zip");
        var assets = release.GetProperty("assets").EnumerateArray().ToArray();
        var matching = assets.Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (matching.Length == 0) throw new IOException("此正式版本没有当前系统架构的更新包。");
        if (matching.Length != 1) throw new IOException("更新附件重复。");
        var checksums = assets.Where(a => a.GetProperty("name").GetString() == name + ".sha256").ToArray();
        if (checksums.Length != 1) throw new IOException("更新包缺少唯一校验和。");
        var asset = matching[0]; var bytes = asset.GetProperty("size").GetInt64();
        if (bytes <= 0 || bytes > 500_000_000) throw new IOException("更新包大小无效。");
        Uri Trusted(JsonElement a, string file)
        {
            var url = a.GetProperty("browser_download_url").GetString();
            if (url != RepositoryUrl + "/releases/download/" + tag + "/" + file) throw new IOException("更新地址不是官方仓库。");
            return new Uri(url);
        }
        var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
        if (digest is not null && !Regex.IsMatch(digest, @"\Asha256:[A-Fa-f0-9]{64}\z")) throw new IOException("更新校验信息无效。");
        return new(version, releaseUrl, name, Trusted(asset, name), Trusted(checksums[0], name + ".sha256"), bytes, digest);
    }
    public async Task<string> DownloadVerifiedAsync(ReleaseUpdate update, string directory)
    {
        Directory.CreateDirectory(directory); var target = Path.Combine(directory, update.Name);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var checksumResponse = await GetAssetAsync(update.ChecksumUrl, stopping.Token);
            var checksum = System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(checksumResponse.Content, 1024, stopping.Token)).Trim();
            var parts = checksum.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"\A[A-Fa-f0-9]{64}\z") || parts[1] != update.Name) throw new IOException("更新校验信息无效。");
            var expected = parts[0];
            if (update.Digest is not null && !update.Digest[7..].Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("更新校验信息不一致。");
            using var response = await GetAssetAsync(update.DownloadUrl, stopping.Token);
            await using (var input = await response.Content.ReadAsStreamAsync(stopping.Token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer, stopping.Token)) > 0)
                { total += count; if (total > update.Bytes) throw new IOException("更新包大小不匹配。"); await output.WriteAsync(buffer.AsMemory(0, count), stopping.Token); }
                if (total != update.Bytes) throw new IOException("更新包大小不匹配。");
            }
            await using (var input = File.OpenRead(temporary))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, stopping.Token)).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("更新包校验失败；不会安装。");
            File.Move(temporary, target, overwrite: true); return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private async Task<HttpResponseMessage> GetAssetAsync(Uri uri, CancellationToken token)
    {
        // GitHub redirects downloads to its own asset storage. Never follow arbitrary redirects.
        for (var count = 0; count < 4; count++)
        {
            if (uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Port != 443 ||
                uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com")) throw new IOException("更新地址不是官方仓库。");
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } next)
            { uri = next.IsAbsoluteUri ? next : new Uri(uri, next); response.Dispose(); continue; }
            try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
        }
        throw new IOException("更新下载重定向过多。");
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken token)
    {
        await using var input = await content.ReadAsStreamAsync(token); using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0) { if (output.Length + count > limit) throw new IOException("更新信息过大。"); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
    public void Dispose() { if (disposed) return; disposed = true; stopping.Cancel(); client.Dispose(); stopping.Dispose(); }
}
