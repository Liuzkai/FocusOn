# 13: Icon/residency update and native draft pipeline (2026-10-12)

Continuation on main baseline 12f482c, no duplicate project/worktree. User approved a separate v0.1.1 draft. Existing published v0.1.0 remains: Windows tag/source 43d6dfd and Linux source 409cb51. About is user-managed manually.

Integrated Windows executable/window/tray icons, Linux hicolor/desktop entry, Mac ICNS/menu bar template and Dock Reopen; recoverable Linux tray fallback; resident recording/timers; explicit exit cleanup and activation before secondary GUI startup. English README includes updated real English/Chinese screenshots; language preference and existing databases/notice bytes are preserved.

Pre-release native source `140d540e8832640b222a50334141ee94edf5ea57`: [six-target run](https://github.com/Liuzkai/FocusOn/actions/runs/38226659404) succeeds. Each native standard Windows/Linux/Mac x64/ARM64 runner passes 102 regressions, native desktop checks and architecture/runtime/archive/SHA256 checks. Both Mac targets actually observe positive foreground Usage and start/quit the self-contained app bundle. This does not replace physical power/TCC/menu/DPI checks in 08. Mac reports absent Apple credentials, ad-hoc validation only; no Developer ID/notarization/stapling claim and no Mac formal Release attachment. Secure seven-Secret configuration is documented in docs/macos.md. Final tag/source/upload/download results will follow below.

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
