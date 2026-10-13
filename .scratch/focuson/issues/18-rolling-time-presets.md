# 18: Rolling hour presets and Today review

**What to build:** Replace Recorded range / 24h buttons with 1h, 2h, 4h, 6h, 8h, 12h, 24h and Today selector. User confirmed real rolling 24h across midnight, Today midnight-to-now, and date-scoped daily totals.

**Blocked by:** None

**Status:** closed

## Acceptance criteria

- Exact elapsed hour spans, Today local/DST midnight; default 1h. Historical date uses its day end, Today returns to current date.
- Cross-day timeline/logs show real records, joining daily parts without duplicate IDs/duration errors. Query one recorder snapshot; preserve all data and date deletion/daily totals.
- Refresh follows a preset, while manual zoom/pan stays fixed. Reset restores chosen preset; preserve selection, hover, filters, scroll/end-follow, timer and bilingual labels. No stale async date/preset result may overwrite current choice.
- Meaningful SQLite/projection/UI/native checks and screenshot/docs updates; v0.1.5 draft via current six-platform workflow, older releases/tags/notice bytes unchanged.

## Comments

- 2026-10-13: Screenshot request and asynchronous reply explicitly confirm rolling 24h versus Today. ADR 0006 records the extension of the original single-date review contract; physical device checks remain with 08.

- 2026-10-13: Implemented all presets and Today with one runtime snapshot, cross-day interval joining and unchanged daily totals/deletion. Manual viewport survives refresh and midnight; Reset follows selected preset. Release build zero warnings/errors; 104 core/platform + 28 UI pass, no failures/skips. Native Linux synthetic-data mouse/keyboard checks and English/Chinese/dropdown screenshots pass. Self-review complete; six native draft jobs pending. Physical device/signing items remain with 08.

- 2026-10-13: Reviewed source `c0938e72afc53a063fc26e0ecc399e8d37b4e398`, immutable v0.1.5 tag object `36e83a964f4801aa0d47183ec9a9bc3d399d898d`. https://github.com/Liuzkai/FocusOn/actions/runs/38342256279 Success: six native architectures each pass 104 core/platform + 28 UI regressions, native desktop and package/source/runtime checks. https://github.com/Liuzkai/FocusOn/releases/tag/untagged-915e2b6097b83a60c8a3 contains eight Windows/Linux archive/checksum assets with downloaded digest verification; previous Release metadata/digests, all old tags and MIT/third-party notice bytes unchanged. Mac Intel/Apple Silicon bundles pass native validation but lack Developer ID/notary/staple and are excluded from Release; physical-device items remain with 08. Engineering complete; parent specification stays open. Delivery documentation is a later commit, without retagging.
