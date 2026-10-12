# FocusOn 开发交接与持续记录

更新日期：2026-10-12。原始整理基线：`main` / `f02d28e`；最新公开 GitHub Release 构建源：`45ad974` / `v0.1.1`（历史 `v0.1.0` 保留），记录范围/标题计时实现源：`c8cbd8a`。本文帮助后续开发快速接续已确认的设计与实现；项目工作约定入口仍是 [AGENTS.md](AGENTS.md)。

## 1. 来源与阅读顺序

本次阅读了工作区 `.temp/设计 Windows 11 软件使用记录工具.md` 中的设计、确认、实施及交付记录，并与当前仓库文档、任务和源码核对。该原始对话位于仓库外，不是构建依赖，也不随本仓库分发；可持续维护的依据如下。

| 需要了解的内容 | 文件 |
| --- | --- |
| 工作方式、任务状态及完成约定 | [AGENTS.md](AGENTS.md)、[任务跟踪](docs/agents/issue-tracker.md)、[状态标签](docs/agents/triage-labels.md) |
| 术语与计时取舍 | [GLOSSARY.md](GLOSSARY.md)、[领域约定](docs/agents/domain.md)、[ADR 0001](docs/adr/0001-foreground-usage-boundary.md) |
| 已确认产品行为 | [产品事实](PRODUCT.md)、[第一版设计](docs/design.md)、[开发规格](.scratch/focuson/spec.md) |
| 功能依赖、验收条件与实现提交 | [开发票](.scratch/focuson/issues)及每票底部的 Comments |
| 构建与日常运行 | [README.md](README.md)、[使用说明](docs/usage.md)、[build.ps1](build.ps1) |
| 自动化、实机与交付证据 | [实施与验收](.scratch/focuson/verification.md)、[测试证据](tests/FocusOn.Tests/Verification.md)、[工程交付](.scratch/focuson/delivery.md)、[人工验收](docs/manual-acceptance.md) |
| 界面约定与待检查项 | [每日回看界面说明](docs/ui/focuson-review.md) |

设计和规格中的“尚未实现”“没有 SDK”等句子是当时的阶段背景，不能用来判断当前代码是否存在。产品行为以已确认设计、规格和 ADR 为依据，实现事实检查源码，完成状态检查任务 Comments 与验收证据。若发现行为冲突，明确记录并澄清，不能用实现偏差直接覆盖产品约定。

## 2. 已经完成的设计过程

1. 2026-10-09，用户逐项确认 Q1–Q13：单机离线、按激活窗口计时、保存标题与起止时长、保留短切换、无输入仍计时、系统停止和手动暂停、托盘自启、长期保存及按天删除。
2. Q14 确认完整第一版设计，形成术语表和前台计时 ADR。界面借鉴 DeepSeek Harness 的横向时间总览与日志联动，采用软件使用记录语义。
3. 确认使用本地 Markdown 跟踪任务，Git 远端为 `https://github.com/Liuzkai/FocusOn.git`。随后形成 46 条用户故事、T01–T13 自动化矩阵、W01–W10 实机验收，并拆为 8 张有依赖的开发票。
4. 用户随后要求逐票实施，历史实现曾由主代理安排子代理协作。01–07 完成工程实现、自动化检查、审查与提交；不能执行的实机分项逐项转交 08。
5. 2026-10-10，历史记录显示完成 x64 自包含发布；之后按用户要求启动程序，观察到“正在记录”，并推送到 `main` 的 `f02d28e`。该次启动只证明基础启动和记录显示，不等于完整验收。

原始对话中的旧路径 `D:\Github\FocusOn`、旧环境和已执行命令仅用于理解历史，不应原样重放。本次工作副本为 `H:\CodebuddyWork\FocusOn`；后续命令以实际仓库根目录为准。

## 3. 后续修改必须保持的产品规则

### 计时与身份

- 名称使用“前台使用时长”，不称“专注时长”或“真实工作时长”。静读、看视频即使没有键鼠输入仍计时，不加空闲阈值自动扣时。
- 只有当前激活窗口获得时间；后台播放、其他屏幕上未激活的窗口不计，FocusOn 自身按相同规则记录。
- 同软件换窗口、当前窗口换标题都分段；当前实现用 HWND、PID 和标题识别片段变化。重复相同事实、后台标题变化不产生新片段。保留两秒短切换，不跨越其他软件合并片段。
- 汇总身份优先用规范化可执行路径，PID 和 HWND 不是跨启动的软件身份。确认前台但身份不可读时显示“未知软件”，标题不可读时显示“标题不可用”；完全无法确认前台时是“未记录”。无法解析打包应用时不猜测宿主就是实际应用。

### 停止、恢复与可信保存

- 手动暂停、睡眠、锁屏、会话灭屏是独立条件；重叠时主显示原因依次为：手动暂停 > 睡眠 > 锁屏 > 灭屏。
- 恢复须同时满足采集可用、未手动暂停、未睡眠、已解锁、会话屏幕亮起；重新读取当前前台。自动恢复事件不能清掉手动暂停。手动暂停跨进程持久化没有既定产品承诺。
- 多显示器中仅关闭一台，不直接视为整会话灭屏。通知与电源行为必须在目标设备验证。
- 已知停止、采集缺口、已删除范围分别表达；停机或故障缺口不能沿用旧软件或旧停止原因，也不能推测补算。
- 五秒检查点用于补查和保存可信终点，窗口切换、停止及正常退出另保存边界。实时显示增长不等于尾段已落盘；崩溃只恢复成功保存的范围，正常调度下可能丢失约五秒尾段，不能承诺硬上限。
- 写入失败必须呈现记录异常、保留成功历史；不能继续声称可靠保存。窗口通知遗漏或延迟无法由五秒补查完整重建。

### 日期、筛选与删除

- 正常时钟与固定时区下，区间采用 `[开始, 结束)`；存 UTC 起止，持续时长来自单调时间。本地日边界用于回看和删除，不硬编码一天等于 24 小时。
- 时间条、日志及软件汇总消费同一每日投影。跨午夜只展示所属日部分；汇总始终是所选日期全天口径。
- 拖选只选择实际相交的当日日志，不再次裁短片段，不改变全天汇总；只接触结束边界不算相交。
- 按天删除要显示日期、范围并确认，事务保留其他日期。删除今天先结束开放片段，删除后从新的完成时刻记录未来状态，保持所有暂停条件。
- 已删内容不能由检查点、旧回调或重启补回；删除后的空白不是采集故障。删除没有撤销或恢复功能。
- 时钟跳变和时区变化要识别并提示，不静默重写历史；异常后的历史日期映射不在第一版正确性承诺内。

### 界面与范围

- 2026-10-10 用户新增截图版要求优先：按 DeepSeek Harness 的深色紧凑工具栏、三行总览和软件标签日志改 UI，详情默认隐藏、选择展开；当前日期软件/标题搜索仅筛选回看，全天汇总不变。时段分隔、时长总览和详情可开合，支持滚轮缩放与右键平移/清范围。详见 09 票和 docs/ui/focuson-review.md，后面的最初布局/搜索范围以此补充为准。

- 单页首行为标题计时，后续为日期/当前记录状态、全天软件汇总、横向比例时间线、日志及详情。时间线按真实比例绘制，短片段靠缩放或日志查看，不人为扩大宽度。
- 同软件颜色稳定，状态同时有文字；日志虚拟化，主要操作可用键盘。刷新应保留选择、筛选及滚动；默认今天随午夜更新，主动选择历史日期后保持所选日。
- 登录自启到托盘，关窗继续采集，托盘明确退出才停止；同用户、同数据目录只能有一个采集写入者。
- 数据长期本地保留，不自动清理。只记录必要的软件/窗口元信息；不另读截图、正文、地址栏或输入内容，标题本身可能包含私人信息。
- 第一版不扩展云同步、多平台、搜索、导出、分类、排除名单、注意力评分、安装器、自动更新器或多架构发布。视觉细节未被指定为品牌承诺。

## 4. 当前工程与代码入口

2026-10-11 用户要求 Windows/Linux/macOS，当前工程为 C#、.NET 10、Avalonia 和 Microsoft.Data.Sqlite；核心/平台/桌面三个项目、一个桌面进程，另有核心/平台与界面测试。原 Windows-only 范围由 ADR 0003 扩展。保持直接的职责分工，不为扩展预先引入服务端、Windows 服务、ORM、WebView 或通用框架。

| 入口 | 职责 |
| --- | --- |
| [App.axaml.cs](src/FocusOn/App.axaml.cs) | 启动参数、组件生命周期、控制状态版本、五秒补查、删除后恢复和固定退出边界 |
| [WindowsForegroundObserver.cs](src/FocusOn.Platforms/WindowsForegroundObserver.cs) | 前台/当前窗口标题事件、消息循环、身份读取及补查 |
| [WindowsSessionObserver.cs](src/FocusOn.Platforms/WindowsSessionObserver.cs) | 会话、锁屏、睡眠和会话显示状态 |
| [RecordingClock.cs](src/FocusOn.Core/RecordingClock.cs)、[RecordingModels.cs](src/FocusOn.Core/RecordingModels.cs) | UTC/单调时间、观测、停止条件、片段、日视图及范围筛选契约 |
| [RecordingRuntime.cs](src/FocusOn.Core/RecordingRuntime.cs) | 后台队列串行执行观测、控制、查询和数据库操作 |
| [RecordingSession.cs](src/FocusOn.Core/RecordingSession.cs) | 核心状态、SQLite 事务、可信终点、故障恢复、每日查询与删除 |
| [DailyProjection.cs](src/FocusOn.Core/DailyProjection.cs) | 本地日期有效边界、DST 午夜与精确时长分配 |
| [MainWindow.axaml](src/FocusOn/MainWindow.axaml)、[MainWindow.axaml.cs](src/FocusOn/MainWindow.axaml.cs) | 日期、状态、日志、详情、删除确认和一秒界面刷新 |
| [TimelineControl.cs](src/FocusOn/TimelineControl.cs)、[DisplayText.cs](src/FocusOn.Core/DisplayText.cs) | 时间线绘制/缩放/选择及显示文案 |
| [RecordingController.cs](src/FocusOn.Platforms/RecordingController.cs) | 平台观测与暂停、删除、退出边界的协调；防止过期系统快照回写 |
| [TrayController.cs](src/FocusOn/TrayController.cs)、[SingleInstance.cs](src/FocusOn.Platforms/SingleInstance.cs)、[StartupRegistration.cs](src/FocusOn.Platforms/StartupRegistration.cs) | 托盘、同用户同目录单实例与 HKCU 登录自启 |

实际持久化已经超出设计阶段的“一个区间表”示意：SQLite 使用 WAL，包含 `intervals`、`deleted_ranges`、`interval_allocations`；后两者保留删除空洞和裁切分配基准，避免删后复活及重复裁切的时长误差。时长存为 ticks，不是设计示意中的 `DurationMs`。修改存储要兼顾已有数据库，使用 [旧库合成测试夹具](tests/FocusOn.Tests/Fixtures/README.md) 验证，不能拿个人活动库做故障或迁移试验。

特别注意已修复的回归：旧系统快照清除手动暂停、旧窗口/恢复回调覆盖新停止条件、HWND 被不同 PID 复用、故障后空前台错误解除异常、删除后旧快照回写、DST 缺失/重复午夜、退出等待时间算给旧软件，以及自启路径引号。修改相关入口时保留这些行为保护。

## 5. 构建、运行与数据

仓库 [global.json](global.json) 指定 SDK `10.0.401`，`rollForward: latestPatch`；生产目标为 `net10.0`，启用 nullable 和 warnings-as-errors，Microsoft.Data.Sqlite 当前固定为 `10.0.12`。依赖版本以项目文件为准，不因本机缺 SDK 降级目标框架。

在仓库根目录的 PowerShell 中执行：

```powershell
dotnet --list-sdks
./build.ps1 build
./build.ps1 test
./build.ps1 publish
```

- `build.ps1` / `build.sh` 优先使用仓库 SDK，再使用 PATH 中的 dotnet；均为 Release，安装遵守 packages.lock.json。`test` 执行核心/平台和 Avalonia 界面测试，Linux 可运行；`smoke` 需要隔离 X11 显示。
- `publish` 要求干净且已提交的工作树；成功后生成 `artifacts/release/FocusOn-<runtime>/`、归档、逐文件 `manifest.json` 和归档 SHA256。`package-check` 是单独的开发验证产物，写 artifacts/validation/，脏工作区不标记正式 sourceCommit。发布清单包含源提交，不能绕过脏工作树检查，也不能把历史包的哈希用于新包。
- `.tools/`、`bin/`、`obj/`、`artifacts/`、测试输出和用户 SQLite 文件被忽略；仅明确列出的合成旧库夹具纳入 Git。克隆代码不会带回历史 SDK、缓存或发布产物。
- 当前副本（2026-10-11）为 `/workspace/FocusOn`，Linux x64 云容器，基准源 `3e1d78f2bc05cd22cb9329ec2c0c06e9664268ad` 加未提交的 11 票修改。官方 SHA512 校验的 SDK 10.0.401 在 `.tools/dotnet/`，没有替换系统 SDK。`.tools/system/` 提供隔离 Xvfb 工具，`.tools/pwsh/` 提供可选 PowerShell 7.6.6；新终端需重设 SDK/缓存变量。2026-10-10 Windows 的 SDK、66 项测试与旧包信息是历史记录，详见 delivery.md。

Windows 正式数据仍为 `%LOCALAPPDATA%\FocusOn\focuson.db`，Linux/macOS 默认目录见 [平台说明](docs/platforms.md)。运行时可能伴随 `-wal`、`-shm`，备份前明确退出程序，再复制完整数据目录。开发/验收使用隔离目录，例如构建后：

```powershell
$focusOnQaData = Join-Path $env:LOCALAPPDATA 'FocusOn-QA\data'
& '.\src\FocusOn\bin\Release\net10.0\FocusOn.exe' --data-dir $focusOnQaData
```

开发构建需要对应 .NET 10 运行时与平台图形依赖；发布目录包含 .NET 运行时。macOS 需辅助功能权限，Wayland GNOME 需随附桥接；见 docs/platforms.md。`--data-dir <目录>` 跳过正式自启注册，隐藏隔离启动的参数顺序为 `--startup --data-dir <目录>`。普通无参数启动按平台注册当前用户 Run 键、XDG autostart 或 macOS LaunchAgent，指向固定程序路径并加 `--startup`；没有应用内自启开关。验收时核对目标数据目录和实例。

## 6. 开发与验证约定

- 开始功能工作前阅读相关开发票全文和 Comments；只选择开放且前置票已关闭的任务。规格和任务继续放在 `.scratch/focuson/`，不另建一套重复状态表。
- 核心自动化边界：受控窗口、标题、系统状态、UTC/单调时间及用户操作 → 公开 `RecordingSession` → 真实临时 SQLite → `GetDay`、状态和重开后的公开结果。测试验证用户可观察行为，不绑定私有字段、SQL 文本或内部调用次数，不用假数据库证明持久化。
- T01/T02/T03/T06 对应 `RecordingSessionTests`；T04/T05 对应 `StopConditionsTests`；T07/T12/T13 对应 `RecoveryTests`；T08 对应 `DailyProjectionTests`；T09 对应 `RangeFilterTests`；T10/T11 对应 `DeleteDayTests`。生命周期补充见 `ResidencyTests` 和 `RecordingRuntimeTests`。
- 核心逻辑变更先验证受影响场景，再完成必要集成检查；纯文档变更检查事实、引用和 diff 即可。VSTest 需要本机回环通信，测试宿主无法启动时要区分环境失败与测试失败。
- 自动化、静态审查、基础启动、真实 Windows 通知/硬件行为分别记证据；模拟事件或文件完整性检查不能代替实机验收。结果使用“通过 / 失败 / 未执行 / 不适用”，注明环境、提交、命令与限制。
- 按现有 tracker，代码票需实现、必需检查、审查和提交后，在 Comments 追加结果和提交引用才能关闭；移交 08 的人工项仍属待验收。08 与整体规格不能仅凭代码完成或历史测试数关闭。

## 7. 当前进度与接续事项

**最新 14 票（2026-10-12）：**用户明确要求公开 v0.1.1；[公开 Release](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.1)已发布并设为 latest，发布源/标签仍为 `45ad974`，没有重建或替换附件。[发布核对 run 38230937502](https://github.com/Liuzkai/FocusOn/actions/runs/38230937502) Success；四个 Windows/Linux x64/ARM64 包与四个 SHA256 公开直链下载再次匹配原草稿大小/哈希/manifest 源，v0.1.0 标签及资产保持。Mac 仅 Actions ad-hoc 验证，未 Developer ID 签名/公证，不是正式附件。14 工程完成，08 的设备/权限/签名待办及父规格继续开放；普通推送仍不直接公开发布，后续版本默认草稿。

**最新 13 票（2026-10-12）：**同一项目接入三平台图标、关闭驻留、恢复/退出、Mac Dock Reopen、Linux StatusNotifier 确认与宿主消失恢复。用户已确认独立 v0.1.1 草稿，保留 v0.1.0。源码 `140d540e8832640b222a50334141ee94edf5ea57` 的[六架构原生验证](https://github.com/Liuzkai/FocusOn/actions/runs/38226659404)全部通过：每目标 90 核心/平台 + 12 UI（102 项，0 失败/跳过），真实原生桌面、SQLite、计时/语言/单实例/退出及六包完整性/架构/校验。Mac Intel/Apple Silicon 均实际记录前台 Usage，并启动最终自包含 .app、正常 native Quit；修复 Darwin IPC 长度、ARM64 fcntl ABI、会话字典常量、原生回调退出死锁及 Mac 包规范布局。Apple Secrets 未配置，Mac 仅 ad-hoc 验证，未做 Developer ID/公证/staple，不上传为正式附件。物理权限、电源、GNOME/KDE 菜单等仍由 08 人工验收。[v0.1.1 草稿](https://github.com/Liuzkai/FocusOn/releases/tag/untagged-9e6e4d5dd19f594f38a2)已创建，正式源 `45ad974` / [run 38228221117](https://github.com/Liuzkai/FocusOn/actions/runs/38228221117) 六架构全部通过，含四个 Windows/Linux 包及 SHA256；上传后下载核对及 v0.1.0 资产快照保持通过，详见 delivery.md；以 13 票、docs/releases.md、docs/macos.md 和最新验收记录为准。

**最新 12 票（2026-10-12）：**Linux x64/ARM64 已追加并公开发布 [v0.1.0](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.0)，Linux 源 409cb51、run 38203441571 成功；旧 Windows 附件/标签保持。99 项回归（89 核心/平台、10 UI）、真实 X11、两语窗口与偏好重启通过；公开下载后的 244 文件/归档/架构/权限再次核验及公开包实际计时保存通过。英文 README、双语源码已推送。About 更新曾被 GitHub 403 拒绝，用户现已选择手动填写，12 改为 ready-for-human，仅外部 About 交用户，不再请求 ABOUT_TOKEN。详细链接/哈希见 delivery.md，硬件验收仍开放。

**最新 11 票（2026-10-11）：**用户扩展全平台；统一 Avalonia 界面及 Windows/Linux/macOS 平台层已接入，无桌面依赖核心保留旧库兼容。87 项核心/平台及 7 项界面测试通过；真实虚拟 X11 窗口/标题/SQLite 与跨进程单实例验证通过，Linux 桌面按钮输入与保存验证通过。六架构开发验证产物在 `artifacts/validation/`。当前验证与剩余限制以 11 票及 tests/FocusOn.Tests/Verification.md 跨平台章节为准；下列 Windows 版本结果为历史证据。工作区未提交，原生 Windows/macOS/GNOME 与电源、DPI 等待人工验收。

**2026-10-12 GitHub 发布配置：**新增 `.github/workflows/release.yml`，推送 `v*` 标签后执行 Windows Release 构建、测试、自包含发布和 `scripts/verify_delivery.py` 独立校验，上传 ZIP 与 SHA256 到 Release 草稿。工作流显式申请 contents: write，使用内置令牌；Actions 已启用，不需要个人 Token Secrets。固定官方 Actions 的提交版本；重跑只更新草稿附件，已发布版本要求新标签。README.md 已记录操作。actionlint 1.7.12 检查通过；配置提交 `43d6dfd` 已推送 main 并标记 `v0.1.0`。GitHub run 38198854147 全部通过，构建零警告/错误、79 项测试通过；首个草稿含 ZIP 和 SHA256，已补中文说明，下载附件后的独立完整性校验通过。详细源提交、链接及哈希见 delivery.md；草稿等待用户检查并公开发布，真实设备验收仍按 08 继续。

**2026-10-12 最新构建：**按用户要求重新编译已与 origin/main 同步的 `3e1d78f`，仓库 SDK 10.0.401；Release 构建零警告/错误，79 项测试通过、0 失败/跳过。干净工作树发布至 `artifacts/FocusOn-win-x64-20261012-3e1d78f/` 及对应 ZIP，自包含 Windows x64；480 个文件（含 manifest）、逐文件大小/哈希、ZIP 内容/哈希及三个关键 AMD64 文件核验通过，详情见 delivery.md 的 2026-10-12 章节。旧路径程序 PID 11676 仍在运行；新包尚未启动，升级先从旧托盘退出。人工验收状态保持原样。

**最新 10 票：**用户要求记录范围/24h 双模式、首行标题计时与使用/停止/计时三行；实现 `7d7dc34`、审查修复/发布源 `c8cbd8a`。Release 零警告/错误，79 项真实 SQLite 等回归通过，两轴复核无遗留；离屏 WPF 两种模式、增长、缩放保持、按钮状态、搜索和窄窗口检查通过。10 工程票关闭，真实输入、DPI、高对比度及睡眠/锁屏规则交 08，整体规格保持开放。

新包位于 `artifacts/FocusOn-win-x64-timer/` 和对应 ZIP，479 文件、哈希、ZIP 内容及关键 AMD64 架构核验通过。旧路径 `artifacts/FocusOn-win-x64/` 的源 18d499f 程序仍以 PID 11676 运行，本轮没有退出或覆盖它；新包尚未替换正在运行的实例。升级时先托盘退出旧版，再从新目录启动。

09 后续用户已要求启动并 commit/push：旧版正常启动，标题 FocusOn · 2026-10-10、Responding True；原 5 个提交已推送至 origin/main 的 `5e1876e`。这些基础启动/推送事实不等于 W01–W10 通过，下文此前“未启动/未推送”是相应历史阶段记录。

截至最新交接，01 前台记录、02 停止恢复、03 可信保存、04 每日历史、05 时间线、06 托盘自启单实例、07 按天删除及 09 Harness 截图版 UI 均为 `closed`；[08 离线交付与整体验收](.scratch/focuson/issues/08-offline-x64-delivery.md) 为 `ready-for-human`；整体规格仍为 `ready-for-agent`，未关闭。

**历史证据，非本次重跑：**2026-10-10 Release 共 66 项通过，0 失败/跳过、编译零警告；发布源 `7f2ec0e` 的 x64 自包含目录、479 个文件及 ZIP 哈希已核验。旧机器为 Windows 11 Pro x64、双屏、支持 S3、不支持 Modern Standby；这不是对当前机器或其他硬件的结论。历史对话后续另有一次成功启动记录和推送记录，补充了交付文档“本轮未启动/未推送”的阶段说明，但没有补齐 W01–W10。

**本次重跑证据：**当前副本源 `1629462`，Release 构建 0 警告/错误，完整测试 66 passed、0 failed/skipped；新发布包 479 个文件、ZIP 内容与哈希及三个关键 AMD64 PE 均独立核验通过。当前 Windows 11 专业版 x64 版本为 `10.0.26200`，支持 S3、不支持 Modern Standby。日志和新 ZIP 校验值见 delivery.md 当前副本章节。

**最新 UI 证据：**源 `18d499f` 的 Harness 截图版 UI 最终构建 0 警告/错误，66 项测试通过，WPF 合成数据正常/窄窗口/详情/搜索/无匹配预览通过，Standards / Spec 修复后均无遗留问题；新发布包 479 文件和 ZIP 内容/哈希/AMD64 核验通过。上述 1629462 同路径旧包已替换，运行路径不变，最新校验值以 delivery.md 的“当前发布”章节为准。真实桌面输入、系统高对比度/DPI 与 W01–W10 仍待 08 验收。

下一步优先按 [人工清单](docs/manual-acceptance.md) 补齐 08，不因现有票已关闭而重写已实现功能：

| 范围 | 仍需补齐的实机证据 |
| --- | --- |
| W01–W03 | 精确短切换、前台/后台标题事件、权限受限及宿主识别、跨屏与后台音频 |
| W04 | 锁屏、睡眠、会话灭屏、重叠条件恢复及目标硬件支持情况 |
| W05–W06 | 登录自启、关窗/托盘、重复启动、固定退出终点、真实杀进程、运行中写入失败及故障界面 |
| W07–W08 | 比例时间线、窄片段、筛选/详情联动、刷新稳定、键盘、DPI、高对比度和长列表 |
| W09–W10 | 删除确认/取消、真实跨日与持续采集删除、发布目录断网运行、无 SDK 环境启动 |

原始整理仅涉及文档和本地环境检查，随后文档已提交推送。本轮接续已完成 SDK 准备、构建、测试和发布，但 Windows 界面工具首次、重试及重置后重试均提示 native pipe 不存在（os error 2），因此未启动 GUI，也未操作正式数据、自启、物理电源或网络设置。后续先恢复界面工具连接，或由用户按隔离清单人工验收，再按实际结果判断是否满足 08 和整体完成条件。

## 8. 后续记录维护

10 新入口：TitleTimerStore 保存独立标题与片段，RecordingSession 的 ChangeTimer/GetDay/DeleteDay 统一公开边界，RecordingRuntime 串行执行；TimelineBounds 给出记录范围或真实本地日，TimelineControl 默认自动随记录增长。新表 title_timers/timer_segments 与原库并存；软件汇总只包含 Usage。锁屏继续、睡眠/退出/异常暂停及重开手动继续为暂未收到偏好回复时采用的默认规则，见 ADR 0002。

本文件保留可复用的决策、入口、风险和最新交接状态；详细测试过程写入对应票 Comments、`verification.md` 或人工清单。新增产品取舍更新设计/规格，术语更新 `GLOSSARY.md`，重大计时决策补 ADR；避免在多处复制完整规格。

每次阶段交接至少记录：日期和源提交、已完成行为与原因、涉及入口、实际验证命令与结果、未验证范围及原因、下一步和依赖。环境、SDK、产物路径和测试数量均要注明观察时间；没有重新执行就标明引用历史证据，不追加未经验证的“通过”。

| 日期 | 变更与验证 | 后续 |
| --- | --- | --- |
| 2026-10-12 | Linux 云端源 409cb51 已公开追加 v0.1.0，run 38203441571 成功；99 回归/原生 X11/公开下载 244 文件和归档再核验/公开包双语计时保存通过。英文 README 与双语源码推送，保留上游 MIT 和第三方声明，Windows 资产/旧标签保持。 | About 403 管理权限缺失，12 needs-info；用户配置 ABOUT_TOKEN 后触发独立 Repository About 工作流并回读。勿重传或替换已公开 Linux/Windows 资产；设备项继续 08。 |
| 2026-10-12 | 12 票：英文 README、中英文界面与偏好保存，99 回归、真实 X11、两语实际窗口及英文偏好重启通过；发布脚本/工作流准备，actionlint 通过。合并 Windows 发布历史并保留 v0.1.0 标签及附件。 | 正式 Linux 包核验并经 Actions 追加 v0.1.0；回读实际发布及 About 权限结果。设备验收继续保持开放。 |
| 2026-10-12 | 按用户要求将 Linux 实际界面截图保存到 `docs/images/focuson-ui.png` 并嵌入 README，注明演示数据与云环境会话提示。图片内容与原预览一致，相对链接及非忽略路径核对通过；仅文档/图片修改，未重跑程序测试。 | 后续更新截图时替换仓库内图片，保留平台和数据来源说明。 |
| 2026-10-11 | 全平台 11 票：net10.0 核心、Avalonia 桌面、Windows/Linux/macOS 适配与六架构打包。87 核心/平台 + 7 UI 通过，真实虚拟 X11/单实例及 Linux 包标题计时保存通过，六包独立哈希/架构/权限验证通过；云安装/启动配置草稿已保存。基准 3e1d78f 加未提交修改。 | 11 保持 ready-for-human；Windows/macOS/GNOME、真实 logind/电源/托盘/DPI/ARM64 与原生签名待验收，08/规格开放。环境设置需用户保存并发布；配置保存不等于已应用。 |
| 2026-10-10 | 根据原始设计过程和 `f02d28e` 仓库创建本交接记录，关联现有任务与证据；核对源码入口、SDK 要求及本地缺项。仅文档整理，未重跑 66 项历史测试。 | 准备匹配 SDK；按 08 与人工清单接续验收并记录实际结果。 |
| 2026-10-10 | 源 `1629462`；准备并校验仓库 SDK 10.0.401，Release 构建零警告/错误，66 项测试全部通过；重新发布并独立验证 479 文件、ZIP 和关键 AMD64 架构。仅新增验收记录，程序源码未变。 | 界面工具 native pipe 缺失，W01–W10 交互仍待验收；恢复连接或按隔离清单人工执行。08 与整体规格保持开放。 |
| 2026-10-10 | 用户 Harness 截图版 UI：实现 be5d7d4、修复 8ebd151、发布源 18d499f；三行总览/软件标签/当前日期搜索/按需详情/深色滚动条。最终构建和 66 项回归通过，WPF 合成离屏图通过，两轴复核无遗留问题，发布包独立核验通过。 | 09 工程票关闭；真实输入/DPI/高对比度仍交 08。当前本地提交未推送，最新使用与验收步骤已更新。 |


**2026-10-12 许可整理：**基于 `6095dbb`，新增完整 MIT LICENSE，并在 README 说明原创代码范围、商业分发、保留版权与许可声明、无需界面署名及第三方依赖例外。仅许可和交接文档变更；检查许可正文、范围及 `git diff --check`，未重新运行 Windows 构建或实机验收。远端提交状态以 Git 远端核验为准。
