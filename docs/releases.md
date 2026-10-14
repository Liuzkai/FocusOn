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


## 2026-10-13: v0.1.5 rolling review draft

Immutable source `c0938e72afc53a063fc26e0ecc399e8d37b4e398`, annotated tag `v0.1.5` (object `36e83a964f4801aa0d47183ec9a9bc3d399d898d`). [Build/draft](https://github.com/Liuzkai/FocusOn/actions/runs/38342256279) Success: six native standard hosted targets each pass 104 core/platform + 28 UI regressions, zero failed/skipped, native desktop and package/source/runtime checks. [Draft and downloads](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.5) contains eight Windows/Linux archive/checksum attachments, independently uploaded/downloaded SHA256 verified. Source reached main before the tag.

The selector replaces the old Recorded range / full-local-day 24h modes with 1h, 2h, 4h, 6h, 8h, 12h, 24h and Today, default 1h. Hours are elapsed lookbacks across midnight; Today is local midnight to now. Historical dates use their day end, while choosing Today returns to the current date. One existing recorder-queue snapshot reads necessary calendar dates and joins original daily parts without duplicate IDs or lost duration ticks. Visible timeline/log scope follows the preset, manual zoom/pan stays fixed on refresh and across midnight, and Reset restores live following. Daily totals and deletion remain scoped to the date picker. Search/selection preserves recorded durations; previous timers, language preference, hints, gaps, colors, compact label dragging, log following and residency remain.

Native Linux synthetic-record mouse/keyboard checks confirm all seven elapsed-hour spans and Today, unchanged daily total labels, cross-day versus Today logs, pointer hints, frozen manual zoom on append, Reset and log-end following. English/Chinese screenshots and an open selector are in README. No personal database or collector is used in those screenshots.

| Architecture | Archive bytes | SHA256 | Download |
| --- | ---: | --- | --- |
| win-x64 | 49617303 | `CF165FF2CCA62D864B45EB6138F75CCFCA3A9081DC8C0A62D1A2076532F3210B` | [FocusOn-win-x64.zip](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.5/FocusOn-win-x64.zip) |
| win-arm64 | 47222850 | `9B92AE1FBB1F6A2FC3E71260232A358260D3D4163D9E2C79ECD2E8095E209B9A` | [FocusOn-win-arm64.zip](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.5/FocusOn-win-arm64.zip) |
| linux-x64 | 47773611 | `FA7FBE91021E44EAF3E428FA94163FC105936AD8FA69E1E3594E59A47C3A4819` | [FocusOn-linux-x64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.5/FocusOn-linux-x64.tar.gz) |
| linux-arm64 | 45138111 | `5F16C75F771F54E35C490070CEF441362C3B97824E91D6A1A6DEB055C2F81C94` | [FocusOn-linux-arm64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.5/FocusOn-linux-arm64.tar.gz) |

Every earlier Release metadata/asset snapshot remains unchanged; cached v0.1.4 archive/checksum digests also match. All old remote tag objects/source commits, latest MIT license and third-party notices remain intact. Public v0.1.1 and all earlier drafts/downloads keep their original source and bytes. v0.1.5 was subsequently published on explicit user instruction; see the publication record below.

Both Intel and Apple Silicon Mac targets pass native tests, actual .app startup/Quit and package checks. Current receipts report ad-hoc-validation, credentialsConfigured=false, notarized=false, stapled=false and formalReady=false. Mac ZIP/.app validation artifacts expire after seven days and are excluded from Release attachments; no Developer ID signing/notarization/stapling success is claimed. Existing hardened-runtime/minimal-JIT/sign/notary/staple workflow awaits the securely configured Apple Secrets documented in docs/macos.md. Physical Windows/macOS/Linux DPI, input, trackpad, permissions, power and real-desktop acceptance remains pending with ticket 08.


## 2026-10-13: v0.1.5 public publication

User explicitly requested “发布0.1.5”. Publication maintenance `b36e077` / [run](https://github.com/Liuzkai/FocusOn/actions/runs/38371850633) Success publishes [public/latest v0.1.5](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.5) without rebuilding or replacing assets. Package source remains `c0938e72afc53a063fc26e0ecc399e8d37b4e398`, native build 38342256279 and all immutable tags remain unchanged. Version selection uses the uniquely changed approval record or an explicitly selected manual approved tag; ordinary pushes cannot publish. Fifteen publication/selection guards and actionlint pass. All older Release metadata/assets/tag objects are verified before and after publication, and original feature notes are retained. Eight public downloads were independently checked in the cloud: exact original size/SHA256, archive checksum contents and four release manifests match. Windows/Linux x64 and ARM64 are available. Mac remains ad-hoc validation only without Developer ID/notary/staple; physical permissions/power/DPI and ticket 08/parent specification remain open. The subsequently requested statistics page belongs to a new v0.1.6 draft, not this immutable release.


## 2026-10-13: v0.1.6 statistics draft

Immutable source `161c21f5b48e96701d812b2d80cf918b75acde56`, annotated tag `v0.1.6` (object `d11531a73290673fcd583a3cb328adc2e6dae6e1`). [Native build/draft](https://github.com/Liuzkai/FocusOn/actions/runs/38378437157) Success: all six standard native targets each pass148 regressions (114 core/platform +34 UI, no failures/skips), native desktop lifecycle checks and archive/source/runtime/architecture/icon verification. [Draft and downloads](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-29ffa82936509d03290e) includes eight Windows/Linux archive/checksum assets with independent upload/download SHA256 verification. Source reached main before the tag.

Adds a separate dark statistics page with app-share donut, rounded daily/hourly usage bars, patterned top-three trend lines and a complete app-total list. Inclusive multi-day dates default to the last seven, with 30-day shortcut and 366-date limit. Single-day local HH:mm periods accept24:00 as the day end. Only foreground Usage contributes; original monotonic allocations keep total/app/bucket sums exact after clipping, DST transitions and prior deletions. Empty/error/loading/applied-scope states, stable ISO dates, persistent validation feedback, high contrast, responsive layout, pointer hints and keyboard details are verified. Page changes preserve review zoom/selection and keep recording and titled timers running; global language/pause/Exit remain available. No schema migration or personal-data screenshot.

Local148 tests and native Linux synthetic-record mouse/keyboard checks pass: seven-day 137040 s/six apps/seven bins, thirty-day 608100 s,09:30–12:30 period 10800 s/four apps/three hour bins, empty period 0 s, whole-day 15420 s, invalid input retains last applied scope and its error across refresh, pointer hint moves 4 px, English/Chinese/navigation preserve scope. README embeds the actual native demonstration surfaces.

| Architecture | Archive bytes | SHA256 | Download |
| --- | ---: | --- | --- |
| win-x64 | 49653203 | `964D47C4D27DEF250AA9B5E80F5A11C55E751AB76DE2403DA7651A965D82B7F3` | [FocusOn-win-x64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-29ffa82936509d03290e/FocusOn-win-x64.zip) |
| win-arm64 | 47258775 | `4287C7793C982F1FAFAA5AA37A21B1B31AED8DE921BE941CED6A43FDC7FBBA36` | [FocusOn-win-arm64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-29ffa82936509d03290e/FocusOn-win-arm64.zip) |
| linux-x64 | 47814504 | `1A889607B5EEACA62921F4C82AECC6DE0016EBFC82C60D1E2825918215245B36` | [FocusOn-linux-x64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-29ffa82936509d03290e/FocusOn-linux-x64.tar.gz) |
| linux-arm64 | 45174779 | `11F7D0DCE922E2318C34F794E9B96CE87261D09CEEA9DBA3D9227120B207F79E` | [FocusOn-linux-arm64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-29ffa82936509d03290e/FocusOn-linux-arm64.tar.gz) |

All previous Release metadata/assets and remote tag objects/commits remain intact; the published v0.1.5 still uses c0938e7 and its original eight download digests. MIT and third-party notice bytes unchanged. v0.1.6 stays a draft; public/latest v0.1.5 is not rebuilt or replaced.

Native Intel/Apple Silicon macOS tests, foreground/.app startup/Quit and package checks pass, but signing receipts are ad-hoc-validation, credentialsConfigured=false, notarized=false, stapled=false, formalReady=false. No Developer ID/notarization/stapling success is claimed and no Mac bundle is attached to Release. Actions Mac validation expires after seven days. Securely configure the existing Apple Secrets described in docs/macos.md; no private material belongs in chat/repo/logs. Physical Windows/macOS/Linux permissions, power, DPI/trackpad and actual desktop acceptance remain pending with ticket 08; parent specification stays open.

## 2026-10-14: v0.1.7 settings, themes, installation and updates draft

Immutable source `9760d1af0ffd9065572045cf26654668d0cabb54`, annotated `v0.1.7` object `a8416ef0c4031855d81915be183cdebab13a3c18`. [Native main verification](https://github.com/Liuzkai/FocusOn/actions/runs/38502933413) and [tagged build/draft](https://github.com/Liuzkai/FocusOn/actions/runs/38503521655) Success. All six standard runners each pass160 regressions (124 core/platform +36UI, zero failed/skipped), native Settings/theme/About/residency/exit, architecture/runtime/icon/package verification. Windows x64/ARM64 also pass actual per-user install, installed executable startup/save/exit, same-version reinstall, uninstall and unchanged synthetic SQLite/preferences checks. Inno6.5.4 comes from its official versioned URL with pinned SHA256; generated installers are unsigned.

[Draft and downloads](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-b45cf62abc599a74a3f8) has12 verified Windows/Linux attachments (four portable archives, two Windows Setup.exe, six SHA256 files). Every asset was downloaded again by the release workflow and compared to the built bytes. Source reached main and passed all six native validations before the immutable tag was created.

| Asset | Bytes | SHA256 |
| --- | ---: | --- |
| FocusOn-linux-arm64.tar.gz | 45234599 | `8235293211a1d936eaf105df63ce517ee166032c46df97b723eedc86f2295d0b` |
| FocusOn-linux-x64.tar.gz | 47862922 | `9f2df68eae95e8f49ce18c71777faa476f5bd7409953a72852f02d81a7ee4679` |
| FocusOn-win-arm64-Setup.exe | 33206165 | `4021a1d4cae0850f9c51ff6e3b7caecac30069ee6276a050be527bf4ac943dda` |
| FocusOn-win-arm64.zip | 47300948 | `bef30e358d25a2ca51e93fe3aabb441328524c8edc41ae432c332aa2e8dcdba8` |
| FocusOn-win-x64-Setup.exe | 36163169 | `656e43ea4de26478e25d72a5c3a7964da1f60d15fc57b1a6c6c57b94f98839ca` |
| FocusOn-win-x64.zip | 49696146 | `f6fab5e45e0d32f724285e134f34e2744c66df31861207d56ef511fbb8fe6478` |


Settings moves saved English/Chinese out of the header, adds saved Light/Dark, high-contrast priority, managed login startup, open-folder and explicit copy/verify/restart data relocation. About is last with actual assembly version/process architecture/GitHub. Original data is kept, destination must be empty/distinct/exclusive, SQLite Backup includes WAL and integrity_check gates the atomic default pointer; isolated modes never modify production pointer/startup. Real isolated Linux UI timer/save/migration/restart preserved every original SQLite table/row and settings. Update checks are explicit, stable-public only and upload no records; Windows validates exact official URLs/architecture/size/SHA256 before save/exit/Setup, Linux/macOS use manual replacement. A draft is intentionally unavailable to the updater.

All older Release metadata/asset digests and v0.1.0–v0.1.6 tag objects/commits are preserved. Public/latest remains v0.1.5 source c0938e7 with all eight original assets; v0.1.6 remains its original statistics draft. Original MIT and existing third-party notice bytes are unchanged; the upstream Inno license is added. Main README/delivery records are later documentation, not a tag rewrite or package rebuild.

Mac Intel/Apple Silicon builds/tests/.app checks succeed, but both signing receipts report ad-hoc-validation, credentialsConfigured=false, notarized=false, stapled=false, formalReady=false. Mac bundles are excluded from Release and remain seven-day Actions validation artifacts. Securely configure the seven existing Apple Secrets in [Mac instructions](macos.md) for Developer ID/notary/staple; no signing or notarization success is claimed. Windows installers likewise have no Authenticode signature. Physical desktop/permission/power/DPI, old WPF-to-installer upgrades and downloaded-from-GitHub SmartScreen behavior remain pending with08/parent acceptance.
