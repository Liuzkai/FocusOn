# 记录会话测试证据

测试边界是公开 `RecordingSession` 输入和 `GetDay` 回看结果，使用可控 UTC/单调时间与真实临时 SQLite。测试不读取表字段或私有状态。

- 2026-10-09 T01 red：`Short_foreground_switch_is_saved_and_reopened_with_daily_totals` 实际失败于 `Observe` 的 `NotImplementedException`，1 failed，0 passed。
- 默认沙箱内 VSTest 无法完成 testhost 的本机回环握手；本地沙箱外测试执行已获自动批准，不需要联网。
- T01 首次 green 运行暂被并行主窗口文件尚未就绪阻止（`MainWindow` CS0246），不是测试通过。
- T01 green：主窗口就绪后测试通过，1 passed。真实保存并重开得到编辑器 10 秒、微信 2 秒、编辑器 10 秒，汇总 20 秒与 2 秒。
- T02 red：标题变化未分段，第二片段错误结束于第 12 秒（预期第 8 秒）。补齐标题边界后 2 passed。
- T03 red：`Checkpoint` 尚未实现。补齐检查点和公开实时投影后 3 passed；实时 20 分钟显示不会将未保存尾段写入数据库，另一会话只能读取已保存 10 分钟。
- T06 red：无法确认前台时错误报告“正在记录”。补齐未记录与软件/标题回退后 4 passed。
- 最终命令：`.tools/dotnet/dotnet.exe test tests/FocusOn.Tests/FocusOn.Tests.csproj --no-restore --verbosity minimal`，2026-10-09 实际结果 4 passed、0 failed、0 skipped，编译零警告。

这些结果覆盖受控事实进入后的记录行为，不证明 Windows 原生事件、窗口标题读取、多显示器或电源行为已经实机通过。跨日投影、停止条件、异常恢复和删除在后续票补齐。

## 02：停止条件

- T04 手动暂停 red：`SetConditions` 尚未实现，实际 1 failed。green：解锁和暂停期间前台变化都不取消手动暂停，恢复用最新前台；原有四项加此项共 5 passed。
- T04 睡眠/锁屏 red：预期“睡眠”，实际“正在记录”。green：唤醒后锁屏仍停止，解锁后从实际前台恢复；连同初始跨日检查共 7 passed。
- T05 重叠原因 red：预期“屏幕已关闭”，实际“正在记录”。已补齐会话灭屏，首次 green 编译被并行 App 的未赋值字段 CS0649 阻止；等待主窗口控制接入后统一验证，不视为通过。
- 合作接入 04 的 `Bounds`/`Clip`：跨日 T08 与四项基本记录及首条 T04 共 6 passed；04 的其余回归随后一起运行。
- T05 green：主窗口控制接入后，全部公开记录会话测试 15 passed、0 failed、0 skipped，编译零警告。其中 02 有 3 个停止条件场景，04 有 8 个跨日保存、实时投影和停止类型场景，基本记录 4 个场景。

Windows 状态源与手动按钮的真实交互由主代理另行验收；这些受控事实测试不能代替真实锁屏、睡眠、灭屏或 Modern Standby 检查。

- 02 审查回归 red：晚到的系统快照携带旧 `ManualPaused=false`，通过整体条件替换后，预期“手动暂停”却得到“正在记录”。
- 审查回归 green：新增独立手动控制与系统控制入口；系统入口始终保留会话当前手动暂停，只有手动入口能主动清除。Release 完整测试 16 passed、0 failed、0 skipped，编译零警告。命令在前述 `dotnet test` 上加 `-c Release`，未触碰仍运行的 Debug 程序。

## 04：DST 午夜回归

- red：Cuba 2026-03-08 缺失午夜抛 ArgumentException；2026-11-01 重复午夜选择了第二次午夜，前一日多累计一小时。
- green：相邻日期使用共享边界；缺失午夜取首个有效时刻，重复午夜取最早 UTC。Release 完整18项通过、0 failed／skipped，编译零警告。没有更改系统时钟或操作 GUI。
