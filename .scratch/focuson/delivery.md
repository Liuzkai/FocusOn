# 15: v0.1.2 UI interaction draft (2026-10-12)

The three requested fixes are implemented at immutable tag/package source `8f25193fa09ec7672c197a80c4679eb0d4d5df44`: pointer-following non-interactive timeline hints with cleanup, log-end following that pauses for history/user scrolling, and small different-app separators without changing time/duration/hit testing. [v0.1.2 draft](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-e5e1022863366939ef6e) contains four Windows/Linux x64/ARM64 archives and four SHA256 files. [Release workflow](https://github.com/Liuzkai/FocusOn/actions/runs/38237429087) is Success: all six native targets, source gate, package checks and draft upload pass; the explicit-publication job is skipped. Uploaded attachments were downloaded and SHA256 compared by the publisher. v0.1.0 remains intact; public/latest v0.1.1 stays at 45ad974 and is not rewritten or replaced.

Local Release has zero warnings/errors; 90 core/platform + 17 UI regressions pass (107, zero failed/skipped). Actual Linux/Xvfb pointer and rendered-UI checks confirm hint movement/re-entry/cross-lane updates, appended-record following, history viewport stability and software-only pixel gaps. Detailed fixture/screenshot provenance is in tests/FocusOn.Tests/Verification.md and docs/ui/focuson-review.md; it uses synthetic records and no production data. Native build checks reuse the existing desktop controller/SQLite/instance/language/close/exit verification on each architecture. Physical Windows/macOS pointer/DPI/multi-monitor and other 08 acceptance remains pending.

| Draft archive | Bytes | SHA256 |
| --- | --- | --- |
| [FocusOn-linux-arm64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-e5e1022863366939ef6e/FocusOn-linux-arm64.tar.gz) | 45121775 | 4E40AED7B97937E50235AA3B3FF3DC0E55008A0EB661B5FB84CA3A48B4A9C17B |
| [FocusOn-linux-x64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-e5e1022863366939ef6e/FocusOn-linux-x64.tar.gz) | 47759063 | E432763125D0272E8DC5D813E26D2B1AD259392B1820D3C947AD6AA0D880B387 |
| [FocusOn-win-arm64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-e5e1022863366939ef6e/FocusOn-win-arm64.zip) | 47204990 | A413245EE1BC41D944005A0B24FA77208741EA4553DBAD2E4E7C74DA36F6A075 |
| [FocusOn-win-x64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-e5e1022863366939ef6e/FocusOn-win-x64.zip) | 49599424 | EB4202454E64CA816525CA90D77193D15E0089C6A847C893FCF6EFB8CB844803 |

Both Mac native targets build/test/package successfully, but missing Developer ID/notary trust leaves `osx-x64` and `osx-arm64` excluded from Release attachments. Mac validation ZIPs remain seven-day [Actions artifacts](https://github.com/Liuzkai/FocusOn/actions/runs/38237429087); no formal Mac readiness or physical acceptance is claimed. Existing Apple Secrets/permission instructions and third-party notices are preserved. Main receives this delivery record after the immutable tag; it does not change package source. About remains manual/user-owned.

# 14: Public v0.1.1 publication (2026-10-12)

User explicitly requested “0.1.1 直接发布吧”. [v0.1.1](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.1) is now public/latest, published at 2026-10-12T17:36:18+08:00 (Asia/Shanghai). [Publication workflow](https://github.com/Liuzkai/FocusOn/actions/runs/38230937502) is Success. Publication tooling source is f962ff5; package/immutable tag source remains `45ad974fc3f86c9721b78da560361a77d1e0809a`, from the already successful six-target [native build](https://github.com/Liuzkai/FocusOn/actions/runs/38228221117). No rebuild, tag rewrite or asset replacement occurred.

All four Windows/Linux x64/ARM64 archives and their four SHA256 files were checked again before publication: immutable tag/source, successful native jobs, exact sizes/digests, archive manifests and checksum contents. After publication the cloud downloaded all eight public URLs without owner credentials; every size/SHA256 and all four package manifests match the verified original draft. Existing v0.1.0 tag and asset ID/size/digest snapshots remain unchanged. Mac ad-hoc validation outputs are excluded; Developer ID/notarization/physical permission and device acceptance are still pending with 08.

Publication tooling's nine offline guard cases pass, including refusal on tag/build/job/asset/source/download/Mac-attachment mismatches and an idempotent already-public case. actionlint, Python compilation and diff checks pass. New version builds still default to drafts; ordinary code/document pushes do not publish. Explicit approval is recorded in `.github/release-publication/v0.1.1.json`; future manual publication requires a matching approval record. About remains manual/user-owned.

# 13: Icon/residency update and native draft pipeline (2026-10-12)

Continuation on main baseline 12f482c, no duplicate project/worktree. User approved a separate v0.1.1 draft. Existing published v0.1.0 remains: Windows tag/source 43d6dfd and Linux source 409cb51. About is user-managed manually.

Integrated Windows executable/window/tray icons, Linux hicolor/desktop entry, Mac ICNS/menu bar template and Dock Reopen; recoverable Linux tray fallback; resident recording/timers; explicit exit cleanup and activation before secondary GUI startup. English README includes updated real English/Chinese screenshots; language preference and existing databases/notice bytes are preserved.

Pre-release native source `140d540e8832640b222a50334141ee94edf5ea57`: [six-target run](https://github.com/Liuzkai/FocusOn/actions/runs/38226659404) succeeds. Each native standard Windows/Linux/Mac x64/ARM64 runner passes 102 regressions, native desktop checks and architecture/runtime/archive/SHA256 checks. Both Mac targets actually observe positive foreground Usage and start/quit the self-contained app bundle. This does not replace physical power/TCC/menu/DPI checks in 08. Mac reports absent Apple credentials, ad-hoc validation only; no Developer ID/notarization/stapling claim and no Mac formal Release attachment. Secure seven-Secret configuration is documented in docs/macos.md. Final tag/source/upload/download results follow below.

## Final v0.1.1 draft result

- Immutable tag/package source: `45ad974fc3f86c9721b78da560361a77d1e0809a`. [Release workflow run](https://github.com/Liuzkai/FocusOn/actions/runs/38228221117) is **Success**: all six native targets, source gate, receipt and draft upload completed. Each target passed 102 regressions (90 core/platform + 12 UI, zero failed/skipped), native desktop checks and self-contained archive verification.
- [FocusOn v0.1.1 draft](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-9e6e4d5dd19f594f38a2) contains four Windows/Linux archives plus four SHA256 files. Draft visibility/downloads require the repository owner's authenticated account. The publisher independently rechecked every package against the exact tag, downloaded every uploaded attachment and compared its SHA256, and confirmed all v0.1.0 asset IDs/sizes/digests remained unchanged.

| Draft archive | Manifest files | Bytes | SHA256 |
| --- | --- | --- | --- |
| [FocusOn-win-x64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-9e6e4d5dd19f594f38a2/FocusOn-win-x64.zip) | 245 | 49594605 | EE292367DE1F232C0910F357EA24EC07DFB1E16B16D3E950D605D37C329B462B |
| [FocusOn-win-arm64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-9e6e4d5dd19f594f38a2/FocusOn-win-arm64.zip) | 245 | 47200190 | C04617DEE6E02924F363D8E25D4FF6EF842AA3CCCF1C2D29ECFF13A4857AB287 |
| [FocusOn-linux-x64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-9e6e4d5dd19f594f38a2/FocusOn-linux-x64.tar.gz) | 257 | 47752480 | 058DD1697CDD3501C2D87D571A41FF08129AA812874C87FB7E070033117B53C5 |
| [FocusOn-linux-arm64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-9e6e4d5dd19f594f38a2/FocusOn-linux-arm64.tar.gz) | 257 | 45119708 | 58010FBF7F22C7562CF6E42FF17A67203E77A61406C0EB7DFF2BC48EAA5B4DE0 |

- Mac Intel/Apple Silicon bundles from the same commit: native foreground usage (Accessibility trusted), menu bar/close/restore, final extracted self-contained .app startup/SQLite/native Quit and normal exit pass. Ad-hoc deep/strict code signatures and final ZIP/file/architecture checks pass. Both `mac-signing.json` reports explicitly say `credentialsConfigured: false`, `notarized: false`, `stapled: false`, `formalReady: false`. Mac artifacts (`FocusOn-osx-x64`, `FocusOn-osx-arm64`) remain [seven-day Actions validation downloads](https://github.com/Liuzkai/FocusOn/actions/runs/38228221117), excluded from Release attachments; this is not Developer ID signing or a notarized distribution.
- Secure follow-up: existing Apple Developer Program membership, a valid Developer ID Application certificate/private key, and notarization API access. Configure the seven Secrets at Settings → Secrets and variables → Actions, following docs/macos.md; do not send private material in chat. Physical fixed-app Accessibility grant/revoke, Dock/menu input, lock/sleep/display, Windows WTS/DPI/offline installation and GNOME/KDE tray/Wayland remain with 08/manual acceptance. About remains user-managed manually.
- Final delivery records are a later documentation commit; the immutable v0.1.1 tag stays at the source above. MIT notice matches remote d21c09f byte-for-byte; pre-existing third-party notices match task baseline 12f482c. No production database or existing release asset was changed.

# FocusOn 第一版工程交付

## 当前 GitHub Linux 发布（2026-10-12）

[v0.1.0](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.0) 已公开发布，按用户选择追加 Linux x64/ARM64，原 Windows 附件保留。Linux 源提交为 `409cb51ed57f79637cfbca6691480aa5bb6a3a69`，与旧 Windows 标签指向的 `43d6dfd901d708ce27d13fcfb691e3061f66c405` 不同；标签没有移动，发布说明及包内 manifest 均记录 Linux 源提交。

[Linux Release run 38203441571](https://github.com/Liuzkai/FocusOn/actions/runs/38203441571) 全部成功，用时 1m57s，包含 Release 构建、99 项核心/平台/界面回归、原生虚拟 X11/跨进程验证、两架构正式打包、独立校验、上传与回传下载核对。初次运行 38202692331 在查询草稿的 tag endpoint 遇到 404；改为列表定位 Release ID 后本次成功，没有覆盖 Windows 资产。

| 公开 Linux 附件 | 清单文件数 | 归档字节 | SHA256 |
| --- | --- | --- | --- |
| FocusOn-linux-x64.tar.gz | 244 | 46835098 | 36DC66BF3EC12A2E459047AF0D18C3194393A812D9381C0F7DC2B471AA5E8D2B |
| FocusOn-linux-arm64.tar.gz | 244 | 44169273 | 5A81564DEA6F212DCD0F6C7F12FEFD0A9A3E7997DDBFB2694D6E3C22ABC8E1AB |

每个归档均有公开的 `.sha256` 附件，包含自有 MIT LICENSE、英文 README、平台说明、第三方声明与 GNOME bridge。清单不含自身，归档另含 manifest。客户端从公开链接重新下载四个附件，与 GitHub 资产 digest/大小一致；逐文件大小/哈希、文件集合、归档内容/SHA256、入口/CoreCLR/SQLite/Skia ELF 架构、执行权限和自包含运行时独立验证再次全部通过。下载保存在忽略的 `.tools/github-linux-v0.1.0/`，回执在 `.tools/github-linux-release-result.json`。

Windows ZIP 保持资产 ID `632512486`、大小 `77388363`、SHA256 `ABF032649026E773D96DD530E0D427B26703181A4D83800D1A5D3185E328F803`；校验附件 ID `632512487`、87 字节、digest `50b809bb2fa52e558a365e95b42b3e4e863a13d231392e6c754dd8233360a392`。上传前后快照与已知 Windows 验证记录一致，没有替换旧 Windows 二进制。

公开下载的 Linux x64 包实际启动、中英文即时切换、独立计时继续/暂停、偏好保存及正常退出 0 通过；设置 DOTNET_ROOT 为不存在的 SDK 路径仍可运行，实际使用随包运行时。隔离 SQLite 重开得到演示计时 603.3514426 秒。ARM64 为交叉构建，未在 ARM64 机器执行；真实 logind、GNOME Wayland、电源、DPI、Windows/macOS 硬件与离线无 SDK 设备验收仍待人工完成。

英文 README 与双语源码已推送。About 设置实际被 GitHub 403 拒绝，默认工作流令牌缺少 Administration 写权限；英文配置在 `.github/about.json`，用户已选择手动填写 About，不再请求 ABOUT_TOKEN 或执行元数据工作流。外部 About 由用户处理，12 保持 ready-for-human；08 与整体规格不因此关闭。

## 11：跨平台开发验证交付（2026-10-11）

当前基准 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad` 加未提交工作区，Linux x64 云环境 / SDK 10.0.401。Windows 原数据目录及 SQLite 兼容；统一 Avalonia 桌面和三平台观测/自启/单实例已经接入。使用与平台要求见 `docs/usage.md`、`docs/platforms.md`。

六架构使用 `bash build.sh package-check <runtime>` 生成在忽略的 `artifacts/validation/`，逐文件清单标记 `buildKind: development-validation`、`sourceCommit: null`。每包有完整目录、归档、SHA256 校验；它们不属于正式已提交发布。

| 归档 | 清单文件数 | 归档字节 | SHA256 |
| --- | --- | --- | --- |
| FocusOn-win-x64.zip | 232 | 49292456 | 170EDC3B4906CAA316080F5B1D1A1B23811B1AE326528BC00136BA665B3E43A6 |
| FocusOn-win-arm64.zip | 232 | 46898057 | 718BA1A55DF77E921160511382FF420A52A0A1B9FD148E96DBDEDCEF14279DBB |
| FocusOn-linux-x64.tar.gz | 233 | 46797500 | 8BC072594B6DFEF8D9B3FE4D74B6FE01351E49E7626268601C66118CC1DD1192 |
| FocusOn-linux-arm64.tar.gz | 233 | 44125204 | D75E88BCDD7EDCD10A07F0E5F65B6D559388F4AE85D24957487DE3EDE2E39B3A |
| FocusOn-osx-x64.tar.gz | 233 | 49452541 | 3A839C2848E09547B4ADB6AFC2E47D6A66FAF058B19FDD98EC5D85B60FDCCF8D |
| FocusOn-osx-arm64.tar.gz | 233 | 47045287 | 5233F1213D43E4EF07CF2E2D2C44DE17660DE3253F4A2A09A9E2A9F04703F775 |

清单不含自身，归档额外包含 `manifest.json`。独立重算文件和归档内容、大小、SHA256，检查入口/CoreCLR/SQLite/Skia（macOS 另含 Avalonia native）的目标 PE/ELF/Mach-O 架构、自包含 runtimeconfig/deps、Unix 入口执行权限、macOS app 的 Info.plist 和图标，六包全部通过。报告在忽略的 `.tools/package-verification.json`。

Release 构建零警告/错误，87 项核心/平台 + 7 项界面回归通过；Linux 原生虚拟 X11、真实第二进程和最终包的鼠标/键盘标题计时/退出保存验证通过。安装脚本与可选 PowerShell 入口在此机器执行成功。正式 `publish` 仍拒绝未提交源，不能将上述归档冒充正式发布。提交并复核后可用 `./build.sh publish <runtime>` 或 `./build.ps1 publish -Runtime <runtime>` 正式生成 `artifacts/release/`。

macOS 包为 FocusOn.app，Linux 包附 GNOME bridge，Windows 为完整 EXE 目录；无需另行安装 .NET 运行时，但仍需要各平台图形库/桌面权限。Linux 跨构建未执行 macOS codesign；原生 macOS 发布步骤会 ad-hoc 签名并验证，不含开发者签名或公证。

GitHub Actions 三系统回归/六架构打包定义已添加，本轮没有运行 CI。Windows/macOS、GNOME Wayland、实际 logind 和 ARM64 启动/硬件行为待按人工清单验收。11/08 与规格未关闭，代码未提交或推送。后续段落保留历史 Windows 交付记录。

2026-10-10，Windows 11 Pro x64。01–07工程票已关闭；08为 `ready-for-human`，整体规格尚未通过完整实机验收。

## 本地文件（历史发布 7f2ec0e）

- `artifacts/FocusOn-win-x64/FocusOn.exe`：从完整文件夹运行；普通首次启动会设置当前用户登录自启。
- `artifacts/FocusOn-win-x64.zip`：完整文件夹ZIP，77,371,844字节，约74MiB。
- `artifacts/FocusOn-win-x64.zip.sha256`：ZIP校验。
- `artifacts/FocusOn-win-x64/manifest.json`：479个运行/说明文件的逐文件大小和SHA256；清单自身另包含在ZIP中。
- 使用说明：`docs/usage.md`；人工步骤：`docs/manual-acceptance.md`。

产物不进入Git；源代码、构建脚本、合成旧库测试数据和验收记录已在本地版本管理。

## 版本与校验（历史发布 7f2ec0e）

- 实现提交：`e0c55c5`；工程票关闭及发布源提交：`7f2ec0e3f738492f37c0bbd037346426f7d3e24b`。
- 以干净工作树执行 `./build.ps1 publish`，Windows x64自包含发布成功；运行时为.NET 10.0.12，包含.NETCore及WindowsDesktop，无需另装SDK。
- 必需WPF、WinForms、CoreCLR和SQLite文件及x64依赖目标检查通过。
- 独立重算所有479个文件哈希与大小，并核对ZIP解压流中的逐文件哈希及manifest本体：通过。
- FocusOn.exe、coreclr.dll、e_sqlite3.dll的PE架构均为AMD64。
- ZIP SHA256：`1D353D89171E464C37E3FCDC94BC234F9F1CA408D2F8943797C9A90A54F130C7`。
- Release完整回归66项通过，0失败/跳过；编译零警告。Standards／Spec最终静态复核无遗留问题。

发布源提交不包含本文件和发布后追加的验收记录；这些后续修改仅记录交付结果，没有修改程序源码。

## 待人工验收

本轮没有启动发布GUI、修改实际用户自启或操作物理电源，文件校验不能代替W10离线运行。W01–W03仍有专项缺项，W04锁屏/睡眠/灭屏、W05登录/托盘、W06真实WPF异常终止与故障界面、W07完整时间线交互、W08键盘/DPI/高对比、W09删除交互以及W10断网/无SDK启动均按清单继续。已有基础采集证据与剩余分项见verification.md。

Modern Standby在历史机器固件上不支持，不能声称兼容测试通过。正常调度下约五秒检查点之间的未保存尾段可因崩溃丢失；Windows通知遗漏或延迟仍是已知限制。

完成待验收分项并记录真实结果后，再判断08与整体规格是否可关闭。上述发布阶段没有向GitHub推送提交或上传发布产物；后续推送历史见 Agent.md。

## 当前工作副本首次重新构建与交付（2026-10-10，历史源 1629462）

- 目录：`H:\CodebuddyWork\FocusOn`；环境：Windows 11 专业版 x64，版本 `10.0.26200`，时区 `China Standard Time`。
- 构建与发布源：`16294624d55d12485eac85e6c32ff72ba78bb698`。发布时工作树干净；本节和本轮验收记录在发布后追加，程序源码没有修改。
- 从[微软官方发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)取得 SDK 10.0.401 的 Windows x64 ZIP，在 `.tools/dotnet/` 解压。下载 SHA512 与官方值一致：`24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430`。没有替换系统 SDK。
- 实际执行 `./build.ps1 build`：Release 构建通过，0 警告、0 错误。`./build.ps1 test`：66 passed、0 failed、0 skipped；测试运行使用仓库 SDK 对应的 `DOTNET_ROOT` 和 `DOTNET_ROOT_X64`。
- 实际执行 `./build.ps1 publish`：Windows x64 自包含发布通过；包含 Microsoft.NETCore.App 和 Microsoft.WindowsDesktop.App 10.0.12，发布脚本必需文件检查通过。
- 当前程序：`artifacts/FocusOn-win-x64/FocusOn.exe`，须保留完整文件夹。当前 ZIP：`artifacts/FocusOn-win-x64.zip`，77,371,878 字节；SHA256：`884FE9DCD11AC5C70ABD3A248391313E3CE6302A4EA160631EA4B1C55F9A8D8F`，与旁置 `.sha256` 一致。
- 独立校验通过：479 个清单文件的大小和 SHA256、实际文件集合、清单源提交、ZIP CRC、ZIP 文件集合及逐文件内容（含 manifest 本体）；FocusOn.exe、coreclr.dll、e_sqlite3.dll 的 PE 架构均为 AMD64。
- 本地产物证据（不进入 Git）：`artifacts/qa-20261010/build.log`、`test.log`、`publish.log`，以及 `artifacts/delivery-verification.json`；独立校验脚本为 `.tools/verify_delivery.py`。
- `powercfg /a`：当前机器支持 S3，不支持 S0 低电量待机（Modern Standby）；Modern Standby 子项不适用，实际 S3 睡眠/恢复仍未执行。
- Windows 界面工具初始化成功，但 `sky.list_windows()` 首次、重试、重置后重试均失败：`Computer Use native pipe is unavailable: failed to connect native pipe: 系统找不到指定的文件。 (os error 2)`。本轮没有启动 FocusOn GUI，没有操作正式数据库、自启项或物理电源，也没有进行断网/无 SDK 环境运行。
- 08 保持 `ready-for-human`，整体规格保持开放。下一步恢复 Windows 界面工具连接或按 `docs/manual-acceptance.md` 在隔离数据目录执行 W01–W10；文件完整性通过不能视为 W10 实机通过。

## 历史发布：Harness 截图版 UI（2026-10-10）

- 发布源 `18d499f176ff99dae5ec57adf603b885414181a3`，包含 09 UI 实现、筛选/高对比度审查修复、深色滚动条及最新使用说明；干净工作树执行发布。此前相同本地路径的源 1629462 包已由本包替换，旧哈希仅为历史证据。
- 当前完整文件夹：`artifacts/FocusOn-win-x64/`，运行入口 `FocusOn.exe`；ZIP：`artifacts/FocusOn-win-x64.zip`，77,380,219 字节。
- ZIP SHA256：`33D153BB226BB960F470397F9EEF5A5C7FF8A0203F5DA4E12F6E0094D9DC7135`。旁置 `.sha256`、479 个清单文件的哈希/大小/实际集合、ZIP CRC/文件集合/逐文件内容与 manifest 本体均独立核验通过；三个关键 PE 仍为 AMD64，包含 Core/Desktop 10.0.12。
- 本轮最终 Release 构建 0 警告/错误，测试 66 passed、0 failed/skipped。日志：`artifacts/ui-harness/build.log`、`test.log`、`preview.log`、`publish.log`；文件核验结果 `artifacts/delivery-verification.json`。
- UI 预览为真实 WPF 控件的合成数据离屏渲染，见 `artifacts/ui-harness/overview-start.png`、`details.png`、`narrow-details.png`、`search.png`、`no-match.png` 和 `overview-200.png`；最后一项只表示 2 倍像素输出，不代表真实 Windows DPI 验收。
- 09 工程票关闭。桌面工具仍报 native pipe 缺失（os error 2）；没有操作正式用户活动库、自启、电源或网络，也没有在发布 GUI 上执行 W01–W10。08 和整体规格保持开放。此次新增提交仅在本地，没有推送或上传包。

## 历史发布：记录范围与标题计时（2026-10-10）

- 源 `c8cbd8a80e0254e9f0dc36bc3cb9c2e8dd8b552d`，干净工作树执行 `./build.ps1 publish -PackageName FocusOn-win-x64-timer`。实现 7d7dc34，修复 c8cbd8a；最终 Release 构建 0 警告/错误，79 项测试通过、0 失败/跳过，两轴复核无遗留。
- 最新完整目录 `artifacts/FocusOn-win-x64-timer/`，入口 FocusOn.exe；ZIP `artifacts/FocusOn-win-x64-timer.zip`，77,395,469 字节，SHA256 `CE39DEE04936B3AC1680EB2EE9FB4134067B95CCA229F26468695A126DF41CDB`。
- `python .tools/verify_delivery.py FocusOn-win-x64-timer`：479 清单文件大小/哈希/实际集合、manifest 源提交、ZIP CRC/集合/逐文件内容与旁置 SHA256 全部通过；FocusOn.exe/coreclr.dll/e_sqlite3.dll 均 AMD64，包含 Core/Desktop 10.0.12。结果 `artifacts/FocusOn-win-x64-timer-verification.json`。
- 合成 WPF 预览在 artifacts/ui-timer/，检查两模式、增长、缩放保持、计时控制状态及搜索；没有运行真实采集或操作用户库。旧目录的源18d499f仍以 PID11676 运行，保持旧目录不覆盖；新版尚未替换旧实例，升级先从旧托盘退出。
- 10 工程票关闭，真实鼠标/键盘、DPI、高对比度、锁屏/睡眠及离线无 SDK 运行仍交08，未执行不记通过。上节未推送/未启动为当时阶段事实；09后已按用户要求正常启动旧包，并推送 main 至5e1876e。

## 本地发布：最新 main 重新编译（2026-10-12）

- 发布源 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad`，发布前工作树干净，与 origin/main 同步。使用仓库 SDK 10.0.401；没有修改程序源码，本节和 Agent.md 更新在发布后追加。
- 实际执行 `./build.ps1 build`：Release 构建成功，0 警告、0 错误；`./build.ps1 test`：79 passed、0 failed、0 skipped。
- 实际执行 `./build.ps1 publish -PackageName FocusOn-win-x64-20261012-3e1d78f`：Windows x64 自包含发布成功，发布脚本运行时和必需文件检查通过。
- 完整目录 `artifacts/FocusOn-win-x64-20261012-3e1d78f/`，入口 `FocusOn.exe`；ZIP `artifacts/FocusOn-win-x64-20261012-3e1d78f.zip`，约 73.8 MiB。SHA256：`B59F79A610EC6A9064F39D8DC2FC4DAA603DC348007F24C69F71BFB904DE1CB1`。
- 独立 PowerShell 核验通过：manifest 源提交、479 个清单文件的大小和 SHA256、480 个 ZIP 文件（含 manifest）的逐项内容哈希、ZIP 哈希与旁置 .sha256；FocusOn.exe、coreclr.dll、e_sqlite3.dll 的 PE 架构均为 AMD64。
- 旧目录 `artifacts/FocusOn-win-x64/FocusOn.exe` 的 PID 11676 仍在运行，新包使用独立目录。此次未启动新版，也未执行界面、硬件或断网验收；08 与整体规格的人工验收状态保持不变。升级时先从旧版托盘退出，再启动新目录程序。

## GitHub Release 自动化配置（2026-10-12）

- 用户授权配置仓库端发布。新增 `.github/workflows/release.yml` 和跟踪的 `scripts/verify_delivery.py`，复用现有 build.ps1，标签 `v*` 触发 Windows 构建、测试、发布、文件校验及草稿附件上传；允许选择已有标签手动重跑。分支运行跳过发布，公开 Release 不覆盖。
- 通过 GitHub API 核对仓库 Actions 已启用、允许官方 Actions；默认令牌为只读，发布 job 显式申请 contents: write，使用内置 GITHUB_TOKEN，无需保存新的 Secrets。
- 官方 checkout v7、setup-dotnet v6 均固定当前对应提交。实际执行 actionlint 1.7.12：无错误；`python scripts/verify_delivery.py FocusOn-win-x64-20261012-3e1d78f`：源提交、479 文件、ZIP CRC/集合/逐项内容、SHA256、运行时与 AMD64 校验通过。
- 配置提交 `43d6dfd901d708ce27d13fcfb691e3061f66c405` 已推送 origin/main；首个注释标签 `v0.1.0` 已推送并触发 [GitHub run 38198854147](https://github.com/Liuzkai/FocusOn/actions/runs/38198854147)，release job 用时 1m24s，全部步骤通过。
- 云端 Release 构建：0 Warning(s)、0 Error(s)；测试 79 passed、0 failed、0 skipped。云端跟踪的校验脚本对 479 文件、ZIP CRC/集合/逐项内容、SHA256、运行时与 AMD64 检查通过。
- [v0.1.0 Release 草稿](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-1f0e09351dfa32edc5eb) 已创建并补充中文功能、下载、升级和验证说明，包含 `FocusOn-win-x64.zip`（77,388,363 字节）和 `.zip.sha256`。API 核对 isDraft=true、tagName=v0.1.0，两个附件均 uploaded。
- 草稿 ZIP SHA256：`ABF032649026E773D96DD530E0D427B26703181A4D83800D1A5D3185E328F803`，与 GitHub 资产 digest 和旁置 SHA256 一致；下载后再次核对 manifest 源提交、479 文件集合/逐文件大小与哈希、ZIP CRC、运行时及三个 AMD64 文件，全部通过。
- 本地云端日志 `.tools/github-release-v0.1.0.log`，下载附件 `.tools/release-v0.1.0/`，均被忽略。草稿尚未公开，后续由用户检查后 Publish release；人工验收与整体规格状态不因此关闭。

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

Immutable source `c0938e72afc53a063fc26e0ecc399e8d37b4e398`, annotated tag `v0.1.5` (object `36e83a964f4801aa0d47183ec9a9bc3d399d898d`). [Build/draft](https://github.com/Liuzkai/FocusOn/actions/runs/38342256279) Success: six native standard hosted targets each pass 104 core/platform + 28 UI regressions, zero failed/skipped, native desktop and package/source/runtime checks. [Draft and downloads](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-915e2b6097b83a60c8a3) contains eight Windows/Linux archive/checksum attachments, independently uploaded/downloaded SHA256 verified. Source reached main before the tag.

The selector replaces the old Recorded range / full-local-day 24h modes with 1h, 2h, 4h, 6h, 8h, 12h, 24h and Today, default 1h. Hours are elapsed lookbacks across midnight; Today is local midnight to now. Historical dates use their day end, while choosing Today returns to the current date. One existing recorder-queue snapshot reads necessary calendar dates and joins original daily parts without duplicate IDs or lost duration ticks. Visible timeline/log scope follows the preset, manual zoom/pan stays fixed on refresh and across midnight, and Reset restores live following. Daily totals and deletion remain scoped to the date picker. Search/selection preserves recorded durations; previous timers, language preference, hints, gaps, colors, compact label dragging, log following and residency remain.

Native Linux synthetic-record mouse/keyboard checks confirm all seven elapsed-hour spans and Today, unchanged daily total labels, cross-day versus Today logs, pointer hints, frozen manual zoom on append, Reset and log-end following. English/Chinese screenshots and an open selector are in README. No personal database or collector is used in those screenshots.

| Architecture | Archive bytes | SHA256 | Download |
| --- | ---: | --- | --- |
| win-x64 | 49617303 | `CF165FF2CCA62D864B45EB6138F75CCFCA3A9081DC8C0A62D1A2076532F3210B` | [FocusOn-win-x64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-915e2b6097b83a60c8a3/FocusOn-win-x64.zip) |
| win-arm64 | 47222850 | `9B92AE1FBB1F6A2FC3E71260232A358260D3D4163D9E2C79ECD2E8095E209B9A` | [FocusOn-win-arm64.zip](https://github.com/Liuzkai/FocusOn/releases/download/untagged-915e2b6097b83a60c8a3/FocusOn-win-arm64.zip) |
| linux-x64 | 47773611 | `FA7FBE91021E44EAF3E428FA94163FC105936AD8FA69E1E3594E59A47C3A4819` | [FocusOn-linux-x64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-915e2b6097b83a60c8a3/FocusOn-linux-x64.tar.gz) |
| linux-arm64 | 45138111 | `5F16C75F771F54E35C490070CEF441362C3B97824E91D6A1A6DEB055C2F81C94` | [FocusOn-linux-arm64.tar.gz](https://github.com/Liuzkai/FocusOn/releases/download/untagged-915e2b6097b83a60c8a3/FocusOn-linux-arm64.tar.gz) |

Every earlier Release metadata/asset snapshot remains unchanged; cached v0.1.4 archive/checksum digests also match. All old remote tag objects/source commits, latest MIT license and third-party notices remain intact. Public v0.1.1 and all earlier drafts/downloads keep their original source and bytes. v0.1.5 remains a draft.

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

## 2026-10-14: Configuration and installation delivery

- 2026-10-14: Reviewed and committed implementation `9760d1af0ffd9065572045cf26654668d0cabb54`; immutable v0.1.7 object `a8416ef0c4031855d81915be183cdebab13a3c18`. Main run38502933413 and tagged https://github.com/Liuzkai/FocusOn/actions/runs/38503521655 Success: six native targets each160 tests (124core/platform+36UI), all native/package checks; Win x64/ARM64 actual installation/installed executable/save/exit/same-version reinstall/uninstall and record/preference preservation pass. https://github.com/Liuzkai/FocusOn/releases/tag/untagged-b45cf62abc599a74a3f8 contains12 uploaded/downloaded SHA256-verified archives/installers/checksums. Actual isolated Linux UI save/copy/restart and indirect alias rejection verified; all old Release metadata/assets/tags and original notices preserved, new Inno upstream notice included. Main documentation follows source tag without retagging. Windows unsigned; Mac two native targets ad-hoc-validation/no credentials/notary/staple/formalReady and excluded from Release; physical/privacy/power/DPI/old-WPF install acceptance stays with08. Public/latest v0.1.5 and original v0.1.6 draft unchanged. Engineering complete.

## v0.1.8 Records and startup delivery

- 2026-10-14: Reviewed/committed source `a8791ee1c85296152fad009e6e90535ae382186c`, immutable tag object `7d4e5769b56651696b8388431cb9200789008c93`; https://github.com/Liuzkai/FocusOn/actions/runs/38507488287 Success, six targets each160 tests and native/package checks, both Win installer lifecycle checks pass. https://github.com/Liuzkai/FocusOn/releases/tag/untagged-1db24c6c1255826cdf26 has12 uploaded/downloaded SHA256-verified assets. Native two-language Records/startup visibility/off-on preference restoration screenshots checked; real sign-in acceptance remains08. All older releases/assets/tags/notices and public/latest v0.1.5 preserved; Mac ad-hoc/no credentials/notary/staple, excluded and physical trust still pending. Engineering closed; documentation is later than source tag.


## v0.1.9 update-check recovery delivery

Source `e3a9cff1e930e8b6070218694b0a1f33130eabe4`, immutable tag `v0.1.9` / object `19ee76f0f83e9d388c734aabc9adce90ee42793d`; [native build](https://github.com/Liuzkai/FocusOn/actions/runs/38511486384) Success. All six native targets pass 179 tests each and package/source/runtime checks; Windows x64/ARM64 installed-app save/exit, reinstall/uninstall and record/preference preservation pass. [Draft release](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.9) contains 12 uploaded/downloaded-verified Windows/Linux archive, installer and checksum assets.

API 403/429, service/transport failures and API-only timeout can recover through the same official repository's public stable Release redirect/assets. Exact headers-only size and GitHub digest/checksum agreement remain required before installation. Successes cache briefly and limited API calls back off; two-channel failures explain retry/manual access. No GitHub token, mirrors or recording uploads. Real cloud API-denied native Check, Chinese/English status, cache and preserved public Linux archive SHA have been validated; details are in verification.md and ADR0009.

**One manual upgrade:** installed v0.1.7/v0.1.8 cannot acquire this code repair through their failing old checker. Download the matching Setup.exe and adjacent .sha256 from the draft, explicitly Quit FocusOn (including the tray), verify the checksum, then install. The existing per-user installation preserves records/preferences outside the program folder. Public/latest remains v0.1.5 and drafts are intentionally invisible to automatic stable checks. This draft is not a public release. Native installation/reinstallation is tested; actual older-version upgrade and user-network acceptance remain ticket 08.

| Package | Bytes | SHA256 | Official draft download |
| --- | ---: | --- | --- |
| FocusOn-linux-arm64.tar.gz | 45245972 | `42E08DDA9ED294E684CB224241765085A097190F3D4F85B550442DDF8974A7A0` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-linux-arm64.tar.gz) |
| FocusOn-linux-x64.tar.gz | 47878307 | `019A96C837F7F985CD59AB7D47C174015227098BD2A2FE4CAE330C3AF9C80561` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-linux-x64.tar.gz) |
| FocusOn-win-arm64-Setup.exe | 33214698 | `901B44CE6163E7CD76E115CD5A25B18CE7C0D150FCFF5C4E282505D62962EA8E` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-win-arm64-Setup.exe) |
| FocusOn-win-arm64.zip | 47309168 | `1945A86FD9553FDC304CD7B0574FF6856FBE273E192EB9827A4101C9067B8D6A` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-win-arm64.zip) |
| FocusOn-win-x64-Setup.exe | 36158224 | `910B37EF843B520DC655A0952E43281CA96200AD9BD5EA4C1ABAFB0FD8FC700A` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-win-x64-Setup.exe) |
| FocusOn-win-x64.zip | 49704355 | `7FF79FD0F92F62DF9F3FC19B24E1B2E072CCEC55A7037902BC80B47BEC8D1606` | [Download](https://github.com/Liuzkai/FocusOn/releases/download/v0.1.9/FocusOn-win-x64.zip) |

Every older tag, Release metadata/asset snapshot, existing Windows attachment, original latest MIT license and third-party notice remains intact. Windows installers are unsigned. Both Mac architectures pass native validation but have no Developer ID/notarization/stapling/formal readiness and are excluded from Release; their Actions validation artifacts expire after seven days. Existing secure Apple setup instructions remain docs/macos.md. Physical device/permission/power/DPI and parent acceptance remain open. Final documentation follows the immutable source tag.


## 2026-10-14: v0.1.9 public publication

User explicitly replied “发布” to the verified v0.1.9 repair draft. Publication maintenance `c208e9058909246611dafbbf7822e470d1b285b5` / [run](https://github.com/Liuzkai/FocusOn/actions/runs/38521909276) Success makes [v0.1.9](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.9) public/latest. The package source remains `e3a9cff1e930e8b6070218694b0a1f33130eabe4`, immutable tag object `19ee76f0f83e9d388c734aabc9adce90ee42793d` and all twelve original attachment sizes/digests stay identical. The workflow rechecked the successful six-target native build38511486384 (179 tests per target), every download/checksum/source manifest and all historical release/tag snapshots before and after publication. No rebuild, asset replacement or tag rewrite.

All twelve public downloads were independently rechecked in this cloud: exact size/SHA256, six package checksum filenames/digests, four archive manifests and the public/latest redirect to v0.1.9 match. Windows x64/ARM64 Setup.exe and ZIP plus Linux x64/ARM64 tar.gz are public, with adjacent .sha256 files. Rate-limited API-only old clients require one manual installer upgrade; the repaired checker can recover via official public Release pages. Current Windows installers are unsigned; macOS remains native ad-hoc validation only, excluded without Developer ID signing/notarization/stapling. Physical old-to-new upgrade/network/privacy/power/DPI and08/parent specification stay open.

The subsequently requested statistics hover animations use a fresh v0.1.10 draft. Future feature builds still default to drafts; no older feature draft is published by this authorization.
