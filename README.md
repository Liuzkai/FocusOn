# FocusOn

Windows、Linux、macOS 的个人前台软件使用记录工具。以当前激活窗口为时间归属，保留窗口与标题切换；没有键鼠输入仍计时。数据仅保存在当前用户本机，标题计时独立保存，不计入软件使用汇总。

## 界面预览

![FocusOn 界面：标题计时、全天软件汇总、三行时间线与日志](docs/images/focuson-ui.png)

Linux 实际截图，记录为演示数据；黄色提示来自云环境缺少真实桌面会话。

## 使用

统一桌面界面支持每日软件汇总、使用/停止/计时三行时间线、日期回看、搜索/拖选/缩放/详情、按天删除和标题计时。

- Windows：完整解压后运行 `FocusOn.exe`。
- Linux：在桌面会话运行 `./FocusOn`；支持 X11，GNOME Wayland 需要启用随附扩展。
- macOS：打开完整 `FocusOn.app`，授予辅助功能权限后重新启动。

普通首次启动配置当前用户登录自启；关闭窗口继续记录，使用窗口或托盘的「退出」停止。重复启动唤回已有实例。Linux 保留最小化的任务栏入口。

平台版本、依赖、权限、数据路径、Wayland 桥接和迁移步骤见[平台说明](docs/platforms.md)，详细操作见[使用说明](docs/usage.md)。Windows 原有 `%LOCALAPPDATA%\FocusOn` 数据与 SQLite 格式保持兼容。

## 开发

需要 `global.json` 指定的 .NET SDK 10.0.401。工程使用 .NET 10、Avalonia 11.3 和 Microsoft.Data.Sqlite；安装与首次恢复需要联网，应用运行不需要联网。SDK 可安装到系统，或解压到忽略的 `.tools/dotnet/`。

Windows PowerShell：

```powershell
./build.ps1 build
./build.ps1 test
./build.ps1 publish -Runtime win-x64
```

Linux / macOS：

```bash
./build.sh build
./build.sh test
./build.sh publish linux-x64   # macOS 使用 osx-x64 或 osx-arm64
```

`test` 执行真实临时 SQLite 的核心/平台回归和 Avalonia 无显示器界面测试，不需要 Windows 桌面运行时。Linux 原生前台检查可在隔离的 X11 桌面执行：

```bash
xvfb-run -a ./build.sh smoke
```

`publish` 要求干净且已提交的工作区，生成 `artifacts/release/FocusOn-<runtime>/`、完整归档、逐文件 SHA256 清单与归档校验值。支持 `win-x64`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`。macOS 交付为 app bundle；Unix tar 包保留执行权限。

开发时用 `./build.sh package-check <runtime>` 或 `./build.ps1 package-check -Runtime <runtime>` 检查未提交的实际工作区，产物写入 `artifacts/validation/`，清单明确标为开发验证，不冒充正式发布源版本。发布名称可指定第三个 shell 参数或 PowerShell 的 `-PackageName`。

隔离运行避免写正式数据和自启：

```bash
dotnet run --project src/FocusOn/FocusOn.csproj -- --data-dir /tmp/focuson-qa
```

仓库 SDK 需将 `.tools/dotnet/` 加入 PATH 或直接使用其中的 `dotnet`。Windows 隔离目录可使用 `$env:TEMP` 下的测试路径。

## 工程与验收

- `src/FocusOn.Core/`：记录会话、真实 SQLite、每日投影、标题计时；与桌面平台无关。
- `src/FocusOn.Platforms/`：各系统前台与会话观测、数据路径、单实例和自启。
- `src/FocusOn/`：统一 Avalonia 界面、时间线与托盘。

规格与任务保存在 [.scratch/focuson/spec.md](.scratch/focuson/spec.md) 和 [11 全平台票](.scratch/focuson/issues/11-cross-platform.md)。平台边界见 [ADR 0003](docs/adr/0003-cross-platform-desktop.md)。实际测试证据见[验证记录](tests/FocusOn.Tests/Verification.md)，Windows/macOS/Wayland 的真实窗口、权限、锁屏、睡眠、托盘和 DPI 等操作仍须[人工验收](docs/manual-acceptance.md)。
