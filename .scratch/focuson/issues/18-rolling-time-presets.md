# 18: Rolling hour presets and Today review

**What to build:** Replace Recorded range / 24h buttons with 1h, 2h, 4h, 6h, 8h, 12h, 24h and Today selector. User confirmed real rolling 24h across midnight, Today midnight-to-now, and date-scoped daily totals.

**Blocked by:** None

**Status:** ready-for-agent

## Acceptance criteria

- Exact elapsed hour spans, Today local/DST midnight; default 1h. Historical date uses its day end, Today returns to current date.
- Cross-day timeline/logs show real records, joining daily parts without duplicate IDs/duration errors. Query one recorder snapshot; preserve all data and date deletion/daily totals.
- Refresh follows a preset, while manual zoom/pan stays fixed. Reset restores chosen preset; preserve selection, hover, filters, scroll/end-follow, timer and bilingual labels. No stale async date/preset result may overwrite current choice.
- Meaningful SQLite/projection/UI/native checks and screenshot/docs updates; v0.1.5 draft via current six-platform workflow, older releases/tags/notice bytes unchanged.

## Comments

- 2026-10-13: Screenshot request and asynchronous reply explicitly confirm rolling 24h versus Today. ADR 0006 records the extension of the original single-date review contract; physical device checks remain with 08.

- 2026-10-13: Implemented all presets and Today with one runtime snapshot, cross-day interval joining and unchanged daily totals/deletion. Manual viewport survives refresh and midnight; Reset follows selected preset. Release build zero warnings/errors; 104 core/platform + 28 UI pass, no failures/skips. Native Linux synthetic-data mouse/keyboard checks and English/Chinese/dropdown screenshots pass. Self-review complete; six native draft jobs pending. Physical device/signing items remain with 08.
