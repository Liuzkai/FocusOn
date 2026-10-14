# Timers and desktop companion

Choose **Stopwatch** or **Countdown** at the top of Records. Both are independent of foreground app totals; only one can be active at a time.

- **Stopwatch:** enter a title, start, pause/resume, then stop to retain history.
- **Countdown:** choose Focus 25 min, Break 5 min, Break 15 min, or Custom1–1440 minutes. The title is optional. Start, pause/resume or stop; at zero the session finishes once and retains its actual elapsed history. Start the next work/break session explicitly.
- Closing the main window keeps a running timer active when a confirmed tray is available. Pausing recording or locking the screen does not pause the independent timer. Sleep, exit, a detected time/sampling anomaly, or deletion intersecting its active history pauses it; manually resume afterward. Restart restores the last saved elapsed time as paused, without counting downtime.

## Companion

Starting or restoring a timer shows a small window near the screen's work-area edge. It displays remaining time and a true elapsed/target percentage for countdowns; stopwatches show elapsed time and a clock hand without a completion percentage. A paused/completed state stays visible.

Click the window to restore the same FocusOn main window, or drag anywhere except its close button to move it. **×** hides only this task's companion and keeps the timer running. A new task shows it again. Settings → Startup and background → **Show compact timer** saves the overall visibility choice; disable/enable it to restore the current companion. It does not create another recorder or writer.

The window opens without taking focus, has no separate taskbar entry, follows Light/Dark/high contrast and English/Chinese, and is repositioned into an available screen's work area after display changes. Its position is retained during the current process, not persisted across restart.

| Platform | Native panel entry | Visible timer |
| --- | --- | --- |
| Windows | Existing notification-area icon; countdown progress ring, hover time and timer menu item | Separate draggable companion near the taskbar; click either entry to restore FocusOn |
| macOS | Existing template menu-bar icon and timer menu item; the host may omit hover text | Same companion near the upper work-area edge; Dock Reopen/native Quit preserved |
| Linux | Existing StatusNotifier icon with time/menu/progress, where a host is confirmed | Same companion; it also works without a tray host |

This is not an embedded Windows11 taskbar toolbar or a macOS inline text extension. Linux XEmbed-only trays remain unsupported; GNOME requires an AppIndicator/StatusNotifier host. Some Wayland compositors ignore app-requested position or always-on-top; use the available window position and the main-window/tray controls. FocusOn does not inject into the shell or require new privacy permissions for this feature.

Completion updates the companion, main page and tray/menu item without forcing foreground activation, starting another session or automatically playing sound. Turning off the companion leaves the main/tray timer details available.

## History and compatibility

Countdown targets extend the existing SQLite timer history with an additive table. Old stopwatch records and preferences are retained. Saved duration and countdown completion are committed together; failed writes pause the timer and preserve the last trusted checkpoint. Read-only views cannot extend history beyond the countdown target.

Deleting a day retains other dates and pauses an intersecting active timer. Remaining time is based on retained history, so deleting some elapsed timer history increases its remaining time. Deleting retained history from a completed countdown also dismisses its old completion display. There is no automatic history restoration or merge. Back up the complete data folder after Exit; avoid running an older version against a currently unfinished countdown, because older clients do not understand its target.

Native runner checks and the Linux synthetic preview are documented with release evidence. Physical Windows/macOS/Linux taskbar/menu interaction, multiple monitors/DPI, sleep/lock and Wayland placement are still separate [manual checks](manual-acceptance.md). Mac signing/notarization requirements are unchanged.
