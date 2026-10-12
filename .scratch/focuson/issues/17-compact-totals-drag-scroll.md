# 17: Compact transient totals indicator and dragging the app strip

**What to build:** Apply the user's follow-up to the daily app totals: hide the horizontal scrollbar when idle, show a thin line during pointer/scroll interaction, and allow dragging the app labels themselves.

**Blocked by:** None

**Status:** closed

## Acceptance criteria

- A three-DIP position line appears during interaction and hides after about one second idle. Reserve only six DIP below labels, with no overlap or layout jumps.
- Left-drag anywhere on app chips scrolls the strip, with pointer capture and bounds clamping. Wheel, horizontal trackpad, keyboard and the thin indicator remain usable.
- Clicks/right button do not change records/filters; short/fitting totals do not consume unrelated scrolling. Refresh preserves offset; hiding/deactivation/capture loss/close release gesture/timer resources.
- Preserve prior colors, timeline gestures, log following, data, notices and immutable releases. Meaningful UI and native mouse checks, screenshot/usage updates, and the existing six-platform draft workflow prepare v0.1.4 without replacing v0.1.3.

## Comments

- 2026-10-12: User explicitly replaces the always-visible large scrollbar from ticket 16 with a transient narrow line and chip dragging. Existing v0.1.3 draft/tag/assets stay unchanged.

- 2026-10-12: Implemented label and footer dragging, both wheel axes, keyboard scrolling, fixed thin footer with idle indicator and input/lifecycle cleanup. 90 core/platform + 23 UI regressions pass; meaningful new gesture/idle/refresh/cleanup/fitting checks pass. Real Linux mouse and rendered-pixel checks confirm drag survives a held refresh, line visible during input and hidden after idle without covering/moving labels. Native preview uses synthetic DayView only. Release build zero warnings/errors; old releases, data and notices unchanged. Native six-architecture v0.1.4 draft pending.

- 2026-10-12: Reviewed and committed engineering source `eafc2053e5377dfe52af00013e4d5975c75d2893`; immutable v0.1.4. https://github.com/Liuzkai/FocusOn/actions/runs/38254694792 Success: all six native architectures each 113 tests, desktop/package checks pass; https://github.com/Liuzkai/FocusOn/releases/tag/untagged-2e201aae9575a400ec4c contains eight downloaded-verified Windows/Linux assets. Old Release metadata/attachments and saved prior digests match; source tags/license bytes unchanged. Mac ad-hoc validation excluded; physical Windows/macOS DPI/trackpad and device checks remain with 08. Engineering complete, parent specification open. Delivery documentation is a later commit, no retagging.
