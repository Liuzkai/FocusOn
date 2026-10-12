# 12：Linux 发布、英文仓库信息和双语界面

**What to build:** 按用户要求把 Linux 包追加到已有 v0.1.0，补全英文 About/README，并提供英文、简体中文界面切换。

**Blocked by:** None

**Status:** ready-for-human

## Acceptance criteria

- Linux x64/ARM64 正式包对应已提交源码，清单/归档哈希、架构、执行权限与运行时验证通过。
- 追加至现有 v0.1.0，Windows 附件及标签保持；说明 Linux 与旧 Windows 标签的源差异并回读确认上传。
- README 使用英文，保留仓库内真实演示截图；About 的描述、主页与 topics 使用英文。
- 首次按系统语言选择中文或英文，界面切换即时生效并按数据目录保存；错误、对话框、托盘和时间线文字覆盖两种语言。
- 切换不重启采集/计时，不改变用户标题、原记录、日期、选择或缩放；配置写入失败保留原选择并提示。
- 回归、两种语言的真实窗口与持久偏好验证完成；外部发布/设置的权限限制与未执行设备验收如实记录。

## Comments

- 2026-10-12: User chose to fill About manually. The remaining external metadata action is user-owned; do not request ABOUT_TOKEN again. Linux release, English README and saved bilingual interface are complete as recorded below. About has not been changed by this agent.

- 2026-10-12：用户明确选择追加现有 v0.1.0。远端 Windows 发布提交 43d6dfd 和记录 6095dbb 已合并，跨平台代码提交 2321305，合并提交 48264ea。本环境 GitHub API 返回 Forbidden，Git HTTPS 读写可用；将使用仓库 Actions 的内置令牌执行发布。About 设置需要仓库管理权限，不能用配置文件存在代替实际设置成功。
- 2026-10-12：英文 README、集中显示翻译、首行语言选择、数据目录内偏好与错误回滚已实现；89 核心/平台 + 10 UI 回归通过，真实 X11 smoke 与中英文窗口/退出重启偏好验证通过。README 已使用新的实际英文截图，并链接中文版本。Linux 发布工作流及 About 元数据已准备，actionlint 与 Python 语法通过；外部上传/设置结果待回读。
- 2026-10-12：源 409cb51ed57f79637cfbca6691480aa5bb6a3a69 已推送 main；Linux run 38203441571 全部通过。两个 Linux tar.gz 和各自 SHA256 已追加并公开发布到 v0.1.0；公开下载后再次核验 244 文件清单、逐项内容/哈希、归档哈希、ELF 架构、执行权限和自包含运行时全部通过。Windows ZIP/校验附件 ID、大小、digest 及旧标签 43d6dfd 保持不变。发布说明标明 Linux 来源与旧 Windows 标签不同，详见 delivery.md。
- 2026-10-12：实际公开下载的 Linux x64 包在隔离目录正常启动（DOTNET_ROOT 指向无 SDK 的路径）；中英文切换、独立计时继续/暂停、配置保存和正常退出 0 通过，计时重开为 603.3514426 秒（原演示 600 秒加实际操作）。ARM64 未做原生启动；真实 logind/GNOME/电源与其他系统人工项保持未执行。
- 2026-10-12：About 真正更新被 GitHub 403 Resource not accessible by integration 拒绝，默认 GITHUB_TOKEN 没有仓库 Administration 写权限；英文描述/主页回退/topics 在 .github/about.json，已向用户请求安全配置 ABOUT_TOKEN。新增独立 Repository About 工作流，配置后可单独执行，保留已成功的 Linux 发布回执；票仅因这一实际权限依赖保持 needs-info，不声明 About 已完成。
