# Windows、Linux、macOS

2026-10-11 起使用统一的 .NET 10 / Avalonia 桌面应用。记录会话与旧 SQLite 数据兼容，运行时无需网络。平台支持实现与实机验收结果分开记录，证据见 `tests/FocusOn.Tests/Verification.md`。

| 平台 | 交付架构 | 前台采集 | 权限与依赖 |
| --- | --- | --- | --- |
| Windows 11（以及 .NET 10 支持的 Windows 10 版本） | x64 / ARM64 | WinEvent 窗口/标题事件、WTS 与电源通知 | 当前用户桌面会话；受限应用可能显示未知软件 |
| Linux（例如 Debian 13、Ubuntu 22.04/24.04，glibc） | x64 / ARM64 | X11 EWMH；GNOME Wayland 使用随附桥接 | X11、fontconfig 等图形库；systemd-logind；Wayland 需启用扩展 |
| macOS 15 或更新的 .NET 10 支持版本 | Intel x64 / Apple Silicon ARM64 | NSWorkspace / Accessibility 窗口；IOKit 电源通知 | 系统设置中授予辅助功能权限 |

不提供 musl/Alpine、32 位或无桌面会话的前台采集。其他 Wayland 桌面（如 KDE/Sway）没有通用窗口枚举权限：仍可运行界面、标题计时和回看，前台采集显示不可用；可切换到桌面提供的 X11 会话。不会把 XWayland 可见的部分窗口视为全部原生 Wayland 活动。

## 安装与启动

- Windows：完整解压 `FocusOn-win-x64.zip` 或 `FocusOn-win-arm64.zip`，将文件夹放在固定位置，运行 `FocusOn.exe`。
- Linux：解压对应 `.tar.gz`，在图形桌面运行文件夹中的 `./FocusOn`。压缩包保留执行权限；若文件由其他方式复制失去权限，运行 `chmod +x FocusOn`。Debian/Ubuntu 常用图形依赖为 `libx11-6 libxext6 libice6 libsm6 libfontconfig1 libgl1`；会话检查使用 `loginctl`、`busctl`（systemd 提供）。
- macOS：解压对应 `.tar.gz`，将完整 `FocusOn.app` 移至 `/Applications` 或其他固定目录，双击启动。在「系统设置 → 隐私与安全性 → 辅助功能」允许 FocusOn，然后重新启动。程序不申请屏幕录制权限。原生 macOS 发布会执行并验证 ad-hoc 签名；跨平台构建不包含开发者签名或公证，系统阻止首次打开时按 macOS 的「打开」流程处理，不关闭系统安全设置。

Windows/macOS 关闭窗口隐藏到托盘或菜单栏。Linux 关闭窗口保留最小化的任务栏入口以便没有托盘宿主的桌面恢复；采集和计时继续。所有平台也可再次启动程序唤回同数据目录实例，或在窗口/托盘明确「退出」保存并结束进程。

## GNOME Wayland 前台授权

随 Linux 包附带 `gnome/focuson@focuson.local/`，源码在 `platforms/gnome/`。扩展只通过当前用户的会话 D-Bus 提供激活窗口的编号、进程、应用名、标题和会话状态，不读取屏幕内容。

```bash
mkdir -p ~/.local/share/gnome-shell/extensions
cp -R gnome/focuson@focuson.local ~/.local/share/gnome-shell/extensions/
```

注销并重新登录（Wayland 无法通过重启 Shell 加载新扩展），然后启用：

```bash
gnome-extensions enable focuson@focuson.local
```

支持 GNOME Shell 45–50。若扩展被禁用、版本不兼容或会话总线不可达，界面给出权限提示；启用成功后自动恢复读取。其他本机同用户应用也能访问用户启用的 D-Bus 桥接，符合本机权限边界，不是跨用户/网络服务。

## 数据和自启

| 平台 | 默认数据目录 | 普通首次启动注册的自启入口 |
| --- | --- | --- |
| Windows | `%LOCALAPPDATA%\FocusOn` | 当前用户 Run 键 `FocusOn` |
| Linux | `$XDG_DATA_HOME/FocusOn`，未设置时 `~/.local/share/FocusOn` | `$XDG_CONFIG_HOME/autostart/focuson.desktop`，默认 `~/.config/autostart/` |
| macOS | `~/Library/Application Support/FocusOn` | `~/Library/LaunchAgents/org.focuson.app.plist` |

同一目录的 `.focuson.lock` 是常驻锁文件，不应在进程运行中删除。不同目录可独立记录。自启仅注册固定的 FocusOn 发布可执行文件，不能把 `dotnet` 或 IDE 注册为应用；移动程序后普通启动更新位置。`--data-dir <目录>`（可在前面加 `--startup`）隔离验证不注册自启。

首行语言下拉框支持 English 与简体中文，切换即时生效，不重启记录或计时；偏好保存在当前数据目录的 `settings.json`。首次启动跟随系统中文语言，否则默认英文。软件名、窗口标题、用户计时标题与持久历史不会被翻译。配置保存失败保留原语言并显示错误。

旧 Windows 版本使用原来的默认数据目录直接升级；升级前明确退出旧托盘进程。数据库格式没有跨平台改写，可离线迁移：退出程序后复制整个数据目录中的 SQLite 文件（包括存在的 WAL/SHM），新平台以 `--data-dir` 指向副本验证。迁移不会合并多台机器的使用时长；历史时间仍以当前选择的本地时区投影。

## 采集精度与验证边界

Windows 保留原事件观测和五秒补查。Linux/macOS 前台采样间隔为 200ms，系统状态约 500ms；两个样本之间的更短切换可能遗漏，不声称重建。超过两秒的采样停顿保留无法确认的范围为未记录，标题计时仅保留已保存时间并暂停；唤醒需手动继续标题计时。macOS 睡眠通知会在确认睡眠前保存；Linux 额外查询 logind 的睡眠状态，未及时收到事件的休眠段保守处理为空缺。

Linux 使用 CLOCK_BOOTTIME、macOS 使用 mach_continuous_time，让单调计时包含休眠；收到明确睡眠通知的范围保留睡眠状态，不误当成系统时钟跳变或未知采样缺口。

2026-10-11 当前云机器是 Linux x64：已执行核心/平台与 Avalonia 界面自动化、虚拟 X11 的真实窗口和标题观测及桌面启动。云容器没有真实 logind 登录会话，因此主程序显示无法确认会话状态，这不等于真实 Linux 桌面的锁屏/睡眠验收通过。Windows/macOS 和 GNOME Wayland 仍需按 `docs/manual-acceptance.md` 在对应桌面验收；跨编译产物不表示原生接口已实机通过。
