# Build and release instructions

The public `v0.1.0` release is historical and immutable: Windows source/tag `43d6dfd`, Linux source `409cb51`. Its original Windows ZIP and checksum and the appended Linux x64/ARM64 archives/checksums remain intact. GitHub tag source downloads refer to the older Windows source; each Linux manifest/release note states its actual source. The icon/residency update is the separate public [v0.1.1 release](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.1), published on explicit user approval after native verification. New version builds still default to drafts.

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

## Explicitly approved publication

Publishing an existing verified draft requires explicit user authorization recorded in `.github/release-publication/<version>.json`, with its immutable source, successful native build run and exact attachment sizes/SHA256 digests. The publication job checks the tag, all six native jobs, downloads the existing attachments, verifies every archive's source and checksum, and then changes only the release's visibility/notes. It does not rebuild or overwrite packages. The existing v0.1.0 tag/assets are checked before and after.

The v0.1.1 authorization record was added on the user's request, and that verified draft is now public. Only changes to authorization records trigger the publication job on main; ordinary code pushes do not publish. A maintainer can also manually select `publish_existing` with an existing approved `version_tag`. New version builds still create drafts by default, and unnotarized Mac validation packages remain excluded.

## 2026-10-12: v0.1.3 UI feedback draft

Immutable source `9c6afdb9febf4793764985bca8b9f91ca53fcf40`, annotated tag `v0.1.3` (tag object `392df6cf21700528ebb8dbc86391fac96711401e`). [Native build and draft](https://github.com/Liuzkai/FocusOn/actions/runs/38244571956) Success; all six standard hosted x64/ARM64 targets passed 90 core/platform + 21 UI tests each, with no failures/skips, native desktop checks and package verification. [Draft](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-4ffc94db1f1a690ca41f) contains eight Windows/Linux archive/SHA256 attachments, downloaded and checked by the release runner.

Changes: unobstructed daily-totals scrolling with horizontal offset retained on refresh; saved perceptually separated software colors with corrupt/unwritable preference fallback; right-click fits the selected range while preserving its filter, alongside existing wheel zoom, right-drag pan and left-click/drag selection. English/Chinese hints and README native synthetic-data screenshot updated. Linux real mouse checks and 144-color stability/contrast regressions passed. No production database or language settings were modified.

Initial draft creation returned GitHub 403 while the tag’s workflow changes had not yet been synced to main. After syncing main, [guarded retry](https://github.com/Liuzkai/FocusOn/actions/runs/38245549233) Success reran only the failed draft job; the original successful native jobs/artifacts and source tag were retained. Workflow maintenance commit `4ba2fce` adds committed exact-source requests, checks all six successful native jobs and restricts retry to a single failed draft attempt; eight offline mutation guards pass. Future release changes should reach main before their immutable version tag is pushed. No credential or tag rewrite was needed. Draft receipts now retain all six validation logs, and release creation checks every older Release’s metadata/attachment identity before and after.

| Architecture | Archive bytes | SHA256 |
| --- | ---: | --- |
| win-x64 | 49603992 | `A7B00BD9C2C664898E9C85B1C24C7F900164D40D1A939CA0EC76A9A78F87B587` |
| win-arm64 | 47209573 | `9E44AE90B76B1CDC2722A838CC31A16B490AB7810A5AB3F97B832E4B8B21489D` |
| linux-x64 | 47761685 | `87ADB56AEC304004CCAE91FA814064E8B2AEA28BFE14FE830EB2E735C1CB2DC3` |
| linux-arm64 | 45126444 | `FC605F32D864C72CB52994475254FC7054CEC2C4E25CE853D381ADD11FE52C2C` |

The new snapshot and saved earlier receipts confirm v0.1.1/v0.1.2 attachment sizes/digests unchanged; v0.1.0 metadata and its six attachments unchanged. All three remote tag objects, the latest MIT license and third-party notices remain intact. Mac Intel/Apple Silicon bundles pass native tests and actual .app/package checks but remain ad-hoc validation only, excluded from Release without Developer ID/notary credentials; temporary artifacts expire after seven days. Physical Windows DPI/monitor/input and prior platform power/permission/offline checks remain in ticket 08. The v0.1.3 draft has not been publicly published; public v0.1.1 remains unchanged.

## 2026-10-12: v0.1.4 compact totals draft

Immutable source `eafc2053e5377dfe52af00013e4d5975c75d2893`, annotated tag `v0.1.4` (object `ef5ef6c9593bf3d54a42b62e5813bcdb8e7217cc`). [Build/draft](https://github.com/Liuzkai/FocusOn/actions/runs/38254694792) Success; six native standard hosted targets each pass 90 core/platform + 23 UI regressions, no failures/skips, desktop smoke and package/source/runtime checks. [Draft](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-2e201aae9575a400ec4c) has eight Windows/Linux archive/checksum attachments, uploaded and downloaded SHA256 checks pass. Source reached main before the tag; no failed-job retry needed.

App totals now scroll by dragging any label, either wheel axis, keyboard arrows/Home/End/Page keys or the thin footer. Three-DIP line shows during input and hides after about one second idle; the six-DIP footer preserves complete chip padding and stable layout. Mouse capture, endpoint clamping, refresh stability and hide/deactivate/lost-capture cleanup are scoped to the totals. Existing data, color assignments, language, timeline gestures and log following retained. Native Linux synthetic-data checks confirm drag offsets 180→180 across refresh→240, wheel to 288, full chip bottom 188 versus indicator top 190, visible line RGB(160,162,168) returning to background RGB(34,34,34) after idle, with labels pixel-identical as the line hides. README image refreshed.

| Architecture | Archive bytes | SHA256 |
| --- | ---: | --- |
| win-x64 | 49608482 | `AADC30BC768410802D268132DDB243632F55F88EF4625D8FBC958A930430CEC7` |
| win-arm64 | 47214059 | `DEEFC2479DD445666F837958D4D003B6725FFE53B70197A057199F75BEA3EF32` |
| linux-x64 | 47765387 | `18A8303BCE7B19E96E23ED6857844ECD6C94CD0D04E23A8737CCD977E76252D6` |
| linux-arm64 | 45131114 | `116D801DE74A27330A9A117829CEDB7C781AD94A916997EA6AC3A7A979B5687A` |

Every older Release metadata/asset snapshot remained unchanged; saved v0.1.1/v0.1.2/v0.1.3 attachment sizes/digests also match. Existing remote tags, MIT license and third-party notices remain intact. Mac Intel/Apple Silicon .app bundles pass native tests/package checks but are ad-hoc validation only; excluded from Release without Developer ID signing/notary trust, seven-day Actions retention. Windows/macOS physical DPI, trackpad and input/device/power/permissions acceptance remains with ticket 08. v0.1.4 remains a draft; public v0.1.1 is not republished or replaced.
