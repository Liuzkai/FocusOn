# FocusOn

An offline desktop app for **Windows, Linux, and macOS** that records foreground app usage and provides independent titled timers. Records stay on your computer in a local SQLite database.

FocusOn measures how long an app's window stays active, including reading or watching without keyboard input. It does not measure attention or count background apps.

## Interface

![FocusOn: titled timer, daily app totals, three timeline lanes, and activity log](docs/images/focuson-ui.png)

An actual Linux screenshot with demonstration records. The session warning appears because the cloud environment has no real desktop login session.

The interface supports **English and Simplified Chinese**. Use the language selector in the top row to switch immediately. The preference is saved for the selected data directory; the first launch follows the system language, with English as the fallback. App names, window titles, timer titles, and historical records are preserved in their original language.

[View the Simplified Chinese interface](docs/images/focuson-ui.zh-CN.png).

## Features

- Daily app totals and a timeline with separate usage, stopped, and timer lanes.
- Window and title changes recorded as separate segments.
- Independent titled timers with start, pause, resume, and stop controls.
- Date navigation, search, range filtering, zoom, and record details. Filters do not change daily totals.
- Manual recording pause and platform session observation for lock, sleep, and display state.
- Confirmation before deleting a day's records; records on other dates are preserved.
- Local storage, login startup, and activation of the existing instance when launched again.

## Download and run

Download the complete package from [GitHub Releases](https://github.com/Liuzkai/FocusOn/releases). The packages include the .NET runtime; users do not need the SDK. The `.sha256` files let you verify the archives. GitHub's **Source code** downloads are source archives, not runnable apps.

| Platform | Architectures | Package and launch |
| --- | --- | --- |
| Windows | x64, ARM64 | Extract the complete ZIP and run `FocusOn.exe`. |
| Linux with glibc | x64, ARM64 | Extract the complete `.tar.gz` and run `./FocusOn` in a graphical desktop session. |
| macOS 15+ | Intel x64, Apple Silicon ARM64 | Move the complete `FocusOn.app` to a stable location, then open it. |

Packages listed here describe the supported build targets. Availability depends on the assets attached to each release. Linux assets appended to the existing Windows `v0.1.0` release use the cross-platform source commit recorded in their `manifest.json` and release notes; the original Windows tag and assets remain unchanged.

Linux X11 foreground capture requires a desktop session with X11 and systemd-logind. Debian/Ubuntu graphical dependencies commonly include `libx11-6 libxext6 libice6 libsm6 libfontconfig1 libgl1`. GNOME Wayland requires the bundled extension in `gnome/focuson@focuson.local/`; see the [platform guide](docs/platforms.md) for installation. Other Wayland desktops can use history and titled timers, but foreground capture is unavailable.

macOS requires permission in **System Settings → Privacy & Security → Accessibility**, followed by a restart. Screen Recording permission is not requested. Native macOS packaging uses ad-hoc signing; developer signing and notarization are not included.

An ordinary first launch registers login startup for the current user. Closing the window keeps recording: Windows/macOS use the tray or menu bar; Linux keeps a minimized taskbar entry. Choose **Exit** in the window or tray to save and stop the process.

For detailed operation and platform requirements, see the [usage guide](docs/usage.md), [platform guide](docs/platforms.md), and [manual acceptance checklist](docs/manual-acceptance.md). These supporting documents currently use Chinese.

## Data and privacy

| Platform | Default data directory |
| --- | --- |
| Windows | `%LOCALAPPDATA%\FocusOn` |
| Linux | `$XDG_DATA_HOME/FocusOn`, or `~/.local/share/FocusOn` |
| macOS | `~/Library/Application Support/FocusOn` |

The existing Windows SQLite data remains compatible. Exit the old version before upgrading or backing up the complete data directory. Window titles can contain private information; FocusOn records titles but does not capture screenshots, document content, or keyboard input. There is no cloud sync or automatic history cleanup.

Use a separate data directory for development or demonstrations; this also skips login startup registration:

```bash
./FocusOn --data-dir /tmp/focuson-demo
```

On Windows, run `FocusOn.exe --data-dir <test-directory>` instead. Language preferences are stored in `settings.json` alongside the database, independently of recording history.

## Development

Use the .NET SDK pinned by [global.json](global.json), currently **10.0.401**. The app uses .NET 10, Avalonia 11.3, and Microsoft.Data.Sqlite. Restoring dependencies requires a network connection; the installed app runs offline.

Windows PowerShell:

```powershell
./build.ps1 build
./build.ps1 test
./build.ps1 publish -Runtime win-x64
```

Linux / macOS:

```bash
./build.sh build
./build.sh test
./build.sh publish linux-x64  # Use osx-x64 or osx-arm64 on macOS.
```

The scripts prefer a repository SDK in `.tools/dotnet/`, then the SDK on `PATH`. Dependencies use committed lockfiles. `test` runs the core/platform SQLite regressions and headless Avalonia UI tests on all supported operating systems. Native Linux window capture and cross-process activation can be checked in an isolated X11 display:

```bash
xvfb-run -a ./build.sh smoke
```

`publish` requires a clean, committed working tree. It creates a self-contained directory, archive, per-file manifest, and archive SHA256 under `artifacts/release/`. Supported runtime identifiers are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. Unix archives preserve executable permissions.

For uncommitted development checks, use `./build.sh package-check <runtime>` or `./build.ps1 package-check -Runtime <runtime>`. These outputs go to `artifacts/validation/` and are marked as development validation; they are not formal committed releases.

## GitHub release automation

The Windows release workflow builds, tests, packages, and verifies version tags before creating a release draft. The Linux release workflow builds and verifies x64/ARM64 packages, then appends them to the selected existing release without replacing Windows assets. It uses GitHub's built-in workflow token, without requiring a personal token for uploading release assets.

Linux release notes record the exact source commit and validation scope. Cross-compilation and automated tests do not replace real-device checks for permissions, sleep, lock, tray behavior, DPI, or ARM64 startup.

## Source layout

- `src/FocusOn.Core/`: recording rules, SQLite persistence, daily projection, titled timers, and display text.
- `src/FocusOn.Platforms/`: native foreground/session observers, data paths, single instance, login startup, and language preferences.
- `src/FocusOn/`: the shared Avalonia desktop interface, timeline, and tray.
- `tests/`: core/platform regressions, headless UI tests, and native X11 smoke checks.

Product rules and work items are maintained in the [specification](.scratch/focuson/spec.md) and [local issue tracker](.scratch/focuson/issues). See [ADR 0003](docs/adr/0003-cross-platform-desktop.md) for platform boundaries and the [verification record](tests/FocusOn.Tests/Verification.md) for actual results and remaining device checks.

## License

Original FocusOn code and accompanying original documentation are licensed under the [MIT License](LICENSE), copyright 2026 Zhongkai Lau (Liuzkai). Copies or substantial portions must retain the copyright and full license notice. Attribution in the application interface is not required.

Third-party components and content retain their own licenses and notices. The .NET runtime, Avalonia, SkiaSharp, Inter fonts, Microsoft.Data.Sqlite, SQLite, and test dependencies are not relicensed by this project's MIT license.
