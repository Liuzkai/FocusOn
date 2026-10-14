# 27: Countdown and cross-platform timer companion

**What to build:** Add a Pomodoro-style countdown to the existing independent timer and a compact, draggable companion showing remaining/elapsed time and countdown progress. Use the existing tray/menu-bar entry across Windows, Linux and macOS; clicking the companion restores the main window.

**Blocked by:** 10 (closed), 13 (closed), 21 (closed), 26 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- Provide 25/5/15 minute presets, custom duration, title, start, pause/resume and stop. Exactly one independent timer; countdown expires once at its real boundary and preserves history without entering negative time or extending past the target.
- Use the serial worker, monotonic clock and real SQLite, adding only compatible schema. Saved running tasks reopen paused without counting downtime. Retain sleep/time-anomaly/delete/exit rules and existing usage totals.
- A one-second application-owned update completes countdowns while the main window is hidden or on another page. Show a clear completion state without forced activation or automatic work/break cycling.
- Reuse one tray icon with progress/time/menu details and one optional, non-activating, draggable, themed companion window. Restore main window on click/keyboard; show elapsed for count-up, remaining/progress for countdown. Explain supported desktop interfaces: do not inject a Windows taskbar toolbar or claim unsupported inline text. No tray dependency or duplicate writer/window. Clean up during update, migration and exit.
- Persist a necessary show-widget preference; support English/Chinese, Light/Dark/high contrast and accessible text. Reposition into a current screen work area after display changes; document compositor placement/always-on-top limits.
- Test completion/pause/restart/deletion/failure/data compatibility plus meaningful UI/lifecycle checks, inspect a real Linux synthetic preview and run six standard native targets. New v0.1.11 draft defaults; do not change the v0.1.10 public release or historical notices/tags/assets. Mac trust and physical desktop acceptance remain with08.

## Comments

- 2026-10-14 (Asia/Shanghai): User requests countdown similar to Pomodoro and a taskbar statistics component. User selected “剩余/已用时间＋进度，点击恢复主窗口”. Windows11 has no public interface for arbitrary embedded taskbar text; agreed progress tray plus compact companion near the taskbar, with the existing menu-bar/tray entry on macOS/Linux. Countdown uses explicit25/5/15 presets and custom duration, no automatic cycling. Continue the same main checkout after83f8837; preserve released v0.1.10.

- 2026-10-14 local implementation/review: additive target table, clamped live/saved history, atomic final write/completion and app-owned1s expiry loop; 25/5/15/custom UI with optional countdown title, one independent task and existing sleep/reopen/delete/save-failure rules. Real write-lock failure and completed-history deletion regressions prevent false completion/resurrection. Saved companion visibility, per-task hide, one reusable non-activating window, native tray image/time/menu and cleanup on exit/update/migration. Native pointer investigation moved handling to window tunneling so child text does not swallow click/drag; completed refresh no longer overwrites the next title. Local152 core/platform+45UI=197 PASS,18 offline publication guards PASS, Release build zero warnings/errors. Linux X11 no-tray native smoke passes countdown exact2s/tray-image rendering/companion/opt-out/reuse and lifecycle; actual App with isolated synthetic data verifies real drag/restore and Statistics-page background completion with exactly60s saved history. English/Chinese/Light/Dark/high-contrast semantics and missing-tray/Wayland placement documented. Native six-target CI, immutable source and v0.1.11 draft still pending; older public v0.1.10/tags/notices unchanged.
