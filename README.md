# FocusOn

Windows 11 个人前台软件使用记录。以激活窗口为时间归属，保留窗口与标题切换；没有键鼠输入仍计时。数据仅在当前 Windows 用户本机保存。

## 使用

2026-10-10 最新标题计时版本：`artifacts/FocusOn-win-x64-timer/FocusOn.exe`，完整 ZIP 为 `artifacts/FocusOn-win-x64-timer.zip`。旧版正在运行，因此使用独立交付路径；从旧托盘退出后，再启动新版。

自包含交付目录为 `artifacts/FocusOn-win-x64`，压缩包为 `artifacts/FocusOn-win-x64.zip`。完整解压到固定位置，再运行 `FocusOn.exe`；首次普通启动会配置当前用户登录自启。关闭窗口后仍在托盘记录，停止程序请从托盘明确退出。

详细操作和本地数据位置见[使用说明](docs/usage.md)。[人工验收清单](docs/manual-acceptance.md)记录发布程序的 Windows 操作步骤；本轮未执行的硬件／界面检查仍是待验收。

## 开发

需要 `global.json` 指定的 .NET 10 SDK（当前为 10.0.401）。可从[微软官方下载](https://dotnet.microsoft.com/download/dotnet/10.0)获取 Windows x64 SDK，安装到系统，或将 SDK zip 解压到 `.tools/dotnet/`。仓库内 SDK 与 NuGet 缓存不进入 Git。

在仓库 PowerShell 中运行：

```powershell
./build.ps1 build
./build.ps1 test
./build.ps1 publish
```

开发环境第一次恢复依赖需要联网；运行程序不需要联网。测试用真实临时 SQLite，经公开记录会话验证结果。测试主机需要本地回环通信，受限沙箱可能阻止 VSTest 启动。

publish 会检查桌面运行时与 SQLite 必需文件，生成逐文件 SHA256 清单、ZIP 和 ZIP 校验文件。文件完整性检查不替代在没有开发工具的机器上断网启动验收。

旧包正在运行时，可用 `./build.ps1 publish -PackageName FocusOn-win-x64-timer` 发布到独立目录，避免覆盖运行文件。升级时先从旧版托盘退出，再从新目录普通启动；相同数据目录的重复启动会定位旧实例。

隔离实机测试数据，可运行构建程序并传入 `--data-dir <测试目录>`；默认数据目录为 `%LOCALAPPDATA%\FocusOn`。开发运行需对应桌面运行时，最终交付使用包含运行时的 x64 文件夹。

`--startup --data-dir <测试目录>` 可隔离验证隐藏托盘启动；这两种隔离入口都不修改正式自启项。

## GitHub Releases

`.github/workflows/release.yml` 在推送 `v*` 标签时自动执行 Release 构建、测试、Windows x64 自包含打包和文件校验，再创建带 ZIP 与 SHA256 附件的 Release 草稿。SDK 从 `global.json` 安装，使用 GitHub 自动提供的令牌，无需新增个人 Token 或 Secrets。

先提交并推送准备发布的代码，再创建新版本标签，例如：

```powershell
git tag -a v0.1.1 -m "FocusOn v0.1.1"
git push origin v0.1.1
```

在仓库 Actions 查看 `Windows Release`；成功后到 Releases 检查草稿、填写版本说明并点击 Publish release。草稿附件可以通过重新运行该工作流更新；已经公开的版本应使用新标签发布。也可在 Actions 的 Run workflow 中选择已有 `v*` 标签重跑，选择分支不会创建 Release。

下载完整 `FocusOn-win-x64.zip` 并解压运行，旁置 `.sha256` 用于校验。GitHub 自动生成的 Source code 附件是源码。自动化构建和文件校验不代替 `docs/manual-acceptance.md` 的实机验收。

## 规格与任务

完整行为见[开发规格](.scratch/focuson/spec.md)，依赖与验收条件见[独立任务](.scratch/focuson/issues)。测试证据区分可控事件逻辑与真实 Windows/界面行为，不把模拟事件通过当作硬件兼容性通过。

## 许可

本仓库中 Liuzkai 的原创代码及其原创配套文档采用 [MIT 许可](LICENSE)，允许使用、复制、修改和商业分发；分发软件副本或实质性部分时，须保留版权声明及完整许可声明。无需在产品界面署名。第三方组件或内容仍遵循各自的原许可，相关版权及许可声明必须保留。

.NET 桌面运行时、Microsoft.Data.Sqlite、SQLite 及测试依赖不因本项目采用 MIT 而改变许可；再分发自包含程序时应保留随这些组件提供的声明。
