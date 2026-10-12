# FocusOn 实施与验收记录

记录自动化、各平台原生采集和人工硬件验证各自的证据；待验收项不代表通过。

## 11：当前跨平台工作区（2026-10-11）

基准 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad` 加未提交修改，Debian 13 Linux x64 / .NET SDK 10.0.401。统一 Avalonia 界面、net10.0 无桌面依赖核心和 Windows/Linux/macOS 适配已接入。

- 通过：锁定依赖恢复、Release 零警告构建、87 项核心/平台及 7 项界面回归；最终布局 UI 7 项再次通过。保留原有 79 项旧库/计时/停止/删除规则回归。
- 通过：隔离虚拟 X11 的真实窗口、中文标题变化、PID/可执行文件身份、失效窗口拒绝、SQLite 保存/重开；真正第二进程排他及激活。
- 通过：Linux x64 自包含开发包实际窗口和鼠标/键盘输入，标题计时从首行开始/停止，正常退出 0，持久化已结束的 35.3765402 秒计时。缺 logind 的提示符合预期，不能当作真实 Linux 锁屏/睡眠通过。
- 通过：六架构包、清单/归档哈希、原生架构、执行权限及 macOS app 结构独立验证；脏源正式 publish 拒绝符合预期。产物见 delivery.md。
- 未执行：Windows/macOS 和 GNOME Wayland 的实机交互、真实电源/锁屏/多屏/DPI/托盘、自启、原生 macOS 签名、无 SDK 离线硬件启动及 GitHub Actions。步骤见 docs/manual-acceptance.md 的跨平台清单。

详细证据和命令见 `tests/FocusOn.Tests/Verification.md` 的 11 章节。11 保持 `ready-for-human` 且尚未提交，08 和整体规格保持开放。以下环境和 WPF 结果是原 Windows 版本的历史记录。

## 环境

- 2026-10-09，Windows 11 Pro x64，10.0.26100，Asia/Shanghai。
- 2 台活动显示器；`powercfg /a` 显示支持 S3，固件不支持 Modern Standby（S0 low power idle）。Modern Standby 分项在此机器上不适用，不能推广到支持它的设备。
- 官方 SDK zip SHA512 校验通过，仓库内 .NET SDK 10.0.401／桌面运行时 10.0.12；Microsoft.Data.Sqlite 10.0.12。
- 实机数据隔离在仓库忽略目录，通过 `--data-dir` 运行，不写入正式用户活动库。

## 01 自动化与构建

- T01、T02、T03、T06：逐条 red→green，4 passed、0 failed、0 skipped。详细过程见测试证据文档。
- 整合 WPF 构建：0 warnings、0 errors。
- Standards 审查：0 项明确违规；Spec 身份回退和实机记录缺失两项已修复，复核无新增阻断。工程提交 c6722b7、修复提交 30e71b9。

## Windows 与界面

| 编号 | 已执行的真实观察 | 剩余项／限制 |
| --- | --- | --- |
| W01 | 启动真实 FocusOn 后，基础日志显示 FocusOn 与 Notepad 的秒级切换及标题；自身标题首次读取不可用后补查成 FocusOn，分为不同片段。界面持续增长。 | 当前窗口精确2秒切换、主动同窗口改标题和后台改标题仍待专门实机操作；自动化 T01/T02 已验证相应记录规则。Notepad UI 验收授权超时，未继续输入。WinEvent 不携带历史标题，通知遗漏／延迟仅能五秒补查，不能重建漏失活动。 |
| W02 | 普通 FocusOn 和 Windows 打包 Notepad 的进程路径读取成功，日志软件身份可区分。 | 权限受限、宿主路径不可解析窗口尚未实测；逻辑回退已由 T06 验证，宿主不猜测实际应用。 |
| W03 | 双活动显示器环境中，真实日志只出现当时激活软件；FocusOn 自身按同一规则累计。 | 跨屏切换及后台音频专项待人工操作；不能从双屏配置和逻辑测试声称已验证全部情形。 |
| W04 | 02已接入真实会话和电源通知；尚未主动锁屏、睡眠或灭屏。 | 用户按物理Esc停止computer-use，本轮后续界面操作停止。交付后按人工步骤验收 S3、锁屏、显示器及恢复，Modern Standby 此机不支持。 |
| W05 | 单实例、登录自启、托盘和固定退出边界已实现；真实命名句柄跨进程竞争和唤起自检通过。 | 本轮不创建GUI或修改真实用户自启项。登录、自启、关窗、托盘、重复启动定位、退出交互按人工清单待验收；句柄自检不是桌面交互通过。 |
| W06 | 03已实现；真实SQLite检查点、内存丢弃重开、实际写锁及故障恢复回归通过。 | 实际WPF程序杀进程、故障界面和重启待人工；丢弃会话内存不是实际杀进程。 |
| W07 | 基础今日日志与完整标题详情已真实渲染；05时间线、范围、缩放及联动实现已通过编译和静态审查。 | 本轮不操作GUI，时间线实际比例、窄片段、拖选与联动体验待人工，不能把静态审查记为实机通过。 |
| W08 | 原生只读日志与可选取完整标题文本已呈现；05键盘入口与文字等价信息已实施。 | 全程实际键盘流程、DPI/高对比度专项待人工。 |
| W09 | 07已实施日期/范围确认、跨日事务删除、持久删除洞、完成后新快照恢复。 | 持续采集中的确认/取消、键盘、真实午夜和再次切换/检查点/重启按人工清单待验收。 |
| W10 | x64自包含发布脚本、必需文件检查、逐文件清单与ZIP校验已实施；产物结果在下文补记。 | 本轮不启动GUI；发布程序在断网及无SDK机器上的采集、回看、删除、自启/托盘待人工，不能由文件检查推断通过。 |

## 人工完成步骤

在交付程序上进行：窗口切换与标题变化／两秒切换、跨屏与后台音频、暂停后锁屏再解锁、S3睡眠唤醒仍锁屏、会话灭屏与亮屏、登录自启、DPI与高对比度。逐项记录日期、环境、结果或不支持原因。涉及个人历史的删除仅由用户确认目标日期后执行；开发验证使用隔离数据库。

## 02／04 实施进展

- 9928018：独立停止条件、真实Windows状态源、原生暂停按钮和历史日期／午夜投影已实施。
- 首轮15项公开会话真实SQLite测试通过。审查新增旧系统回调覆盖manual的竞争和DST午夜两项，已修复并补回归。
- 2026-10-10：Release 全部18项通过，0 failed／skipped、0 warnings。旧系统快照不能清除手动暂停；Cuba 时区缺失与重复午夜均经真实SQLite公共查询验证。Standards 与 Spec 复审均无新增问题。
- 02／04实机交互验收未继续执行，因用户停止computer-use；不会把编译或逻辑测试记作实机通过。

## 03 原生边界接入

- 停止事件和手动暂停直接提交当时边界，不等待窗口标题读取；只有恢复请求读取新前台。源版本检查与提交共用锁，旧恢复快照不能覆盖更新的停止条件。
- 已知前台观测异常走可信范围失效入口；普通空前台不清除原生故障提示。旧观测用单调时刻拒收，不被解释为系统时钟回拨。
- 睡眠回调最多等待保存1.5秒；操作系统可能中断进程，不能保证回调时落盘。未保存尾段按崩溃恢复契约处理。[Microsoft suspend 文档](https://learn.microsoft.com/en-us/windows/win32/power/pbt-apmsuspend)
- Stopwatch 在 Windows 使用 QPC；微软文档说明它包含睡眠、休眠与 connected standby 的经过时间。这是 API 依据，不是本机睡眠实测结果。[Microsoft QPC 文档](https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps)

## 05 时间线

- 提交1fcadac；T09公开日查询＋真实SQLite红绿，2项通过。范围仅选择相交日志，保持原每日边界与全天汇总。
- Standards／Spec静态审查均无确定问题。05代码票已关闭，W07／W08实机分项仍交08；渲染、拖选、无鼠标体验不在本轮通过结果中。

## 03 工程关闭

- c3d0021：可信范围恢复、真实写锁、故障后空前台、时钟/时区检测修复；Release 全部38项通过，0失败/跳过、编译零警告。
- Standards／Spec复核均无遗留问题；f608b84关闭03工程票。W06仍待实机，父规格保持开放。

## 06／07 集成

- 06：同用户命名Mutex/Event在真正第二进程中排除第二写入者并唤起主实例；初始化期间激活、释放后重获及隔离目录有公开回归。未实际设置用户Run项、创建托盘或操作GUI。
- 明确退出在停止原生观测线程前捕获终点，等待线程的时间不加给旧软件；超时降级未记录，实际SQL失败仍提示最后尾段未保存。
- 07：真实SQLite事务裁切跨日记录，删除洞持久保存；实时/恢复缺口扣除洞，标题不通过检查点或重开重新出现，其他日保持可用。
- 删除恢复保留快照捕获时刻；如果更新的窗口/控制事实已先进入会话，旧快照不能恢复，调用方重新获取。系统版本和源状态一致检查保护新停止条件，手动暂停独立保留。
- 人工分项步骤见 `docs/manual-acceptance.md`；W05、W06、W09未记为通过。

## 自动化矩阵

| 规格编号 | 主要公开测试边界与覆盖 |
| --- | --- |
| T01、T02、T03、T06 | RecordingSessionTests：短暂切换、窗口/标题/PID边界、检查点与实时尾段、未知身份/标题/无前台 |
| T04、T05 | StopConditionsTests：独立手动/睡眠/锁屏/灭屏、原因优先级、旧系统快照不能清除手动暂停 |
| T07、T12、T13 | RecoveryTests：丢弃未保存内存、成功终点与缺口、真实SQL写锁/恢复、UTC跳变/时区变化、不可信空前台和退出 |
| T08 | DailyProjectionTests：本地午夜、跨日使用/停止/缺口、精确时长分配、缺失及重复午夜 |
| T09 | RangeFilterTests：只选择相交日志，不再裁切，不改变全天软件汇总 |
| T10、T11 | DeleteDayTests：历史/今日/停止状态删除、跨日保留、后续保存/重开、真实删除及恢复写失败、旧快照重试 |
| 补充生命周期 | ResidencyTests真实命名句柄和自启命令边界；RecordingRuntimeTests公开actor确认固定退出终点 |

Windows原生通知与WPF交互不在上述程序逻辑通过结果内。最终完整运行的数量和发布包校验另记交付结果。

## 最终工程回归

- 2026-10-10：完整 Release 测试 66 passed、0 failed、0 skipped，编译零警告；真实旧 v1 SQLite 库兼容、私有原始分配基准及重复删除均通过。
- 首次完整运行仅含引号自启路径一项失败；加入显式路径引号校验后全套通过。Standards／Spec 最终复核无遗留问题。
- 发布脚本 dirty 工作树防护已实际触发并在编译前拒绝发布；不会把未提交源代码标成 HEAD 对应的产物。

## 08 发布结果

- 发布源7f2ec0e3f738492f37c0bbd037346426f7d3e24b，以干净工作树生成Windows x64自包含文件夹和ZIP，包含.NET 10.0.12桌面/Core运行时与原生SQLite。
- 479个文件的大小/哈希与manifest一致；ZIP内逐文件内容及manifest本体均独立校验通过。FocusOn、CoreCLR与SQLite三个PE文件均AMD64。ZIP 77,371,844字节，SHA256见delivery.md。
- 本轮未运行发布GUI或修改实际Run项；W10断网/无SDK机器运行仍待人工。08设ready-for-human，所有剩余Windows分项按manual-acceptance.md处理，整体规格保持开放。

## 当前副本接续验证（2026-10-10，源 1629462）

- Windows 11 专业版 x64 `10.0.26200`，`China Standard Time`；仓库 `H:\CodebuddyWork\FocusOn`。
- SDK 10.0.401 已下载并按官方 SHA512 校验后解压到 `.tools/dotnet/`；桌面/Core 运行时为 10.0.12。
- `./build.ps1 build`：Release 通过，0 警告、0 错误。`./build.ps1 test`：66 passed、0 failed、0 skipped。本次为实际重跑，覆盖前述 T01–T13 与补充生命周期测试。
- 干净工作树执行 `./build.ps1 publish`，源 `16294624d55d12485eac85e6c32ff72ba78bb698`。479 个清单文件、ZIP CRC/清单/逐文件内容、SHA256 和三个关键 PE 的 AMD64 架构均独立校验通过；最新大小和哈希见 delivery.md 的当前副本章节。
- 本地日志在 `artifacts/qa-20261010/`，独立文件校验结果在 `artifacts/delivery-verification.json`；这些是忽略的本地产物。
- 界面工具 native pipe 不存在，首次、重试、重置后重试均报 os error 2；本轮未启动 GUI 或执行 W01–W10 交互。未操作正式数据、自启、电源或网络设置。
- 当前 `powercfg /a` 支持 S3、不支持 Modern Standby：后者子项不适用；锁屏、S3 睡眠、灭屏等实际行为仍未执行。08 与整体规格继续开放。

## 09 Harness 截图版 UI（2026-10-10）

- 实现 `be5d7d4`、修复 `8ebd151`、最终 UI 源 `18d499f`；软件使用、停止原因和未记录三行总览，紧凑工具栏、软件标签日志、当前日期搜索、按需详情以及深色滚动条。
- 末次 `./build.ps1 build`：Release 0 警告/错误；`./build.ps1 test`：66 passed、0 failed/skipped。现有记录、SQLite、日期、筛选和删除自动化仍通过，没有将其计为新鼠标/键盘验收。
- WPF 合成 DayView 的正常、详情、860×580 窄窗口、搜索、无匹配及 2 倍像素图实际渲染并检查；程序化搜索 5 条匹配、0 条无匹配、清空恢复 31 条均通过。没有创建采集器或连接用户数据库。
- Standards 初审 1 项 P2（高对比度前景/背景不配对）；Spec 初审 2 项 P2（相同配色问题，以及点击仅被范围排除记录时误清搜索）。`8ebd151` 修复，后续两轴复核均 0 遗留问题；深色滚动条追加复核无问题。
- `./build.ps1 publish`：源 `18d499f176ff99dae5ec57adf603b885414181a3`，479 文件与 ZIP/manifest/AMD64 独立核验通过；新大小及 SHA256 见 delivery.md 当前发布章节。
- 本轮再次检查桌面工具仍为 native pipe 不存在 os error 2；真实 W07/W08 输入、系统 DPI、高对比度、滚动条拖动及 W01–W10 硬件/离线行为仍交 08。合成图和代码复核不能代替这些实机检查。

## 10 记录范围与标题计时（2026-10-10）

- 实现7d7dc34，修复/发布源c8cbd8a。`./build.ps1 build` 0 警告/错误，`./build.ps1 test` 79 passed、0 failed/skipped。新增 TitleTimerTests（11）/TimelineBoundsTests（2），沿用公开 RecordingSession、受控时间与真实临时 SQLite 边界，验证状态控制、独立汇总、检查点/重开、跨日、删除不复活/无关日不停、重复裁切 tick、真实写锁与睡眠失败恢复。
- Standards 首审1项P2，Spec首审2项P2；跨日删除误暂停及保存失败未暂停已修复并回归，两轴最终均0遗留。
- WPF 离屏合成 DayView 检查记录范围/24h切换、记录增长、刷新保持缩放、开始/暂停/停止按钮、搜索5条/无匹配0条/清空恢复32条及窄窗口，检查通过，预览 artifacts/ui-timer/。没有真实桌面输入或用户库写入。
- 独立新版自包含包479文件、ZIP内容/哈希/AMD64核验通过，见delivery.md当前发布。旧版进程未退出，新包未替换当前运行实例；新功能物理睡眠/锁屏、输入、DPI/高对比度及W10继续移交08。

- 2026-10-12 v0.1.3 source `9c6afdb9febf4793764985bca8b9f91ca53fcf40`: local 90 core/platform + 21 UI regressions pass; six native hosted architectures each repeat all 111 with no skips/failures, native desktop and package/source/runtime checks pass in https://github.com/Liuzkai/FocusOn/actions/runs/38244571956. Native Linux synthetic-data scrollbar/range/pan/Escape and prior hint/follow/history/gap checks pass; 144 app-color assignments satisfy label contrast and persist across restart/reorder/new apps, corrupt/unwritable files preserved. Eight guarded-retry fixtures pass; Actions helper 38245549233 and failed draft-only retry succeed. Draft https://github.com/Liuzkai/FocusOn/releases/tag/untagged-4ffc94db1f1a690ca41f has eight verified Windows/Linux archive/checksum attachments; all older releases/tags/license bytes retained. Mac Developer ID/notary and physical-device checks remain pending in ticket 08; parent specification stays open.
