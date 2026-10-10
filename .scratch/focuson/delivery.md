# FocusOn 第一版工程交付

2026-10-10，Windows 11 Pro x64。01–07工程票已关闭；08为 `ready-for-human`，整体规格尚未通过完整实机验收。

## 本地文件

- `artifacts/FocusOn-win-x64/FocusOn.exe`：从完整文件夹运行；普通首次启动会设置当前用户登录自启。
- `artifacts/FocusOn-win-x64.zip`：完整文件夹ZIP，77,371,844字节，约74MiB。
- `artifacts/FocusOn-win-x64.zip.sha256`：ZIP校验。
- `artifacts/FocusOn-win-x64/manifest.json`：479个运行/说明文件的逐文件大小和SHA256；清单自身另包含在ZIP中。
- 使用说明：`docs/usage.md`；人工步骤：`docs/manual-acceptance.md`。

产物不进入Git；源代码、构建脚本、合成旧库测试数据和验收记录已在本地版本管理。

## 版本与校验

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

Modern Standby在当前机器固件上不支持，不能声称兼容测试通过。正常调度下约五秒检查点之间的未保存尾段可因崩溃丢失；Windows通知遗漏或延迟仍是已知限制。

完成待验收分项并记录真实结果后，再判断08与整体规格是否可关闭。当前没有向GitHub推送提交或上传发布产物。
