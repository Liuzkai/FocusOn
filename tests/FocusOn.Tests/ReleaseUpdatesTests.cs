using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FocusOn.Tests;

public sealed class ReleaseUpdatesTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic installer bytes");
    private static string Sha => Convert.ToHexString(SHA256.HashData(Payload));
    private const string Name = "FocusOn-win-arm64-Setup.exe";
    private const string Url = ReleaseUpdates.RepositoryUrl + "/releases/download/v1.2.3/";
    private static byte[] Release(bool draft = false, bool prerelease = false, string? host = null, string? name = null) => JsonSerializer.SerializeToUtf8Bytes(new {
        draft, prerelease, tag_name = "v1.2.3", html_url = ReleaseUpdates.RepositoryUrl + "/releases/tag/v1.2.3",
        assets = new[] { new { name = name ?? Name, size = Payload.Length, digest = (string?)("sha256:" + Sha), browser_download_url = (host ?? Url) + Name },
                         new { name = Name + ".sha256", size = 100, digest = (string?)null, browser_download_url = Url + Name + ".sha256" } }
    });
    [Fact]
    public void Checks_only_newer_public_stable_versions_and_matching_architecture_and_official_urls()
    {
        Assert.Null(ReleaseUpdates.Parse(Release(draft: true), new(0,1,0), "win-arm64"));
        Assert.Null(ReleaseUpdates.Parse(Release(prerelease: true), new(0,1,0), "win-arm64"));
        Assert.Null(ReleaseUpdates.Parse(Release(), new(1,2,3), "win-arm64"));
        Assert.Null(ReleaseUpdates.Parse(Release(), new(2,0,0), "win-arm64"));
        var update = ReleaseUpdates.Parse(Release(), new(0,1,0), "win-arm64")!;
        Assert.Equal(Name, update.Name); Assert.Equal(new Version(1,2,3), update.Version);
        Assert.Throws<IOException>(() => ReleaseUpdates.Parse(Release(), new(0,1,0), "win-x64"));
        Assert.Throws<IOException>(() => ReleaseUpdates.Parse(Release(host:"https://evil.invalid/"), new(0,1,0), "win-arm64"));
        Assert.Throws<IOException>(() => ReleaseUpdates.Parse(Release(name:"wrong"), new(0,1,0), "win-arm64"));
    }
    [Fact]
    public async Task Explicit_check_and_download_verify_bytes_sha_and_filename_before_exposing_an_installer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var requests = new List<string>();
            var nativeName = "FocusOn-" + ReleaseUpdates.NativeRuntime + (OperatingSystem.IsWindows() ? "-Setup.exe" : OperatingSystem.IsLinux() ? ".tar.gz" : ".zip");
            var nativeRelease = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Release()).Replace(Name, nativeName));
            using var updater = new ReleaseUpdates(new Handler(request => {
                requests.Add(request.RequestUri!.AbsoluteUri);
                return Bytes(request.RequestUri.AbsolutePath.EndsWith("latest") ? nativeRelease : request.RequestUri.AbsolutePath.EndsWith("sha256") ? Encoding.UTF8.GetBytes(Sha + "  " + nativeName + "\n") : Payload);
            }));
            Assert.Empty(requests); var update = await updater.CheckAsync(); Assert.NotNull(update); Assert.Single(requests);
            var file = await updater.DownloadVerifiedAsync(update!, directory);
            Assert.Equal(Payload, File.ReadAllBytes(file)); Assert.Equal(3, requests.Count);
            Assert.All(requests, request => Assert.DoesNotContain("focuson.db", request));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("filename")]
    [InlineData("bytes")]
    [InlineData("redirect")]
    public async Task Damaged_download_bad_checksum_or_untrusted_redirect_never_returns_a_package(string failure)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-update-bad-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var updater = new ReleaseUpdates(new Handler(request => {
                if (failure == "redirect") { var response = new HttpResponseMessage(HttpStatusCode.Redirect); response.Headers.Location = new("https://evil.invalid/file"); return response; }
                if (request.RequestUri!.AbsolutePath.EndsWith("sha256")) return Bytes(Encoding.UTF8.GetBytes((failure == "hash" ? new string('0',64) : Sha) + "  " + (failure == "filename" ? "other.exe" : Name)));
                return Bytes(failure == "bytes" ? new byte[Payload.Length] : Payload);
            }));
            var update = ReleaseUpdates.Parse(Release(), new(0,1,0), "win-arm64")!;
            await Assert.ThrowsAsync<IOException>(() => updater.DownloadVerifiedAsync(update, directory));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
}
