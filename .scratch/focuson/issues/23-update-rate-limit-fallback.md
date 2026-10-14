# 23: Recover update checks from GitHub API rate limits

**What to build:** Fix the installed previous version's update check failing with GitHub API403 rate limit exceeded. Provide an official non-API fallback and actionable bilingual failures without weakening download validation.

**Blocked by:** 22 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- API403/429, API service errors or unavailable API connectivity can check GitHub's public stable latest redirect and official expanded release assets, without credentials or third-party mirrors.
- Preserve stable-only/version/native-architecture/exact official URL/size/digest/adjacent checksum protection. No download body during metadata HEAD requests, no recording upload, bounded responses/timeouts, no arbitrary redirect following.
- Cache a successful short-lived result and rate-limit backoff per application session. Failures explain manual Release access and retry; never present a connectivity failure as no updates.
- Tests exercise rate limit fallback with older/current/newer versions, trusted redirects, exact HEAD size, metadata corruption/architecture/missing checksums, cache/backoff, cancellation, failed fallback and final download validation.
- Reproduce real API-denied/public-latest checks on isolated native desktop, six-target validation and fresh immutable v0.1.9 draft, preserving all earlier releases/tags/notices/data. Explain one manual upgrade for old clients and that v0.1.8 remains draft; public/latest is v0.1.5. No unapproved publication or token requirement.

## Comments

- 2026-10-14: Screenshot shows HTTP403 rate limit exceeded in the API-only v0.1.7/0.1.8 client. Public GitHub latest currently resolves to v0.1.5; the newer settings/navigation releases are drafts and intentionally invisible to stable checks. The installed old binary cannot be patched remotely; the fix needs one manual installer upgrade. Keep the release-draft policy. Existing backend/recording/installation behavior stays unchanged.

- 2026-10-14: Implemented limited API attempt + strict official website fallback, HEAD-only stable latest/package size, bounded expanded-asset parsing with exact same-repository links/adjacent checksum/unique GitHub digest,2min success cache and bounded API reset/backoff.143 core/platform +36 UI regressions pass (19 meaningful fallback/guard/cache/cancel cases),18 publication guards pass. Real cloud API-denied request succeeds with public/latest v0.1.5 older than current0.1.9; actual public Linux asset HTML parses, HEAD returns47773611bytes, validated download SHAFA7FBE91021E44EAF3E428FA94163FC105936AD8FA69E1E3594E59A47C3A4819 matches original release. Native synthetic desktop actual Check click/cached repeat and Chinese/English success status pass; screenshot documents actual network fallback. No system startup/recording data/token use. Source review preserves existing download integrity/stable-only and clear failure behavior. New native v0.1.9 draft pending; old binary requires manual upgrade and formal publication remains separately authorized.
