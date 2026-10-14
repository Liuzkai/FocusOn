using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FocusOn.Tests;

public sealed class ReleaseUpdateFallbackTests
{
    private const string Repo = ReleaseUpdates.RepositoryUrl;
    private const string Tag = "v1.2.3";
    private static string Name => "FocusOn-" + ReleaseUpdates.NativeRuntime + (OperatingSystem.IsWindows() ? "-Setup.exe" : OperatingSystem.IsLinux() ? ".tar.gz" : ".zip");
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("synthetic verified update");
    private static string Sha => Convert.ToHexString(SHA256.HashData(Payload));
    private static byte[] Html(string? package = null, bool checksum = true, string? digest = null) => Encoding.UTF8.GetBytes(
        $"<ul><li><a href=\"{package ?? $"/Liuzkai/FocusOn/releases/download/{Tag}/{Name}"}\">{Name}</a><span>SHA256:{digest ?? Sha}</span><span>36.1 MB</span></li>" +
        (checksum ? $"<li><a href=\"/Liuzkai/FocusOn/releases/download/{Tag}/{Name}.sha256\">checksum</a></li>" : string.Empty) + "</ul>");
    private static HttpResponseMessage Response(HttpStatusCode status = HttpStatusCode.OK, byte[]? bytes = null) => new(status) { Content = new ByteArrayContent(bytes ?? []) };
    private static HttpResponseMessage Redirect(string url) { var r = Response(HttpStatusCode.Redirect); r.Headers.Location = new(url); return r; }
    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task API_failure_recovers_using_stable_official_redirect_exact_HEAD_size_and_verified_download(int status)
    {
        var requests = new List<(string Method, string Url)>();
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-update-fallback-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var updater = new ReleaseUpdates(new Handler(req => {
                requests.Add((req.Method.Method, req.RequestUri!.AbsoluteUri));
                if (req.RequestUri.Host == "api.github.com") return Response((HttpStatusCode)status);
                if (req.RequestUri.AbsoluteUri == Repo + "/releases/latest") { Assert.Equal(HttpMethod.Head, req.Method); return Redirect(Repo + "/releases/tag/" + Tag); }
                if (req.RequestUri.AbsoluteUri == Repo + "/releases/tag/" + Tag) return Response();
                if (req.RequestUri.AbsolutePath.Contains("expanded_assets")) return Response(bytes:Html());
                if (req.Method == HttpMethod.Head) { var r=Response(); r.Content.Headers.ContentLength=Payload.Length; return r; }
                if (req.RequestUri.AbsolutePath.EndsWith("sha256")) return Response(bytes:Encoding.UTF8.GetBytes(Sha + "  " + Name));
                return Response(bytes:Payload);
            }));
            var update = await updater.CheckAsync(); Assert.NotNull(update); Assert.True(updater.UsedWebsiteFallback);
            Assert.Equal(Payload.Length,update!.Bytes); Assert.Equal(new Version(1,2,3),update.Version); Assert.Equal("sha256:"+Sha.ToLowerInvariant(),update.Digest);
            Assert.DoesNotContain(requests,r=>r.Method=="GET" && r.Url.EndsWith(Name));
            var count=requests.Count; Assert.Same(update,await updater.CheckAsync()); Assert.Equal(count,requests.Count);
            var file=await updater.DownloadVerifiedAsync(update,directory); Assert.Equal(Payload,File.ReadAllBytes(file));
        }
        finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task An_older_public_latest_is_not_mistaken_for_a_draft_update_and_repeated_checks_are_cached()
    {
        var requests=new List<string>();
        using var updater=new ReleaseUpdates(new Handler(req=>{
            requests.Add(req.RequestUri!.AbsoluteUri);
            if(req.RequestUri.Host=="api.github.com")return Response(HttpStatusCode.Forbidden);
            return req.RequestUri.AbsoluteUri.EndsWith("latest")?Redirect(Repo+"/releases/tag/v0.1.5"):Response();
        }));
        Assert.Null(await updater.CheckAsync()); Assert.True(updater.UsedWebsiteFallback); Assert.Equal(3,requests.Count);
        Assert.Null(await updater.CheckAsync()); Assert.Equal(3,requests.Count);
        Assert.DoesNotContain(requests,u=>u.Contains("expanded_assets")||u.Contains("download"));
    }
    [Fact]
    public async Task Rate_limit_backoff_skips_API_until_reset_but_allows_website_checks_after_the_result_cache_expires()
    {
        var now=DateTimeOffset.UtcNow;var api=0;var website=0;
        using var updater=new ReleaseUpdates(new Handler(req=>{
            if(req.RequestUri!.Host=="api.github.com") { api++;var response=Response(HttpStatusCode.Forbidden);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(10));return response; }
            website++;return req.RequestUri.AbsoluteUri.EndsWith("latest")?Redirect(Repo+"/releases/tag/v0.1.5"):Response();
        }),()=>now);
        Assert.Null(await updater.CheckAsync());now=now.AddMinutes(3);Assert.Null(await updater.CheckAsync());Assert.Equal(1,api);Assert.Equal(4,website);
        now=now.AddMinutes(11);Assert.Null(await updater.CheckAsync());Assert.Equal(2,api);
    }
    [Theory]
    [InlineData("https://evil.invalid/releases/tag/v1.2.3")]
    [InlineData("https://github.com/other/repo/releases/tag/v1.2.3")]
    [InlineData(Repo+"/releases/tag/v1.2.3-beta")]
    [InlineData(Repo+"/releases/tag/untagged-deadbeef")]
    public async Task Latest_redirects_to_untrusted_other_repository_or_nonstable_tags_are_never_followed(string destination)
    {
        var urls=new List<string>();
        using var updater=new ReleaseUpdates(new Handler(req=>{urls.Add(req.RequestUri!.AbsoluteUri);return req.RequestUri.Host=="api.github.com"?Response(HttpStatusCode.Forbidden):Redirect(destination);}));
        await Assert.ThrowsAsync<IOException>(()=>updater.CheckAsync()); Assert.DoesNotContain(destination,urls);
    }
    [Theory]
    [InlineData("missing-checksum")]
    [InlineData("different-architecture")]
    [InlineData("bad-digest")]
    [InlineData("duplicate")]
    public void Website_metadata_rejects_missing_or_conflicting_package_evidence(string failure)
    {
        var html=failure switch {
            "missing-checksum"=>Html(checksum:false),"different-architecture"=>Html(package:"https://evil.invalid/"+Name),
            "bad-digest"=>Html(digest:"wrong"),_=>Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Html())+Encoding.UTF8.GetString(Html()))
        };
        Assert.Throws<IOException>(()=>ReleaseUpdates.ParseWebsite(html,Tag,ReleaseUpdates.NativeRuntime));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(500000001)]
    public async Task Missing_or_invalid_content_length_is_not_guessed_from_the_rounded_website_size(long length)
    {
        using var updater=new ReleaseUpdates(new Handler(req=>{
            if(req.RequestUri!.Host=="api.github.com")return Response(HttpStatusCode.Forbidden);
            if(req.RequestUri.AbsoluteUri.EndsWith("latest"))return Redirect(Repo+"/releases/tag/"+Tag);
            if(req.RequestUri.AbsolutePath.Contains("expanded_assets"))return Response(bytes:Html());
            var r=Response();if(req.RequestUri.AbsoluteUri.Contains("/download/")){if(length<0)r.Content=new UnknownLengthContent();else r.Content.Headers.ContentLength=length;}return r;
        }));
        await Assert.ThrowsAsync<IOException>(()=>updater.CheckAsync());
    }
    [Fact]
    public async Task An_unavailable_website_reports_a_retry_or_manual_download_instead_of_no_updates()
    {
        using var updater=new ReleaseUpdates(new Handler(_=>Response(HttpStatusCode.Forbidden)));
        var error=await Assert.ThrowsAsync<IOException>(()=>updater.CheckAsync()); Assert.Contains("查看 Release",error.Message); Assert.Contains("稍后",error.Message);
    }
    [Fact]
    public async Task An_unreachable_API_still_checks_the_official_website()
    {
        using var updater = new ReleaseUpdates(new Handler(req => {
            if (req.RequestUri!.Host == "api.github.com") throw new HttpRequestException("API unavailable");
            return req.RequestUri.AbsoluteUri.EndsWith("latest") ? Redirect(Repo + "/releases/tag/v0.1.5") : Response();
        }));
        Assert.Null(await updater.CheckAsync()); Assert.True(updater.UsedWebsiteFallback);
    }
    [Fact]
    public async Task Disposing_the_client_cancels_an_inflight_check_without_attempting_website_requests()
    {
        var handler = new WaitingHandler(); var updater = new ReleaseUpdates(handler);
        var check = updater.CheckAsync(); await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); updater.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check); Assert.Equal(1, handler.Requests);
    }
    private sealed class WaitingHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Requests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests++; Started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return Response(); }
    }
    private sealed class UnknownLengthContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(respond(request)); }
}
