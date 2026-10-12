# 08: 交付无需 SDK 的 Windows x64 离线程序，并完成整体验收

**What to build:** 提供可放在固定目录运行的 Windows x64 自包含程序文件夹及简短使用说明；在交付程序上验证断网采集、回看、删除、自启和托盘，汇总全部测试证据并修复集成阻断问题。

**Blocked by:** 05：通过比例时间线、范围筛选和联动详情回看一天；06：登录自启、驻留托盘，并保持单实例记录；07：按天确认删除，保留其他日期并继续记录未来活动

**Status:** ready-for-human

## Acceptance criteria

- [x] 产出 Windows x64 自包含程序文件夹，运行无需 .NET SDK 或开发工具；固定位置可用于自启。只做该交付形式，不加安装器、自动更新器或其他架构。
- [x] 说明启动/固定目录、自启/托盘、暂停/继续、退出、前台使用时长口径、本地数据位置和按天删除；说明实时尾段与落盘检查点、标题采集和已知兼容性限制。
- [ ] 在 Windows 11 对交付产物断网启动并验收 W10：真实采集、今日/历史回看、比例时间线、删除可用且不依赖服务端；运行程序所需文件完整。
- [x] 将全部票的 T01–T13 自动化矩阵和 W01–W10 实机证据汇总，明确环境、日期、观察结果和限制。重跑集成后受影响的自动化检查，不用重复验收代替已有有效证据。
- [x] 程序逻辑测试与 Windows 通知/标题/电源/界面实机结果分开记录；环境不支持或尚未验证的兼容项标注原因，不冒充通过。
- [x] 正常时间/时区条件下的计时归属、全部停止条件、保存恢复、每日裁切、范围筛选和持续采集删除不存在已知阻断问题；修复集成发现的阻断回归，再完成审查和本地 Git 提交。
- [ ] 本票形成可审查的交付和证据，不自行关闭或修改父规格；按仓库规则关闭本票，整体规格关闭须另按整体完成约定处理。

## Verification

- 规格用户故事：01、02、46。
- 本票测试证据：T01、T02、T03、T04、T05、T06、T07、T08、T09、T10、T11、T12、T13、W01、W02、W03、W04、W05、W06、W07、W08、W09、W10。测试编号与行为以 FocusOn 第一版开发规格为准；使用记录会话这一主要自动化边界和真实临时 SQLite，Windows/界面另做实机验收。
- 每票在验收、必需检查、审查和本地提交后关闭，Comments 记录实际结果与提交引用；未跑或环境不支持的检查明确标注。

## Comments

- 2026-10-09：根据已确认产品设计与现有规格拆分；用户要求逐票实施并由主代理安排子代理协作，确认此拆分及测试边界，发布为独立任务。
- 2026-10-10：依赖票均closed，发布源7f2ec0e；66项自动化通过、编译零警告，最终两轴复核无遗留问题。自包含发布成功，独立逐文件/ZIP哈希及原生AMD64检查通过。完整版本、文件和校验值见delivery.md。
- W10及此前移交的实机分项尚未执行；用户物理Escape停止computer-use后本轮未恢复GUI操作。按照tracker保持ready-for-human，非closed；人工步骤见docs/manual-acceptance.md，父规格保持开放。未执行不是通过。

- 2026-10-10：在当前工作副本 `H:\CodebuddyWork\FocusOn` 接续，安装并校验仓库本地 SDK 10.0.401；源 `1629462` 的 Release 构建 0 警告/错误，完整测试 66 passed、0 failed/skipped。干净工作树重新发布 x64 自包含目录及 ZIP，479 个文件、ZIP 内容/校验和关键 AMD64 架构独立验证通过。最新校验值和日志位置见 delivery.md 的当前副本章节。
- 本轮 Windows 界面工具 native pipe 缺失，首次、重试及重置后重试均报 os error 2；没有启动 GUI 或执行断网、登录、电源及删除交互。当前机器支持 S3、不支持 Modern Standby，后者子项不适用；其余实机分项仍待验收。保持 ready-for-human，不关闭本票或父规格。
- 后续用户要求 Harness 截图版 UI，09 工程票已完成；当前发布源更新为 `18d499f`，66 项回归通过，WPF 合成离屏预览通过，479 文件和 ZIP 独立校验通过。新增搜索/时段/详情/缩放平移/深色滚动条的实机步骤已加入 manual-acceptance.md 的 W07，W08 仍需实际高对比度和 DPI 检查。本票继续 ready-for-human，不能沿用旧包哈希；最新校验见 delivery.md 当前发布章节。
- 10 标题计时/记录范围票完成，最新发布源 c8cbd8a，79项回归、离屏WPF和479文件/ZIP独立核验通过。新路径 artifacts/FocusOn-win-x64-timer，旧版仍运行未替换；新功能真实锁屏/睡眠、重开、删除、输入/DPI/高对比度已加入W07/W09，继续待验收。本票及整体规格保持开放。

- 2026-10-12：用户授权配置 GitHub Releases；配置提交 `43d6dfd` 已推送 main，`v0.1.0` 标签触发 run 38198854147，云端 Release 构建零警告/错误、79 项测试全部通过、发布包校验通过。Release 草稿已包含 ZIP/SHA256 与中文说明；下载附件后独立复核文件集合、内容、ZIP、架构和运行时通过。详细哈希、链接和源提交见 delivery.md。此次没有新增设备交互验收，08 继续 ready-for-human，整体规格保持开放。


- 2026-10-12: 13 engineering complete at immutable v0.1.1 source `45ad974fc3f86c9721b78da560361a77d1e0809a`; https://github.com/Liuzkai/FocusOn/actions/runs/38228221117 Success, six native x64/ARM64 targets each 102 regressions plus real desktop/.app and package checks. Draft https://github.com/Liuzkai/FocusOn/releases/tag/untagged-9e6e4d5dd19f594f38a2 contains verified Windows/Linux archives/checksums; v0.1.0 preserved. Accept only automated boundaries as passed. Remaining manual items: Windows physical WTS/power/Explorer/DPI/offline installed package; GNOME/KDE real tray/host restart and Wayland/logind power; Mac fixed app Accessibility grant/revoke, Dock/menu/input/LaunchAgent, lock/sleep/all displays/fast user switching and offline native installed packages. Mac Developer ID certificate/account/notary API and seven secure Actions Secrets are not configured; signing/notarization/stapling/Gatekeeper distribution must still execute successfully before formal Mac readiness. See docs/macos.md and docs/manual-acceptance.md. Both ad-hoc Mac validation bundles expire after seven days; do not call them trusted Release downloads. 08 remains ready-for-human and parent specification open.

- 2026-10-12: 15 UI engineering completed at v0.1.2 source `8f25193fa09ec7672c197a80c4679eb0d4d5df44`, draft https://github.com/Liuzkai/FocusOn/releases/tag/untagged-e5e1022863366939ef6e, six native targets/package checks and upload pass in https://github.com/Liuzkai/FocusOn/actions/runs/38237429087. Local 107 regressions and actual Linux synthetic-data pointer/cross-lane hint/log-end/history/pixel checks pass. Continue physical Windows/macOS/Linux DPI/multi-monitor/edge-hover/hide, real new foreground-record follow and prior permission/power/offline acceptance in manual-acceptance.md. Mac still validation-only without Developer ID/notary trust. 08/parent specification remain open; public v0.1.1 unchanged.

- 2026-10-12: Ticket 16 adds a separate totals scrollbar row, persistent separated app colors and right-click range zoom. Local Linux/Xvfb mouse checks and regressions pass; continue physical Windows 100/150/200% DPI and narrow-window scrollbar/drag, right-click/right-drag distinction, app-color restart/date stability and high contrast in manual acceptance. Preserve existing power/permission/offline items; this does not close ticket 08 or the parent specification.

- 2026-10-12: Ticket 17 replaces the large totals scrollbar with a transient 3-DIP line and full-label-strip dragging. Local regressions and real Linux mouse/pixel checks pass. Verify physical Windows/macOS DPI/trackpads, narrow windows, mouse leaving/reentering during drag, hide/deactivate while captured and keyboard scrolling. Existing data/color/timeline/log/power/permissions acceptance remains open.
