# 11：Windows、Linux、macOS 桌面支持

**What to build:** 按用户新要求扩展为跨平台桌面工具，复用记录规则与数据库，统一界面并适配各平台前台采集、系统状态、单实例、自启和交付。

**Blocked by:** 10（closed）

**Status:** ready-for-human

## Acceptance criteria

- 核心在 net10.0 运行；旧 SQLite 夹具及原回归通过，Windows 数据目录兼容。
- 统一桌面界面支持每日汇总、三行时间线、搜索/范围/缩放/详情、日期删除确认、记录暂停、独立标题计时。
- Windows 原生观测保留；Linux X11 原生观测；macOS 前台应用/辅助功能窗口观测。Wayland GNOME 提供显式安装桥接及缺失提示，不伪造权限。
- 跨平台单实例、独立数据目录与自启，关闭窗口可恢复，退出保存固定终点。
- 发布 Windows、Linux、macOS 的自包含产物和 macOS app 结构，支持 x64/ARM64 架构。
- Linux 实际构建、核心/平台/界面回归、真实虚拟 X11 前台/标题/采集及桌面启动验证；Windows/macOS 原生接口与交互逐项记录待对应设备验收。

## Comments

- 2026-10-11：用户明确扩大平台范围；ADR 0003 优先于原第一版的 Windows-only 范围。当前云机器 Linux x64，Windows/macOS 不标记为实机通过。
- 2026-10-11：基准 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad` 加当前未提交工作区。完成 net10.0 核心拆分、Avalonia 统一界面、三平台观测/单实例/自启、GNOME 可选桥接和六架构打包/CI 定义。原有 79 项回归保留，加 8 项平台/缺口检查共 87 项通过，7 项界面检查通过；真实虚拟 X11 标题切换/窗口失效/SQLite 重开、跨进程激活及 Linux 桌面按钮输入通过。详细证据见 `tests/FocusOn.Tests/Verification.md` 与 `.scratch/focuson/delivery.md`。
- 2026-10-11：Windows/macOS 与 GNOME Wayland 原生接口未在对应设备执行，真实 logind、锁屏/睡眠、多屏、系统托盘和 DPI 验收保持未执行，交 `docs/manual-acceptance.md` 的跨平台清单。其他 Wayland 桌面仅支持界面/独立计时/历史，前台采集明确不可用。GitHub Actions 定义已添加，本轮没有触发或声称 CI 通过；代码未提交，票保持开放，正式 publish 保留干净源检查。
