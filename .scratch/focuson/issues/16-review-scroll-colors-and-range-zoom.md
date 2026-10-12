# 16: Unobstructed totals, range zoom and distinguishable persistent colors

**What to build:** Resolve the Windows screenshot feedback in the existing application: reserve room for the totals scrollbar, right-click to zoom a selected range, and reduce similar software colors.

**Blocked by:** None

**Status:** ready-for-agent

## Acceptance criteria

- Horizontal scrolling never covers totals chips; scrolling and refresh remain usable.
- Wheel zoom, right-drag pan, left-click record selection and left-drag range selection remain available. Right-click zooms a selected range without clearing it or changing saved intervals/full-day totals. Escape and the explicit clear button still clear filters.
- Assign perceptually separated app colors and preserve assignments across refresh, sorting, dates and restart. Same app color is shared by totals, log and timeline; labels and high contrast remain available. Preserve production records and language preferences.
- Run meaningful regression/native checks, update the README screenshot and prepare a new v0.1.3 draft using the existing workflow, preserving every older tag/release and Mac trust restrictions.

## Comments

- 2026-10-12: Confirmed screenshot feedback. Previous v0.1.1 public release and v0.1.2 draft are immutable; no existing attachment or tag will be replaced. Physical Windows DPI/multi-monitor checks remain with ticket 08.

- 2026-10-12: Implemented the non-overlay Fluent scrollbar row, exact selected-range zoom and persistent farthest-distance OKLab color assignments. Color-only preferences use a separate atomic JSON file, preserve corrupt/unwritable files and never alter activity data/language settings. Reviewed gesture/filter/half-open/duration semantics and lifecycle preservation. Local 90 core/platform + 21 UI regressions pass, no skips; Release build has no warnings/errors. Native Linux mouse checks confirm non-overlap (chip bottom 182, scrollbar top 188), horizontal offset retained across refresh, exact selected UTC viewport/log IDs, pan preserves filter and Escape restores all logs. Prior pointer/follow/history/gap checks also pass; README screenshot refreshed with synthetic data. Six-platform draft build pending; physical-device checks explicitly transferred to 08.
