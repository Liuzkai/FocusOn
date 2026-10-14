# Development

FocusOn uses .NET 10, Avalonia 11.3 and Microsoft.Data.Sqlite. Install the SDK pinned by [global.json](../global.json), currently 10.0.401. The repository scripts prefer `.tools/dotnet/`, then the SDK on `PATH`. Dependency restore uses committed lockfiles and requires network access.

## Build and test

Windows PowerShell:

```powershell
./build.ps1 build
./build.ps1 test
```

Linux / macOS:

```bash
./build.sh build
./build.sh test
```

Tests include core/platform regressions against real temporary SQLite databases and headless Avalonia UI checks. Use a separate data folder for development; `--data-dir <test-folder>` skips production login startup registration.

An isolated native X11 capture and activation check is available on Linux:

```bash
xvfb-run -a ./build.sh smoke
```

`desktop-smoke` additionally checks desktop lifecycle behavior. Native checks and automated regressions do not replace physical permission, power, DPI or input acceptance.

## Packaging

Formal packaging requires a clean, committed working tree:

```powershell
./build.ps1 publish -Runtime win-x64
```

```bash
./build.sh publish linux-x64
```

Supported runtime identifiers: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Use the matching native platform for release validation. Packages include the runtime, icons, notices, per-file manifest and archive SHA256 under `artifacts/release/`; Unix archives preserve executable permissions.

For uncommitted development checks, use `./build.sh package-check <runtime>` or `./build.ps1 package-check -Runtime <runtime>`. Outputs under `artifacts/validation/` are marked as development validation and are not formal releases.

The [release guide](releases.md) documents the six standard GitHub-hosted runners, Windows installer validation, immutable source/tag checks, seven-day Actions retention, guarded draft publication and preserved older assets. [macOS trust and permissions](macos.md) covers Developer ID, notarization and secure Secrets configuration. Missing Apple credentials exclude Mac attachments from Release.

## Source layout

| Path | Responsibility |
| --- | --- |
| `src/FocusOn.Core/` | Recording rules, SQLite persistence, projections, timers and display text |
| `src/FocusOn.Platforms/` | Native observers, data paths, single instance, startup, preferences, safe relocation and update checks |
| `src/FocusOn/` | Avalonia windows, timeline, statistics, settings and tray |
| `tests/` | Core/platform regressions, UI tests and native checks |

Start with [AGENTS.md](../AGENTS.md) and [Agent.md](../Agent.md) for repository conventions and current handoff. Product rules and work items live in the [specification](../.scratch/focuson/spec.md) and [local issue tracker](../.scratch/focuson/issues). [Architecture decisions](adr/) record platform and duration boundaries; [verification](../.scratch/focuson/verification.md) distinguishes automated checks from pending physical acceptance.
