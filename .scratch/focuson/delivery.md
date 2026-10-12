# FocusOn 第一版工程交付

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
