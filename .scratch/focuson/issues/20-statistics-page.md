# 20: Multi-day and single-day period statistics

**What to build:** Add a statistics page in the existing desktop app, using the user's dark donut/bar/line chart reference. Support a multi-day date range and a time period within one local day.

**Blocked by:** 18 (closed)

**Status:** closed

## Acceptance criteria

- Clear navigation between review and statistics without stopping recording/timers or losing review selection/viewport. Bilingual labels follow the saved global language.
- Multi-day start/end dates (default last seven days); single-day start/end local times, including a clear 24:00 end. Explicit applied scope, validation and empty/loading/error states.
- Only foreground Usage contributes to statistics. Proportional clipping retains original monotonic allocations, excludes timers/stops/gaps and deleted ranges, treats local/DST boundaries correctly and includes live usage only from one snapshot.
- Dark donut app share, rounded duration bars and app trend lines, readable legends/labels and accessible equivalent app totals. Meaningful values and units, no sample percentages or personal-data screenshots.
- SQLite/projection/UI regressions, native synthetic-data mouse checks, screenshot/English README and usage updates, new v0.1.6 draft through six standard native runners. Published v0.1.5 and earlier tags/assets/licenses remain unchanged. Mac trust/device limits remain with 08.

## Comments

- 2026-10-13: User added statistics while v0.1.5 publication was in progress. It extends the same project and uses a new version draft; no v0.1.5 rebuild/retag. Multi-day totals count every selected calendar day including empty days; a single-day period uses local start/end boundaries. Window titles remain on the review page, statistics show app totals only.

- 2026-10-13: Implemented the statistics page and existing recorder-queue read API with original allocation endpoints. Local 114 core/platform + 34 UI regressions pass; SQLite tests cover partial ticks, buckets/empty dates, deletion/reopen, live read-only snapshots and DST, UI covers navigation/state/language/validation/stale async/empty/error/narrow/high contrast/pointer/keyboard. Native Linux synthetic mouse checks verify seven-day 137040 s / thirty-day 608100 s, 09:30–12:30 10800 s and four apps/three bins, whole day15420 s, invalid scope preservation, empty period0 s, hint movement4 px and language/navigation. Fixed date format and persistent validation feedback during periodic refresh after review. Screenshot/English README/usage/ADR updates; six native v0.1.6 draft pending. No user data or old releases/notice bytes changed.

- 2026-10-13: Reviewed/committed engineering source `161c21f5b48e96701d812b2d80cf918b75acde56`, immutable v0.1.6 tag object `d11531a73290673fcd583a3cb328adc2e6dae6e1`. https://github.com/Liuzkai/FocusOn/actions/runs/38378437157 Success: six native targets each 148 tests (114 core/platform,34UI), no failures/skips, native desktop and package/source/runtime checks. https://github.com/Liuzkai/FocusOn/releases/tag/untagged-29ffa82936509d03290e contains eight Windows/Linux archive/checksum assets with downloaded digest verification. Every prior release's metadata/assets, remote tags, MIT and third-party notices remain unchanged; public/latest v0.1.5 has its original c0938e7/eight assets. Mac native Intel/Apple Silicon builds are ad-hoc-validation with no Developer ID/notary/staple, excluded from Release. Physical permissions/power/DPI and overall acceptance remain with08/parent specification. Engineering complete; delivery documentation is later than the source tag, no retagging.
