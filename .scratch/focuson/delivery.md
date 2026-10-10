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

## 当前工作副本重新构建与交付（2026-10-10）

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
