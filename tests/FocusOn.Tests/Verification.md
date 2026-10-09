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
