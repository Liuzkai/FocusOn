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
    private readonly Func<DateTimeOffset> utcNow;
    private DateTimeOffset apiRetryAfter, cacheExpires;
    private bool hasCachedCheck;
    private ReleaseUpdate? cachedUpdate;
    public bool UsedWebsiteFallback { get; private set; }
    private readonly CancellationTokenSource stopping = new();
    public ReleaseUpdates(HttpMessageHandler? handler = null, Func<DateTimeOffset>? utcNow = null)
    {
        client = handler is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FocusOn-Desktop/" + CurrentVersion);
    }
    public static Version CurrentVersion { get; } = new(typeof(ReleaseUpdates).Assembly.GetName().Version!.ToString(3));
    public static string NativeRuntime => (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux") + "-" +
        (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    public async Task<ReleaseUpdate?> CheckAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (hasCachedCheck && utcNow() < cacheExpires) return cachedUpdate;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var limited = utcNow() < apiRetryAfter;
        if (!limited)
        {
            using var apiTimeout = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token); apiTimeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/Liuzkai/FocusOn/releases/latest");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, apiTimeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    var data = await ReadBoundedAsync(response.Content, 2_000_000, apiTimeout.Token);
                    UsedWebsiteFallback = false;
                    return Cache(Parse(data, CurrentVersion, NativeRuntime));
                }
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                {
                    limited = true;
                    var retry = utcNow().AddMinutes(1);
                    if (response.Headers.RetryAfter?.Delta is { } delta) retry = utcNow() + delta;
                    else if (response.Headers.RetryAfter?.Date is { } date) retry = date;
                    else if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var seconds)
                        && seconds is >= 0 and <= 253402300799) retry = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    apiRetryAfter = retry < utcNow().AddMinutes(1) ? utcNow().AddMinutes(1) : retry > utcNow().AddHours(1) ? utcNow().AddHours(1) : retry;
                }
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!timeout.IsCancellationRequested) { }
        }
        try
        {
            var update = await CheckWebsiteAsync(timeout.Token);
            UsedWebsiteFallback = true; return Cache(update);
        }
        catch (HttpRequestException error)
        {
            throw new IOException(limited ? "GitHub 暂时限制了更新请求。请通过“查看 Release”手动下载，或稍后再试。" : "无法连接 GitHub 更新服务。请检查网络，或通过“查看 Release”手动下载。", error);
        }
        catch (OperationCanceledException error) when (!stopping.IsCancellationRequested)
        { throw new IOException("检查更新超时。请稍后再试，或通过“查看 Release”手动下载。", error); }
    }
    private ReleaseUpdate? Cache(ReleaseUpdate? update)
    {
        cachedUpdate = update; hasCachedCheck = true; cacheExpires = utcNow().AddMinutes(2); return update;
    }
    private async Task<ReleaseUpdate?> CheckWebsiteAsync(CancellationToken token)
    {
        // HEAD follows GitHub's public stable latest redirect without downloading a page or package body.
        using var latest = await GetOfficialAsync(new Uri(RepositoryUrl + "/releases/latest"), HttpMethod.Head, token, latest: true);
        if (latest.StatusCode == HttpStatusCode.NotFound) return null;
        latest.EnsureSuccessStatusCode();
        var uri = latest.RequestMessage!.RequestUri!;
        var prefix = RepositoryUrl + "/releases/tag/";
        if (!uri.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal)) throw new IOException("正式版本信息无效。");
        var tag = uri.AbsoluteUri[prefix.Length..]; var version = StableVersion(tag);
        if (version <= CurrentVersion) return null;
        using var assets = await GetOfficialAsync(new Uri(RepositoryUrl + "/releases/expanded_assets/" + tag), HttpMethod.Get, token);
        assets.EnsureSuccessStatusCode();
        var update = ParseWebsite(await ReadBoundedAsync(assets.Content, 2_000_000, token), tag, NativeRuntime);
        using var package = await GetOfficialAsync(update.DownloadUrl, HttpMethod.Head, token);
        package.EnsureSuccessStatusCode();
        var bytes = package.Content.Headers.ContentLength;
        if (bytes is null) throw new IOException("无法确认更新包大小。请通过“查看 Release”手动下载。");
        ValidateSize(bytes.Value); return update with { Bytes = bytes.Value };
    }
    private static Version StableVersion(string tag)
    {
        if (!Regex.IsMatch(tag, @"\Av\d+\.\d+\.\d+\z") || !Version.TryParse(tag[1..], out var version)) throw new IOException("正式版本信息无效。");
        return version;
    }
    private static void ValidateSize(long bytes)
    { if (bytes <= 0 || bytes > 500_000_000) throw new IOException("更新包大小无效。"); }
    private static string AssetName(string runtime) => "FocusOn-" + runtime +
        (runtime.StartsWith("win-") ? "-Setup.exe" : runtime.StartsWith("linux-") ? ".tar.gz" : ".zip");
    public static ReleaseUpdate ParseWebsite(byte[] data, string tag, string runtime)
    {
        var version = StableVersion(tag); var name = AssetName(runtime);
        var url = RepositoryUrl + "/releases/download/" + tag + "/" + name;
        var options = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;
        var rows = Regex.Matches(System.Text.Encoding.UTF8.GetString(data), @"<li\b[^>]*>(.*?)</li>", options, TimeSpan.FromSeconds(1)).Select(m => m.Groups[1].Value).ToArray();
        bool LinksTo(string row, string expected) => Regex.Matches(row, "href\\s*=\\s*[\"']([^\"']*)[\"']", options, TimeSpan.FromSeconds(1))
            .Any(m => Uri.TryCreate(new Uri(RepositoryUrl), WebUtility.HtmlDecode(m.Groups[1].Value), out var link) && link.AbsoluteUri == expected);
        var packages = rows.Where(row => LinksTo(row, url)).ToArray();
        if (packages.Length == 0) throw new IOException("此正式版本没有当前系统架构的更新包。");
        if (packages.Length != 1) throw new IOException("更新附件重复。");
        if (rows.Count(row => LinksTo(row, url + ".sha256")) != 1) throw new IOException("更新包缺少唯一校验和。");
        var digests = Regex.Matches(packages[0], @"sha256:[A-Fa-f0-9]{64}", options, TimeSpan.FromSeconds(1)).Select(m => m.Value.ToLowerInvariant()).Distinct().ToArray();
        if (digests.Length != 1) throw new IOException("更新校验信息无效。");
        // Bytes is resolved with a headers-only request, never from GitHub's rounded size label.
        return new(version, RepositoryUrl + "/releases/tag/" + tag, name, new Uri(url), new Uri(url + ".sha256"), 0, digests[0]);
    }
    public static ReleaseUpdate? Parse(byte[] data, Version installed, string runtime)
    {
        using var json = JsonDocument.Parse(data); var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString()!;
        var version = StableVersion(tag);
        if (version <= installed) return null;
        var releaseUrl = RepositoryUrl + "/releases/tag/" + tag;
        if (release.GetProperty("html_url").GetString() != releaseUrl) throw new IOException("更新地址不是官方仓库。");
        var name = AssetName(runtime);
        var assets = release.GetProperty("assets").EnumerateArray().ToArray();
        var matching = assets.Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (matching.Length == 0) throw new IOException("此正式版本没有当前系统架构的更新包。");
        if (matching.Length != 1) throw new IOException("更新附件重复。");
        var checksums = assets.Where(a => a.GetProperty("name").GetString() == name + ".sha256").ToArray();
        if (checksums.Length != 1) throw new IOException("更新包缺少唯一校验和。");
        var asset = matching[0]; var bytes = asset.GetProperty("size").GetInt64();
        ValidateSize(bytes);
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
            checksumResponse.EnsureSuccessStatusCode();
            var checksum = System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(checksumResponse.Content, 1024, stopping.Token)).Trim();
            var parts = checksum.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"\A[A-Fa-f0-9]{64}\z") || parts[1] != update.Name) throw new IOException("更新校验信息无效。");
            var expected = parts[0];
            if (update.Digest is not null && !update.Digest[7..].Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("更新校验信息不一致。");
            using var response = await GetAssetAsync(update.DownloadUrl, stopping.Token);
            response.EnsureSuccessStatusCode();
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
    private Task<HttpResponseMessage> GetAssetAsync(Uri uri, CancellationToken token) => GetOfficialAsync(uri, HttpMethod.Get, token);
    private async Task<HttpResponseMessage> GetOfficialAsync(Uri uri, HttpMethod method, CancellationToken token, bool latest = false)
    {
        for (var count = 0; count < 4; count++)
        {
            if (uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Port != 443 ||
                uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com")) throw new IOException("更新地址不是官方仓库。");
            if (latest && uri.AbsoluteUri != RepositoryUrl + "/releases/latest" &&
                !Regex.IsMatch(uri.AbsoluteUri, "\\A" + Regex.Escape(RepositoryUrl) + @"/releases/tag/v\d+\.\d+\.\d+\z")) throw new IOException("更新地址不是官方仓库。");
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Accept.ParseAdd(latest || uri.AbsolutePath.Contains("/expanded_assets/", StringComparison.Ordinal) ? "text/html" : "application/octet-stream");
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.RequestMessage ??= request;
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } next)
            { uri = next.IsAbsoluteUri ? next : new Uri(uri, next); response.Dispose(); continue; }
            return response;
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
