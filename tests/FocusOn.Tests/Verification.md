# 记录会话测试证据

测试边界是公开 `RecordingSession` 输入和 `GetDay` 回看结果，使用可控 UTC/单调时间与真实临时 SQLite。测试不读取表字段或私有状态。

## 11：跨平台验证（2026-10-11）

环境：Debian 13 Linux x64 云容器、官方 SHA512 校验的 SDK 10.0.401。源为基准 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad` 加当前未提交工作区；以下结果属于本轮，后续章节保留历史 Windows 证据。

| 检查 | 实际结果 |
| --- | --- |
| 完整云安装脚本：SDK 版本、`dotnet restore FocusOn.slnx --locked-mode`、`bash build.sh build` | 通过，Release 零警告/错误；原有 WindowsDesktop 限制已由核心/桌面拆分解除 |
| `bash build.sh test` | 87 项核心/平台 + 7 项 Avalonia 界面，0 失败/跳过；保留原有 79 项记录/旧库/删除/计时回归 |
| 最终首行布局调整后的 UI 回归 | 7 passed，0 failed/skipped；TRX 在忽略的 `.tools/test-results/ui-final.trx` |
| `.tools/pwsh/pwsh -NoLogo -NoProfile -NonInteractive -File build.ps1 build` | 通过，PowerShell 7.6.6 在 Linux 执行，Release 零警告/错误 |
| 新建 Xvfb displayfd，`xdpyinfo` 就绪，`bash build.sh smoke` | 通过：真实第二进程写锁排他/激活；真实 X11 活动窗口、Unicode 标题变化、可执行文件身份、失效窗口拒绝；SQLite 保存并重开得到 3 条 Usage |
| 六架构 `bash build.sh package-check <runtime>` | 通过；逐文件与归档内容/大小/SHA256、PE/ELF/Mach-O 目标架构、.NET 自包含运行时、macOS app/图标、Unix 执行权限独立核验 |
| Linux x64 开发包实际 GUI | 通过：隔离数据启动、首行标题输入/开始/停止、正常退出（0）；重开 SQLite 只读核对 `Native desktop final` 已结束，持久计时 35.3765402 秒 |
| `bash build.sh publish linux-x64` | 预期拒绝未提交工作区（退出码 1）；没有绕过正式源版本检查 |

新增平台回归覆盖原生路径、隔离参数、Linux/macOS 自启文件转义、GNOME 桥接锁屏不泄漏前台元数据、可信采样缺口和文件系统大小写边界。实际慢窗口查询回归保证两秒以上不可观测范围不累计软件或独立计时。界面回归覆盖搜索不改变全天汇总、选择/缩放保持、计时按钮、缺权限历史回看、范围/键盘/全天总计、删除确认/取消和高对比度文字。

Linux 实际窗口截图：忽略的 `/tmp/focuson-cross-platform-final.png`、`/tmp/focuson-cross-platform-timer.png`、`/tmp/focuson-cross-platform-stopped.png`。核心与界面测试用独立真实 SQLite；桌面输入用 `.tools/qa-final-fm5mri/`，没有操作正式数据或注册登录自启。

未执行：真实 logind 会话与硬件锁屏/休眠/灭屏；Windows/macOS 原生观测、托盘、自启、DPI 与 ARM64 启动；GNOME Wayland 扩展实机执行；macOS 原生签名/公证；GitHub Actions 运行。本容器没有 logind，主程序正确提示无法确认会话状态，软件汇总为空，独立计时和回看可用。六包是工作区开发验证产物（sourceCommit 为 null），不是正式已提交发布。真实桌面验收见 `docs/manual-acceptance.md`，其他 Wayland 桌面的前台采集不在本实现支持范围。

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

## 03：可信保存与恢复

- T07 red：直接丢弃会话的未保存内存后重开，原实现缺少检查点终点至新启动之间的未记录区间，两个场景失败。green：旧使用或旧手动暂停均只保留已提交十秒，十至三十秒为未记录，新前台从三十秒开始；2 passed。
- T12 red：通过另一条真实 SQLite 连接持有写事务，使切换保存抛出 `SqliteException`；原会话仍报告“正在记录”。green：成功历史保留，异常有状态和文字，实时旧软件尾段丢弃，新鲜观测先保存缺口再恢复；实际写锁超时为一秒。
- T12 重试 red：第二次真实写锁失败使同一灰色缺口标识从 2 变为 3。green：仅在缺口提交成功后消耗标识，故障重试期间保持同一个公开记录标识。
- 晚前台回归 red：停止之后晚到的旧观测检查点抛出时间顺序异常。green：计时控制入口按单调时间忽略过时事实；锁屏及其边界保持，查询不推进该输入边界。
- 已知采集故障 red：`MarkUnreliable` 尚未实现。green：退回最后成功终点，旧使用和旧暂停之外均为未记录；故障时刻及更早快照不能解除故障，只有更晚的新鲜事实可恢复。
- T13 red：UTC 前跳/时区变化未提示，UTC 回拨抛出异常。green：三种输入都提示，已经提交的历史对象不被改写。UTC/单调时间差采用二秒工程容差，任何已观测 UTC 回拨也报告；时间警告与活动保存故障分离。
- T13 实时查询 red：时钟/时区变化没有警告，回拨查询抛错。green：异常实时查询只展示提交历史和警告；正常实时查询不挡掉稍后入队但更早已捕获的前台事实。
- 2026-10-10 最终 Release 全部公开会话测试：34 passed、0 failed、0 skipped，编译零警告。03 的 13 个场景均经过真实临时 SQLite；包含其他票已集成的每日投影、停止条件与范围筛选回归。

时间异常之后的历史日期映射不在第一版正确性承诺内。实际 WPF 程序杀进程及硬件挂起结果由主代理另行记录；本轮未操作桌面或结束运行中的 Debug 程序。

- 03 Spec 审查 P1 red：已知采集故障之后，更晚的 `Observe(null)` 清除了异常；旧使用、手动暂停与睡眠三个场景分别显示“未记录”“手动暂停”“睡眠”，预期均为“记录异常”，3 failed。green：故障期间的空前台事实不解除故障，保留检查点之后的未记录范围；只有更晚的实际窗口事实才恢复，三个场景通过。
- 显式退出 red：故障之后的 `Finish` 没有保存未记录范围，重开只有一条旧暂停，预期两条。green：退出保存成功终点至退出边界的未记录区间，软件和标题字段为空；保存成功不清除采集故障提示。
- 同一真实 SQLite 写锁场景还覆盖空前台轮询及退出保存失败：空轮询不恢复旧软件，退出仍抛出真实 `SqliteException`，公开缺口标识稳定，成功历史不变；释放写锁后新鲜前台正常恢复。
- 2026-10-10 修复后的 focused Release `RecoveryTests`：17 passed、0 failed、0 skipped，编译零警告。没有运行 GUI、终止 Debug 程序或提交 Git；完整集成回归由主代理统一执行。

## T02：窗口身份审查回归

- red：同一个 HWND、相同标题在不同 PID 中出现，原逻辑只有一个十秒使用片段。
- green：比较当前 HWND、PID 和标题，得到两个五秒片段；同 HWND/PID/标题重复事实仍不分段，软件汇总仍基于稳定软件身份，PID 不保存为软件身份。该项已计入上述 34 项完整结果。

## 04：DST 午夜回归

- red：Cuba 2026-03-08 缺失午夜抛 ArgumentException；2026-11-01 重复午夜选择了第二次午夜，前一日多累计一小时。
- green：相邻日期使用共享边界；缺失午夜取首个有效时刻，重复午夜取最早 UTC。Release 完整18项通过、0 failed／skipped，编译零警告。没有更改系统时钟或操作 GUI。

## 07：按天删除

- T10 tracer red：`DeleteDay` 尚未实现。green：跨三日单段删除中间一天后，两侧各一秒保留；单调总时长多出的一个 tick 仍保留在原来日期，重开删除日为空且没有故障提示。
- T11 red：删除当前日后，旧内存片段及排队标题被检查点重新写回。green：相交的当前片段先结束，删除成功后等待 `ResumeAfterDelete`；等待中只更新控制时间和条件，不生成片段。恢复起点之后的旧事实及同边界事实被拒绝。
- 持久删除洞 red：重开从前一日最后终点生成整天未记录，预期从删除完成时刻才有未保存缺口。green：删除范围同事务持久保存，恢复时扩展到该日内的完成边界；恢复缺口及其实时投影扣除这些范围。全部区间被删时，从删除范围终点重开。
- 回归覆盖原有恢复缺口跨过被删历史日：其他日期的未记录范围保留，所删日期为空；保存和再次重开均不补回。无关历史删除不结束当前其他日的片段，历史拆分产生较大标识也不改变真正最后可信终点。
- 五种停止条件回归：手动暂停、睡眠、锁屏、灭屏及采集不可用在删除后保留，只有对应条件解除才累计软件时长。已知故障后的空前台轮询继续保留异常，不沿用旧停止原因。
- 真实 SQLite 写锁覆盖删除事务失败、删除已提交但恢复边界保存失败：成功历史或已删结果均保留，实际抛出 `SqliteException`；恢复失败退出等待状态，之后的新鲜窗口事实可正常恢复，期间更新的手动暂停仍有效。
- 过时删除控制 red：旧删除边界早于最新检查点仍被执行。green：公开入口拒绝该边界，已有历史不变；生产调用在串行 worker 捕获删除时刻。
- 恢复竞态 red：较新系统控制或前台事实先进入等待状态，旧快照仍报告恢复成功，两个场景失败。green：`ResumeAfterDelete` 返回 `false` 要求重读，更新前台事实即使尚不生成片段也推进输入边界；新的实际快照可成功恢复，停止条件保留。查询不推进该边界。
- 精度 red：259231 秒的跨多日片段，其单调时长比 UTC 多 116207 ticks；删中间日后，另一日从 `864000038732` ticks 变为 `864000038731`。green：私有持久分配基准保留原始累计端点，其他日期、重开及再次删除均保持原来的 tick；公开模型和界面不接触该基准。
- 实时缺口 red：扣除删除洞后，末尾缺口错误失去进行中标识。green：只有延伸到当前时刻的最后剩余部分保留进行中标识，既有历史部分保持结束。
- v1 兼容回归使用提交 `c3d0021` 的原公开会话生成的真实固定数据库，未用手工 SQL 构造旧布局。当前会话可打开、查询、删除、继续保存并重开，旧标题不补回；测试数据为合成事实，来源及 SHA-256 见 `Fixtures/README.md`。
- 2026-10-10 最终 focused Release：`DeleteDayTests` 19 项、`RecoveryTests` 17 项、`DailyProjectionTests` 10 项，共 46 passed、0 failed、0 skipped，编译零警告。完整集成回归与 W09 实机删除确认由主代理安排。本轮没有操作 GUI、结束 Debug 程序或提交 Git。

## 06／最终集成

- 06 真实命名 Mutex/Event 独立自检经过 stub red→green，并以真正第二 console 进程验证排他及激活；未创建产品 GUI 或实际写用户 Run 设置。
- 自启命令生成经过 NotImplemented red→green；最后全套首次为 65 passed／1 failed，含双引号路径没有拒绝。新增显式引号校验后，带空格路径、非法路径、命令长度及所有单实例回归通过。
- RecordingRuntimeTests 用真实临时 SQLite 验证 actor 正常退出保存传入的固定截止点，不采用执行命令时的较晚时刻。
- 2026-10-10 最终 Release 完整集成：66 passed、0 failed、0 skipped，编译零警告；Standards／Spec 最终复核均无遗留问题。
- 原生 Windows 通知、托盘/登录、物理电源、完整 WPF 交互和交付程序离线启动仍由 08 人工验收；这些未记为通过。

## 09 UI 更新回归（2026-10-10）

- Harness 截图版最终 UI 源 `18d499f`：Release 完整测试 66 passed、0 failed、0 skipped，生产构建 0 警告/错误。记录会话、真实 SQLite、每日投影与删除测试未改变。
- 当前日期搜索、空结果及清空恢复另由真实 WPF 控件的合成 DayView 离屏预览检查；该工具和 PNG 属于本地产物，未运行原生采集或真实桌面输入。跨筛选修复与高对比度配对经两轴复核，实际 W07/W08 仍待实机验收。

## 10 标题计时与范围（2026-10-10，源 c8cbd8a）

- 完整 Release：79 passed、0 failed/skipped，构建0警告/错误。TitleTimerTests 11项验证暂停/继续/停止、实时查询不落盘、重开不补算、独立软件汇总、锁屏/睡眠、跨日、删除及重开、重复标题归属、时钟跳变、单活动项、tick分配和真实写锁；TimelineBoundsTests 2项验证记录范围、增长、空数据、短片段和真实日长。
- 审查发现“删除昨天误暂停今天标题”及“睡眠边界保存失败仍运行”，修复后加入公开会话回归；真实SQLite写锁释放/唤醒后标题保持暂停，不把睡眠补算进去。两轴复核均无遗留。
- 离屏WPF另验证模式/计时控件/搜索和窄窗口；原生系统通知与真实输入未执行，继续08。
