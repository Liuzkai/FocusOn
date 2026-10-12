# 0005: Recoverable residency and release trust

Status: accepted for implementation; v0.1.1 draft confirmed by the user.

Close preserves the one window, recording controller, titled timer and data-directory lock. A confirmed tray hides the window. When a Linux StatusNotifier host is missing, its item registration cannot be confirmed, or it disappears, the window stays visible or is restored. This also covers tray creation failure on Windows/macOS. Explicit Exit and native Quit save the final boundary and dispose owned resources. Dock Reopen and a second invocation restore the existing window.

Avalonia 11.3 uses StatusNotifierItem over the session D-Bus on Linux. Its XEmbed fallback is unimplemented; a legacy XEmbed tray alone is insufficient. Confirm both the host property and this process's registered item. Unknown/timeout means unavailable. GNOME needs an AppIndicator/StatusNotifier host extension independently of the foreground capture extension.

Generated FocusOn clock artwork supplies the Windows executable/window/tray, Linux hicolor resources and desktop entries, and a multiresolution Mac ICNS. A transparent monochrome template supplies the Mac menu bar.

Preserve v0.1.0 and its differing Windows/Linux source commits. New ordinary pushes only create seven-day Actions artifacts. Native six-architecture builds and tests gate manual/version-tag releases, which stay drafts. No asset clobbering or old tag rewriting. A new release verifies its tag resolves to the exact source of every attached package.

Mac Developer ID signing is opt-in when all required Actions Secrets exist. Sign nested Mach-O code then the app, use hardened runtime with JIT entitlements, submit to Apple, staple the app, validate and assess it, and only then inventory/archive/checksum the final bundle. Missing credentials produce clearly marked ad-hoc validation artifacts, excluded from Release attachments. Successful notarization is separate from permission, sleep and physical desktop acceptance; drafts remain pending those checks.
