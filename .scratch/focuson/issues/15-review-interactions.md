# 15: Pointer-following timeline hints, log-end following and app separators

**What to build:** Apply the user's screenshot feedback to the existing Avalonia UI: stable mouse-following hints, automatic bottom following after scrolling to the log end, and small visual gaps at software transitions.

**Blocked by:** None

**Status:** closed

## Acceptance criteria

- Hover hints appear reliably, follow movement within the same interval and update on refresh; hide on leave, drag, collapse, window hiding and shutdown. Preserve precise text and localization.
- Scrolling to the log end follows appended records; scrolling up or selecting history pauses it. Preserve selection, filters, dates, timer and daily totals.
- Add small decorative gaps only at contiguous different-app boundaries. Do not change UTC intervals, duration, hit testing, filtering or erase/expand short segments.
- Check meaningful UI regressions and native interaction, then prepare v0.1.2 as a draft without rewriting public v0.1.1 or changing Mac trust restrictions.

## Comments

- 2026-10-12: User provided three screenshots and explicitly requested these interactions. Current published v0.1.1 is immutable; the next build is planned as v0.1.2 draft. Existing data, notices and platform lifecycle behavior remain in scope for preservation.
- 2026-10-12: Implemented stable non-interactive pointer-anchored popup and cleanup; virtualization-aware log-end following with user navigation taking precedence; proportional different-app decorative insets. Release build 0 warnings/errors; 90 core/platform + 17 UI tests pass (107, no failures/skips). Five new UI cases cover visible hint movement/re-entry/refresh and cleanup, repeated append/follow/up/resume, historical selection/pending-refresh navigation, and unchanged half-open boundary selection/duration/totals. Native Linux/Xvfb mouse events confirm the same popup moves from X=101 to X=114 within one interval, hides/reopens; log tail follows 44→48 records, while after upward scroll the visible log stays pixel-identical across 48→52 appends. Pixel checks show only different-app boundaries have a background gap. Final non-interactive overlay also passes real movement across usage/timer lanes without intercepting the pointer. Native screenshot uses synthetic DayView (including a historical timer), no production database/recording. Six-target validation and v0.1.2 draft pending.

- 2026-10-12: Reviewed and committed engineering source `8f25193fa09ec7672c197a80c4679eb0d4d5df44`, immutable v0.1.2. Run https://github.com/Liuzkai/FocusOn/actions/runs/38237429087 Success: all six native jobs and exact-source package/draft upload pass. Draft https://github.com/Liuzkai/FocusOn/releases/tag/untagged-e5e1022863366939ef6e has eight Windows/Linux archive/checksum attachments, uploaded-download SHA256 checks pass. Mac validation-only bundles excluded; old public tags/assets retained. Local 107 regressions and final actual overlay/scroll/pixel checks pass. Physical DPI/multi-monitor/pointer and other device/trust items remain explicitly transferred to 08/manual acceptance; parent spec stays open. Final record is a later documentation commit, no retagging.
