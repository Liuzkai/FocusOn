# Windows installation and updates

The Settings/installer feature starts with v0.1.7. It does not replace the original v0.1.0 WPF attachments or any older portable package. New versions remain drafts until explicitly approved for publication.

Choose `FocusOn-win-x64-Setup.exe` for Intel/AMD Windows or `FocusOn-win-arm64-Setup.exe` for Windows on ARM. Compare SHA256 with the adjacent `.sha256` file before running. The current installers are **unsigned**: checksums verify bytes, not a verified publisher, and Windows may show an unknown-publisher/SmartScreen prompt. No Authenticode success is claimed.

Setup installs for the current user in `%LOCALAPPDATA%\Programs\FocusOn` without required administrator elevation. It creates a Start menu shortcut and optionally a desktop shortcut. Portable ZIPs remain available. Keep the executable in a fixed location for login startup. Existing default records remain in `%LOCALAPPDATA%\FocusOn`; the program folder and data folder are separate.

Quit FocusOn explicitly before installing an update or uninstalling. The installer uses Windows Restart Manager and does not force-kill the recorder. Installing again replaces application files while keeping data. Uninstall removes application files and only the startup entry pointing to that installation; it deliberately keeps records, preferences and the custom-directory pointer. Native validation covers install, installed executable startup/save/exit, same-version reinstall and uninstall on x64/ARM64; an older WPF-to-new-installer upgrade and physical SmartScreen/device behavior require human acceptance.

In Settings → Updates, **Check for updates** requests public stable releases only. It does not upload records, automatically check, silently download or install. Drafts are unavailable to the updater, so an installed development version can legitimately be ahead of the latest public version. When a newer matching installer is available, **Download and install** asks for confirmation, checks size/SHA256/official URL, saves records, pauses the independent timer and quits before showing Setup. Canceling Setup leaves your data unchanged; reopen FocusOn manually. Do not bypass a checksum failure. If a newer old-format release has no matching Setup/checksum, the app explains that no applicable installer exists.

Settings → Data storage accepts a full path or a folder chooser, then **Save and restart**. Select an empty distinct directory. The app preserves the original directory, saves the recorder, copies/verifies SQLite and preferences, then restarts using the new location. It never merges existing records. A small `data-location.json` remains in the platform's original data folder so future launches find the new folder. Keep that pointer and its destination when backing up. External/removable locations must be present and writable at startup. Old versions do not understand the pointer; do not run them against your preserved source and expect automatic merging.

Setup uses [Inno Setup 6.5.4](https://github.com/jrsoftware/issrc/tree/is-6_5_4); its [upstream license](licenses/InnoSetup.txt) is included unchanged. No paid runner, external updater service or new application dependency is required.

## GitHub rate-limit recovery (v0.1.9)

If v0.1.7/v0.1.8 shows `403 (rate limit exceeded)`, manually download the matching official repair installer once and quit FocusOn before installation. Existing records/preferences are retained. Drafts are invisible to stable checks; public/latest remains v0.1.5 until explicitly approved publication, so a development build may have no applicable update.

The fixed checker falls back to the same official repository’s public stable redirect/assets when the API is unavailable. It reads exact download links/digests and headers-only byte counts, then preserves full download/checksum validation before installation. No token or third-party mirror is required. Successful checks cache briefly; API rate limits back off. If the website also fails, use View release and retry later/check connectivity. This is transport recovery, not Authenticode signing; the Windows installers remain unsigned.
