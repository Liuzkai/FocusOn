# 0009: Official Release fallback for rate-limited update checks

Date: 2026-10-14. Status: accepted (user-reported installed-client failure).

The API-only updater in v0.1.7/0.1.8 fails on GitHub403 rate limit exceeded. It cannot obtain draft updates, either: public/latest is still v0.1.5. Do not turn drafts public or ask for user GitHub tokens to hide this failure. The installed old binary needs one manual upgrade to the fixed client.

Retain API metadata when it is available. Bound the API attempt to8s within a25s overall check. On HTTP403/429, API service/transport errors or API-only timeout, use GitHub's official public stable `/releases/latest` redirect. Follow only same-repository stable vX.Y.Z redirects; use HEAD to avoid page/package bodies. If that version is not newer than the installed assembly, finish successfully without reading assets. Otherwise fetch bounded official `/releases/expanded_assets/<tag>` HTML, require unique exact OS/architecture download and adjacent checksum links, and the single consistent GitHub SHA256 digest within that package's row. Determine exact bytes through a headers-only request, never the rounded human size label.

Keep HTTPS/official download-host redirects and final size/SHA256/checksum-filename/digest agreement. No mirrors, API credentials, record uploads, automatic download or weakened validation. Unexpected/missing/duplicate metadata, prerelease/untagged/foreign redirects and unknown or oversize Content-Length fail visibly, retaining manual official Release access.

Cache only successful checks for2min per process; API limits back off at least1min up to1h using Retry-After/rate-reset where available. Explicit checks after cache expiry can still use the website during API backoff. No timers or background network requests are added. Window/application shutdown cancels in-flight requests. Failures explain network/limit/timeout and View release/retry; they never mean no newer version. Chinese/English status confirms successful official fallback, including the legitimate case where a development version is newer than the public stable release.

New v0.1.9 source/tag/draft preserves all earlier versions and recording data. Old-client API rate limits cannot be repaired remotely: use the official installer once, then future explicitly published stable releases use the repaired checker. Physical old-to-new upgrade and user network acceptance remain in08; signing/trust policy remains ADR0005/0008.
