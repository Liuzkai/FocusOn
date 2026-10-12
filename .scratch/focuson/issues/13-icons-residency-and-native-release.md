# 13: Application icons, resident desktop behavior and native releases

**What to build:** Continue the existing application with integrated Windows/Linux/macOS icons, recoverable tray/menu bar behavior, native runner validation, and a guarded draft release with optional Developer ID signing and notarization.

**Blocked by:** None (existing implementation is available; hardware acceptance stays with 08)

**Status:** ready-for-agent

## Acceptance criteria

- Integrate application, tray, desktop and bundle icons; preserve local databases, language preferences and all third-party notices.
- Close keeps recording and timers alive; an available tray/menu restores the window and offers explicit exit. An unavailable/lost Linux tray keeps or restores a visible window with a bilingual explanation.
- Avoid duplicate writers and tray icons; exit saves the final boundary and disposes owned observers, IPC and tray resources.
- Validate native Mac APIs and privacy behavior without treating cross-compilation as device acceptance. Support Intel and Apple Silicon on native standard runners.
- Reuse GitHub Actions with six standard public-repository runner targets, locked restore, tests, native desktop checks, verified self-contained packages and checksums. Temporary artifacts expire after seven days.
- Manual/version-tag draft release only; check immutable tag/source identity and avoid clobbering existing assets. Preserve v0.1.0 Windows/Linux attachments and their documented source differences.
- Prepare Developer ID import, hardened runtime, minimal runtime entitlements, notarization, stapling and verification; secrets stay in Actions Secrets. Missing credentials leave Mac outputs unverified and out of release attachments.
- Report exact commits/runs, assets, signing results and outstanding physical desktop/permission checks.

## Comments

- 2026-10-12: User confirmed preparing v0.1.1 draft, retaining v0.1.0. First native run 38214196313 passed both Windows/Linux architectures; Mac failed. Diagnostics run 38214665412 exposed two actual prior Mac defects: Unix pipe paths exceed Darwin's 104-byte limit, and fixed-argument fcntl P/Invoke corrupts memory under Darwin ARM64's variadic ABI. Shortened only Mac IPC identity to 128 bits, retained Windows/Linux IPC names, and supplied ARM64 varargs on the stack. Added real symlink/case-alias activation regression. No Mac attachment uploaded or readiness claimed.

- 2026-10-12: Baseline main 12f482c is clean and matches origin/main. Original MIT notice from d21c09f is retained. v0.1.0 remains at 43d6dfd; published Linux source is 409cb51. A separate v0.1.1 draft is proposed; user confirmed the v0.1.1 draft while implementation proceeds.
- 2026-10-12: User will set About manually. Do not request ABOUT_TOKEN or change repository About as part of this ticket.
- 2026-10-12: Implemented integrated 16–256 Windows ICO/executable resource, window/tray PNG, Linux 16–1024 hicolor/desktop installer, and multi-size Mac ICNS/template. Added recoverable residency and Dock Reopen; controller/database/language rules preserved. Native Mac observer implementation reviewed; TCC permission denial remains explicit.
- 2026-10-12: Release build 0 warnings/errors, 89 core/platform + 12 UI tests pass (0 failed/skipped), native X11/two-process smoke passes. Real Linux window/SQLite/timer/language/exit and no-tray fallback pass. Isolated real D-Bus StatusNotifier registration, hide/restore and host disappearance restores the window; this is protocol validation, not a GNOME/KDE physical desktop pass. actionlint, Python compile and diff checks pass. Native Actions validation pending.
