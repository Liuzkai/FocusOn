# 21: Settings, light/dark theme, installation and updates

**What to build:** Continue the same application with a Settings page, move language there, add saved Light/Dark, changeable data directory, About/version/GitHub, useful login startup control, Windows per-user installers and explicit stable-release update checks.

**Blocked by:** 20 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- Three-page navigation preserves review/statistics and recording. Language/theme persist; high contrast overrides both. Every custom chart/log remains legible in Light.
- About is last, with actual build version and repository link. Only useful managed preferences: language/theme/startup/data location. No sampling/durability tuning, automatic deletion or speculative settings.
- Data changes explicitly confirm a save/restart, preserve original data, reject occupied/overlapping/symlink destinations, lock writers, copy SQLite consistently and validate before atomically redirecting normal startup. Isolated --data-dir must not alter production location or startup registration.
- Explicit update check fetches only official stable public GitHub releases, matches native OS/architecture, rejects missing/mismatched SHA256/digests/untrusted URLs, never silently downloads/installs or records upload. Windows installs verified package after saving; other OS links clearly describe manual replacement.
- Windows x64/ARM64 per-user installer: icon/shortcuts/uninstall/upgrade, no required elevation, preservation of data, no forced-kill writer. Native install/upgrade/uninstall tests and installer source/checksum evidence.
- Reuse six standard runners; new immutable v0.1.7 draft and hashes, preserve all older releases/tags/MIT/third-party notices. Clearly report signing/device limits.

## Comments

- 2026-10-14: User supplied all seven requested items; no pending item-2 clarification. Existing v0.1.6 remains unchanged. Updates are manual and stable-public only; default Dark and existing login startup behavior are retained. Data relocation is explicit copy/save/restart, preserving the source, not arbitrary merging.

- 2026-10-14: Implemented all requested configuration items with immediate/persisted two-language/two-theme navigation and useful startup/folder controls. Local Release build and 124 core/platform +36 UI regressions pass, plus18 release publication/selection guards. Real Linux X11 native lifecycle/Settings/theme/About/isolation checks pass. Actual isolated FocusOn starts a timer, accepts a new path/explicit confirmation, saves/copies/restarts into one target process; all source SQLite rows/tables/settings survive with source retained and both databases integrity_check ok. No production path or startup was modified. Synthetic settings/Light statistics screenshots added; native Windows installer verification and new immutable v0.1.7 draft pending.

- 2026-10-14: First source f07b115 / run38502262854: Windows x64/ARM64 all160 tests, native desktop/package and actual installer/installed executable/reinstall/uninstall/preserved data pass; Linux ARM64 passes. Mac tests exposed an indirect ancestor alias canonicalization bug (/var vs /private/var) and expected lexical path mismatch. Canonical targets now recursively normalize returned links with a depth guard; tests additionally reject indirect ancestor aliases. Linux x64 exposed an existing polling fixture's second valid sample racing the assertion under runner load: fixture now controls one delayed lookup and subsequent unavailable observations, preserving the intended no-backfill assertion. High contrast also explicitly selects the Dark Fluent controls while retaining the user's Light choice, preventing dark-card/light-widget text collisions. Local124+36 regressions pass after fixes. No v0.1.7 tag/release existed during this iteration.
