# 0010: Countdown and recoverable desktop timer companion

Status: accepted for implementation, 2026-10-14.

Countdown extends the independent titled timer, never foreground usage or attention measurement. One timer may be active: count-up or countdown. Presets are25 minutes for a work session,5 for a short break and15 for a long break, with custom1–1440 minute UI duration. Work/break transitions are explicit, not automatic.

Store countdown targets in an additive table linked to existing title_timers. Existing stopwatch/history stays readable. Use the worker's monotonic elapsed time, cap every live/saved segment at the target, and atomically save the final segment and completion. Read-only projections never complete/write. An application-owned1s command checks expiry even when a window/page is hidden, without writing every tick. Pauses, sleep, shutdown, time anomalies and restart follow ADR0002; deleting retained timer history changes its retained elapsed time and pauses intersecting active segments. No lost/downtime interval is invented.

User chose remaining/elapsed time plus progress with click-to-restore. Windows11 does not expose a supported arbitrary inline taskbar-text interface. Reuse the existing notification icon/menu with progress and time details, plus a small optional draggable window near the work-area edge. Linux uses the confirmed StatusNotifier host and the same independent window; lack of tray does not make the companion or main window unrecoverable. macOS retains the template menu-bar icon and timer menu details with the same companion. Do not replace native shell processes, install third-party taskbar extensions, request new privacy permissions or create another recorder.

The companion is non-activating, not a taskbar/Dock app entry, and supports text/keyboard restore, existing themes and a saved visibility preference. Countdown ring represents true elapsed/target; count-up uses a clock dial without inventing a completion percentage. Completion remains visible until another task starts; it never forces foreground focus. Native compositor work-area, placement and always-on-top behavior are capabilities to verify, not guaranteed on every Wayland desktop.

New source uses v0.1.11 with the normal draft default. Preserve all previous tags/assets and original MIT/third-party notices. Native builds/tests do not constitute physical multi-monitor/DPI/taskbar/permission/power acceptance; unsigned/unnotarized Mac validation remains excluded from formal Release.
