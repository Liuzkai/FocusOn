# Build and release instructions

The public `v0.1.0` release is historical and immutable: Windows source/tag `43d6dfd`, Linux source `409cb51`. Its original Windows ZIP and checksum and the appended Linux x64/ARM64 archives/checksums remain intact. GitHub tag source downloads refer to the older Windows source; each Linux manifest/release note states its actual source. The icon/residency update uses a separate `v0.1.1` draft, confirmed by the user.

## Standard runner builds

| Runtime | Standard GitHub-hosted runner | Native check boundary |
| --- | --- | --- |
| win-x64 | windows-latest | Real desktop window/residency, SQLite/timers; physical WTS/power acceptance pending. |
| win-arm64 | windows-11-arm | Native ARM64 build and desktop smoke if runner succeeds; no Intel emulation claim. |
| linux-x64 | ubuntu-24.04 | Real X11 capture and two-process activation, no-tray fallback and isolated real D-Bus StatusNotifier host loss. |
| linux-arm64 | ubuntu-24.04-arm | Same checks on native ARM64; not merely a cross-compiled ELF. |
| osx-x64 | macos-15-intel | Native Intel GUI/clock/session/bundle startup and Quit; TCC/power hardware pending. |
| osx-arm64 | macos-15 | Native Apple Silicon checks; same permission/power limits. |

Only standard runners for free public-repository Actions are used. No large runners, paid services or developer-membership purchases are configured. Queue/runner availability can still prevent validation; an unrun or failed target is not labeled supported by that run.

Each job restores committed lockfiles, builds, runs core/platform and UI regressions, performs native checks, packages from a clean commit, checks the self-contained runtime, native architecture, icons, complete license notices, per-file inventory, archive bytes and SHA256. Mac jobs run on matching architectures. Validation evidence and archives are **Actions artifacts retained for seven days**; they expire and are not permanent Release downloads. `codex/desktop-status` stores a small public receipt for this cloud client's Git-only access, including run/job links and signing results; it contains no secrets and does not trigger another build.

## Draft release

1. Set `Version` in `Directory.Build.props` and commit all intended source, icon, docs and license changes. Use a fresh semantic version; do not retag old releases.
2. Create and push that version tag. Alternatively select the release workflow and manually enter an existing matching `version_tag`. The selected tag's application Version, resolved commit and every package manifest must agree.
3. The release workflow calls the same six native build jobs. All required tests and package checks must pass before upload. Mac release signing uses the existing account's seven [Apple Secrets](macos.md); missing bindings produce ad-hoc validation artifacts and exclude Mac Release attachments.
4. The upload job independently extracts and rechecks packages, verifies the remote immutable tag again, creates or uses a **draft**, uploads without clobbering existing assets, and downloads each attachment to compare SHA256. A same-named different archive fails rather than overwriting. Rerun with a clean new draft/version when rebuilt archive bytes differ.
5. Review source/run evidence, installation and real-device acceptance before public publication. The workflow never changes a draft to public on ordinary pushes. Even notarized Mac drafts remain pending physical Accessibility/foreground/lock/sleep/display/menu-bar acceptance.

Windows archives are ZIP; Linux archives are tar.gz preserving executable modes; Mac archives are ZIP containing `FocusOn.app`, the notarization status, README and license notices. No DMG is needed. Each archive has an adjacent SHA256. Release notes link the exact build source/run. Existing `v0.1.0` assets are snapshotted before/after a new draft upload.

The legacy Linux append workflow is manual, defaults to the already published `409cb51` source, and refuses to replace differing attachments. New feature versions use the unified desktop draft workflow. Repository About is managed manually by the user.
