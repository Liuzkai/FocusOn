using System.Globalization;
using System.Text.RegularExpressions;

namespace FocusOn;

/// <summary>Presentation text only; recorded application names and user titles remain unchanged.</summary>
public static class Localizer
{
    public static string Language { get; private set; } = "zh-CN";
    public static void SetLanguage(string language)
    {
        if (language is not ("zh-CN" or "en")) throw new ArgumentException("Unsupported interface language.", nameof(language));
        Language = language;
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-US" : language);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string Get(string chinese, params object?[] arguments)
    {
        var format = Language == "en" && English.TryGetValue(chinese, out var english) ? english : chinese;
        return arguments.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, arguments);
    }

    // Core/platform observers retain their original diagnostic strings. Translate only
    // complete known messages at the display boundary, including cached warnings.
    public static string? Translate(string? message) => Translate(message, 0);
    private static string? Translate(string? message, int depth)
    {
        if (message is null || depth > 5) return message;
        if (English.ContainsKey(message)) return Get(message);
        foreach (var pair in English) if (pair.Value == message) return Get(pair.Key);
        foreach (var template in Templates)
        {
            var match = template.Pattern.Match(message);
            if (!match.Success) continue;
            var output = Language == "en" ? template.English : template.Chinese;
            return Placeholders.Replace(output, placeholder =>
                Translate(match.Groups["p" + placeholder.Groups[1].Value].Value, depth + 1) ?? string.Empty);
        }
        if (message.Contains('\n')) return string.Join('\n', message.Split('\n').Select(line => Translate(line, depth + 1)));
        return message;
    }

    private static readonly Regex Placeholders = new(@"\{(\d+)(?::[^}]+)?\}");
    private sealed record Template(Regex Pattern, string Chinese, string English);
    private static Regex Pattern(string format)
    {
        var parts = new List<string>(); var cursor = 0;
        foreach (Match match in Placeholders.Matches(format))
        {
            parts.Add(Regex.Escape(format[cursor..match.Index]));
            parts.Add("(?<p" + match.Groups[1].Value + ">.*?)"); cursor = match.Index + match.Length;
        }
        parts.Add(Regex.Escape(format[cursor..]));
        return new("\\A" + string.Concat(parts) + "\\z", RegexOptions.Singleline);
    }

    public static IReadOnlyDictionary<string, string> Translations => English;
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["数据目录链接层数过多。"] = "Too many data folder links.",
        ["当前目录：{0}"] = "Current folder: {0}",
        ["输入完整的新目录路径，或点击选择目录"] = "Enter an absolute new folder path, or choose a folder",
        ["选择目录…"] = "Choose folder…",
        ["无法启动，请手动打开 FocusOn：{0}"] = "Unable to start. Please reopen FocusOn manually: {0}",
        ["已通过官方 Release 检查，当前版本 {0} 没有可用的正式更新。"] = "Checked the official releases. No newer public stable release is available for version {0}.",
        ["GitHub 暂时限制了更新请求。请通过“查看 Release”手动下载，或稍后再试。"] = "GitHub is temporarily limiting update requests. Use View release to download manually, or try again later.",
        ["无法连接 GitHub 更新服务。请检查网络，或通过“查看 Release”手动下载。"] = "Unable to reach GitHub updates. Check your connection or use View release to download manually.",
        ["检查更新超时。请稍后再试，或通过“查看 Release”手动下载。"] = "The update check timed out. Try again later or use View release to download manually.",
        ["无法确认更新包大小。请通过“查看 Release”手动下载。"] = "Unable to confirm the package size. Use View release to download manually.",
        ["记录"] = "Records",
        ["记录日期"] = "Record date",
        ["记录列表更新失败"] = "Unable to refresh records",
        ["无法更新记录列表：{0}。已显示的记录可能不是最新。"] = "Unable to refresh records: {0}. Displayed records may be out of date.",
        ["开机启动"] = "Start at login",
        ["登录系统后自动启动 FocusOn；托盘可用时后台运行，否则显示窗口。关闭此选项可取消当前用户的自启，选择会自动保存。"] = "Start FocusOn automatically when you sign in. It runs in the background when the tray is available; otherwise it shows the window. Turn this off to disable startup for your account. Your choice is saved automatically.",
        ["配置"] = "Settings",
        ["外观"] = "Appearance",
        ["主题"] = "Theme",
        ["启动与后台运行"] = "Startup and background",
        ["数据保存"] = "Data storage",
        ["更新"] = "Updates",
        ["关于"] = "About",
        ["打开数据目录"] = "Open data folder",
        ["更改目录…"] = "Change folder…",
        ["检查更新"] = "Check for updates",
        ["查看 Release"] = "View release",
        ["下载并安装…"] = "Download and install…",
        ["下载更新…"] = "Download update…",
        ["选择空的数据目录"] = "Choose an empty data folder",
        ["登录时启动到托盘"] = "Start in the tray at login",
        ["保存并重启"] = "Save and restart",
        ["配置立即生效并保存；系统高对比度优先于主题选择。"] = "Preferences apply immediately and are saved. System high contrast overrides the selected theme.",
        ["隔离数据模式不会更改正式环境的登录自启。"] = "Isolated data mode does not change login startup for your regular installation.",
        ["关闭窗口后继续后台记录；托盘或菜单栏可恢复窗口并退出。不支持托盘时保留可恢复窗口。"] = "Closing the window keeps recording in the background. Restore or exit from the tray or menu bar. Without tray support the window remains recoverable.",
        ["记录仅保存在本机。更改目录会保存、复制并校验数据，然后重启；保留原目录，不合并已有数据。运行中的独立计时将暂停。"] = "Records stay on this computer. Changing folders saves, copies and verifies data, then restarts. The original folder is kept; existing data is never merged. A running independent timer will pause.",
        ["仅在点击时访问 GitHub 检查正式版本，不上传记录。Windows 下载校验后运行安装程序；其他系统手动替换应用。安装包当前未签名。"] = "Checks GitHub for public stable releases only when clicked, without uploading records. Windows verifies the download before running the installer; other systems use manual replacement. Windows installers are currently unsigned.",
        ["离线前台软件使用记录与独立计时。MIT 许可；第三方声明随发布包提供。"] = "Offline foreground app usage and independent timers. MIT license; third-party notices are included in release packages.",
        ["版本 {0} · {1}"] = "Version {0} · {1}",
        ["数据目录尚未初始化"] = "Data directory is not initialized",
        ["无法保存配置：{0}"] = "Unable to save settings: {0}",
        ["无法更改数据目录：{0}"] = "Unable to change data folder: {0}",
        ["无法打开链接或目录。"] = "Unable to open the link or folder.",
        ["无法打开链接：{0}"] = "Unable to open link: {0}",
        ["正在检查更新…"] = "Checking for updates…",
        ["当前版本 {0} 没有可用的正式更新。"] = "No newer public stable release is available for version {0}.",
        ["可用版本 {0}"] = "Version {0} is available",
        ["检查更新失败：{0}"] = "Update check failed: {0}",
        ["更新失败：{0}"] = "Update failed: {0}",
        ["正在下载并校验更新…"] = "Downloading and verifying the update…",
        ["更新将下载并校验安装包，保存记录后退出并启动安装程序。独立计时将暂停。安装包未签名。是否继续？"] = "Download and verify the installer, save records, then exit and start installation? The independent timer will pause. The installer is unsigned.",
        ["保存并复制记录到：{0}。校验完成后重启，原目录保留为备份。独立计时将暂停。是否继续？"] = "Save and copy records to {0}, verify them, then restart? The original folder is kept as a backup. The independent timer will pause.",
        ["操作失败，仍使用原数据目录：{0}"] = "The operation failed. The original data folder is still in use: {0}",
        ["记录已保存。无法重新启动，请手动打开 FocusOn：{0}"] = "Records are saved. Please reopen FocusOn manually; restart failed: {0}",
        ["请选择与当前目录互不包含的空目录。"] = "Choose an empty folder that does not contain, or sit inside, the current data folder.",
        ["目标目录必须为空；不会覆盖或合并已有记录。"] = "The destination must be empty. Existing records will not be overwritten or merged.",
        ["目标数据目录正在使用。"] = "The destination data folder is in use.",
        ["请选择完整数据目录路径。"] = "Choose an absolute data folder path.",
        ["数据目录内含链接，无法安全迁移。"] = "The data folder contains links and cannot be safely migrated.",
        ["复制后的数据库校验失败；仍使用原数据目录。"] = "The copied database failed validation. The original folder remains in use.",
        ["数据文件复制校验失败；仍使用原数据目录。"] = "File copy verification failed. The original folder remains in use.",
        ["正式版本信息无效。"] = "Invalid stable release information.",
        ["更新地址不是官方仓库。"] = "The update URL is not from the official repository.",
        ["此正式版本没有当前系统架构的更新包。"] = "This stable release has no update package for your OS and architecture.",
        ["更新附件重复。"] = "Duplicate update assets.",
        ["更新包缺少唯一校验和。"] = "The update package has no unique checksum.",
        ["更新包大小无效。"] = "Invalid update package size.",
        ["更新校验信息无效。"] = "Invalid update checksum information.",
        ["更新校验信息不一致。"] = "Update checksum information is inconsistent.",
        ["更新包大小不匹配。"] = "Update package size mismatch.",
        ["更新包校验失败；不会安装。"] = "Update verification failed. It will not be installed.",
        ["更新下载重定向过多。"] = "Too many update download redirects.",
        ["更新信息过大。"] = "Update information exceeds the size limit.",
        ["前一天"] = "Previous day", ["后一天"] = "Next day", ["回看日期"] = "Review date",
        ["今天"] = "Today", ["正在启动…"] = "Starting…", ["正在启动"] = "Starting",
        ["暂停记录"] = "Pause recording", ["继续记录"] = "Resume recording", ["退出"] = "Exit",
        ["输入标题，开始独立计时"] = "Enter a title to start an independent timer", ["计时标题"] = "Timer title",
        ["开始计时"] = "Start timer", ["暂停"] = "Pause", ["继续"] = "Resume", ["停止"] = "Stop",
        ["界面语言"] = "Interface language", ["全天软件汇总"] = "Daily app totals",
        ["时长"] = "Duration", ["时段 ⊟"] = "Hours ⊟", ["时段 ⊞"] = "Hours ⊞",
        ["详情 ⊟"] = "Details ⊟", ["详情 ⊞"] = "Details ⊞", ["记录范围"] = "Recorded range",
        ["放大时间线"] = "Zoom in", ["缩小时间线"] = "Zoom out", ["重置"] = "Reset",
        ["时间范围"] = "Time range", ["最近 1 小时"] = "Last 1 hour", ["最近 {0} 小时"] = "Last {0} hours", ["自定义视窗"] = "Custom view",
        ["搜索时间范围内的软件或窗口标题"] = "Search apps or window titles in this time range",
        ["清除范围"] = "Clear range", ["删除所选日期"] = "Delete selected day",
        ["搜索当天软件或窗口标题"] = "Search apps or window titles for this day",
        ["开始时间"] = "Start time", ["软件 / 状态"] = "App / status", ["窗口标题 / 说明"] = "Window title / description",
        ["正在读取记录…"] = "Loading records…", ["记录详情"] = "Record details",
        ["选择一条记录查看详情。"] = "Select a record to view details.",
        ["正在读取所选日期…"] = "Loading the selected day…", ["回看更新失败"] = "Unable to refresh history",
        ["没有匹配的软件或窗口标题。"] = "No matching apps or window titles.",
        ["所选范围没有可用记录。"] = "No records in the selected range.",
        ["{0} 没有可用记录。"] = "No records for {0}.", ["{0} 尚无软件使用片段。"] = "No app usage recorded for {0}.",
        ["{0:HH}:00 时段"] = "{0:HH}:00 hour", ["本地全天"] = "Full local day",
        ["日志范围：{0} — {1} · 全天汇总不变 · 右键点击放大选区"] = "Log range: {0} — {1} · Daily totals unchanged · Right-click to zoom to selection",
        ["搜索：{0} · {1} 条匹配 · 全天汇总不变"] = "Search: {0} · {1} matches · Daily totals unchanged",
        ["{0} · 左键点击或拖选，滚轮缩放，右键拖动平移，右键点击放大选区"] = "{0} · Left-click to select or drag to filter, scroll to zoom, right-drag to pan, right-click to zoom to selection",
        ["请先填写计时标题"] = "Enter a timer title first", ["计时操作失败：{0}"] = "Timer action failed: {0}",
        ["无法更新回看：{0}。已显示的记录可能不是最新。"] = "Unable to refresh history: {0}. Displayed records may be out of date.",
        ["无法保存界面语言：{0}"] = "Unable to save interface language: {0}",
        ["确认删除日期"] = "Confirm day deletion", ["删除"] = "Delete", ["确定"] = "OK", ["取消"] = "Cancel",
        ["删除 {0} 的所有记录？\n本地范围：{1} 至 {2}（不含结束）。\n跨日记录保留其他日期部分。删除无法撤销，删除今天后继续记录未来活动。"] =
            "Delete all records for {0}?\nLocal range: {1} to {2} (end excluded).\nRecords on other days are preserved. This cannot be undone. Deleting today allows future recording to continue.",
        ["删除操作出现异常：{0}"] = "Deletion failed: {0}", [" · 进行中"] = " · Ongoing",
        ["进行中；尾段可能尚未保存。"] = "Ongoing; the latest segment may not have been saved yet.",
        ["打开时间线"] = "Open timeline", ["退出 FocusOn"] = "Exit FocusOn",
        ["系统托盘不可用，关闭按钮会保留窗口。记录和计时继续运行；请使用“退出”保存并关闭程序。"] = "The system tray is unavailable, so Close keeps this window open. Recording and timers continue; use Exit to save and quit.",
        ["使用时间线。方向键选择，Shift 加方向键选择范围，加减键缩放，Escape 取消范围。"] =
            "Usage timeline. Arrow keys select; Shift and arrows select a range; plus and minus zoom; Escape clears the range.",
        ["已选：{0}"] = "Selected: {0}", ["使用"] = "Usage", ["计时"] = "Timer",
        ["{0}小时 {1}分 {2}秒"] = "{0}h {1}m {2}s", ["{0}分 {1}秒"] = "{0}m {1}s",
        ["{0}秒"] = "{0}s", ["小于1秒"] = "Less than 1 second",
        ["软件使用"] = "App usage", ["手动暂停"] = "Manually paused", ["睡眠"] = "Sleep",
        ["锁屏"] = "Locked", ["灭屏"] = "Display off", ["标题计时"] = "Title timer",
        ["未记录"] = "Unrecorded", ["未知软件"] = "Unknown app", ["标题不可用"] = "Title unavailable",
        ["此区间不累计软件使用时长。"] = "App usage is not counted during this interval.",
        ["记录异常"] = "Recording error", ["已停止"] = "Stopped", ["屏幕已关闭"] = "Display off",
        ["正在记录"] = "Recording", ["前台采集不可用"] = "Foreground capture unavailable",
        ["记录会话尚未初始化。"] = "The recording session has not been initialized.",
        ["记录已启动，但未能设置登录自启：{0}"] = "Recording started, but login startup could not be configured: {0}",
        ["无法开始记录：{0}"] = "Unable to start recording: {0}", ["记录控制失败：{0}"] = "Recording control failed: {0}",
        ["退出前记录未能可靠保存：{0}"] = "Records could not be saved reliably before exit: {0}",
        ["请先停止当前计时。"] = "Stop the current timer first.", ["请输入 1 至 200 字的计时标题。"] = "Enter a timer title containing 1 to 200 characters.",
        ["当前没有标题计时。"] = "There is no active title timer.",
        ["标题计时因系统时间或时区变化已暂停；仅保留已保存的时间，请手动继续。"] =
            "The title timer paused after a clock or time-zone change. Only saved time is retained; resume manually.",
        ["标题计时保存失败，已暂停并保留成功保存的时间：{0}"] = "The title timer paused after a save failure. Saved time is retained: {0}",
        ["无法删除记录：{0}"] = "Unable to delete records: {0}", ["无法保存删除后的记录边界：{0}"] = "Unable to save the recording boundary after deletion: {0}",
        ["无法保存记录：{0}"] = "Unable to save records: {0}",
        ["时区已变化；已保存历史未改写，异常后的日期映射尚未验证。"] = "The time zone changed. Saved history remains unchanged; date mapping after the change is unverified.",
        ["系统时间已跳变；已保存历史未改写，异常后的日期映射尚未验证。"] = "The system clock changed. Saved history remains unchanged; date mapping after the change is unverified.",
        ["X11 显示不可用；请在已登录的桌面会话中运行 FocusOn。"] = "The X11 display is unavailable. Run FocusOn in a logged-in desktop session.",
        ["X11 显示不可用。"] = "The X11 display is unavailable.", ["无法启动系统状态观测。"] = "Unable to start system-state observation.",
        ["无法启动前台观测。"] = "Unable to start foreground observation.", ["前台观测已中断：{0}"] = "Foreground observation was interrupted: {0}",
        ["前台观测失败：{0}"] = "Foreground observation failed: {0}",
        ["自启程序位置不存在。"] = "The startup executable does not exist.", ["自启需要发布的 FocusOn 程序。"] = "Login startup requires the published FocusOn executable.",
        ["自启需要固定位置的 Windows 程序路径。"] = "Login startup requires a fixed Windows executable path.",
        ["自启命令超过 Windows Run 的 260 字符限制。"] = "The startup command exceeds the Windows Run limit of 260 characters.",
        ["自启需要固定位置的完整程序路径。"] = "Login startup requires a fixed absolute executable path.",
        ["会话观测已经启动。"] = "Session observation has already started.", ["观测已经启动。"] = "Observation has already started.",
        ["无法确认会话状态：{0}"] = "Unable to confirm session state: {0}",
        ["采样因睡眠或调度停顿中断；空缺不补算，标题计时已暂停，请手动继续。"] =
            "Sampling stopped during sleep or a scheduling gap. Missing time is not counted; the title timer is paused. Resume manually.",
        ["前台读取停顿；无法确认的范围不补算，标题计时已暂停。"] = "Foreground reading stalled. Unobserved time is not counted; the title timer is paused.",
        ["支持的启动参数：--startup 或 --data-dir <目录>。"] = "Supported arguments: --startup or --data-dir <directory>.",
        ["数据目录不能为空。"] = "The data directory cannot be empty.", ["FocusOn 支持 Windows、Linux 与 macOS。"] = "FocusOn supports Windows, Linux and macOS.",
        ["无法读取系统连续计时器。"] = "Unable to read the system continuous clock.", ["无法读取 macOS 连续计时器。"] = "Unable to read the macOS continuous clock.",
        ["macOS 电源通知注册失败。"] = "Unable to register macOS power notifications.",
        ["无法确认 macOS 会话状态：{0}"] = "Unable to confirm macOS session state: {0}", ["无法确认 macOS 屏幕状态。"] = "Unable to confirm macOS display state.",
        ["macOS：请在系统设置 → 隐私与安全性 → 辅助功能中授权 FocusOn，再重新启动。标题计时与回看仍可使用。"] =
            "macOS: Allow FocusOn in System Settings → Privacy & Security → Accessibility, then restart. Title timers and history remain available.",
        ["Wayland：请启用随附的 FocusOn GNOME 扩展以授权前台采集；仍可使用标题计时和回看。"] =
            "Wayland: Enable the bundled FocusOn GNOME extension to authorize foreground capture. Title timers and history remain available.",
        ["Linux：无法确认锁屏和会话状态，请在支持 logind 的本地桌面会话运行。标题计时和历史回看仍可使用。"] =
            "Linux: Lock and session state cannot be confirmed. Run in a local desktop session with logind. Title timers and history remain available.",
        ["logind 未提供当前会话状态；请从本地桌面会话启动。"] = "logind did not provide the current session state. Start from a local desktop session.",
        ["Wayland 前台采集需要启用 FocusOn GNOME 扩展；见 docs/platforms.md。{0}"] = "Wayland foreground capture requires the FocusOn GNOME extension; see docs/platforms.md. {0}",
        ["无法启动 {0}。"] = "Unable to start {0}.", ["{0} 响应超时。"] = "{0} timed out.",
        ["回看"] = "Review",
        ["统计"] = "Statistics",
        ["查看多日或一天内指定时段的前台软件使用情况。"] = "Explore foreground app usage across dates or within part of one day.",
        ["统计范围"] = "Statistics scope",
        ["多日"] = "Multiple days",
        ["单日时段"] = "Single-day period",
        ["开始日期"] = "Start date",
        ["结束日期"] = "End date",
        ["日期"] = "Date",
        ["结束时间"] = "End time",
        ["应用筛选"] = "Apply filters",
        ["近7天"] = "Last 7 days",
        ["近30天"] = "Last 30 days",
        ["全天"] = "Whole day",
        ["前台使用时长"] = "Foreground usage",
        ["使用软件数"] = "Apps used",
        ["平均每天"] = "Daily average",
        ["每小时平均"] = "Hourly average",
        ["软件使用占比"] = "App share",
        ["使用时长分布"] = "Usage duration",
        ["每日使用时长"] = "Daily usage",
        ["时段使用时长"] = "Usage by hour",
        ["软件使用趋势"] = "App trends",
        ["时长排名前三的软件"] = "Top 3 apps by duration",
        ["软件明细"] = "All apps",
        ["时长 · 占比"] = "Duration · Share",
        ["其他软件"] = "Other apps",
        ["所选范围没有软件使用记录。"] = "No app usage in the selected scope.",
        ["请选择日期。"] = "Select a date.",
        ["请选择不晚于今天且不超过366天的日期范围。"] = "Select up to 366 dates ending no later than today.",
        ["请填写完整的开始和结束时间。"] = "Enter both a start and end time.",
        ["时段须在同一天内，开始时间早于结束时间；结束可为24:00。"] = "Use a period within one day with start before end; the end may be 24:00.",
        ["请输入HH:mm格式的时间；结束可为24:00。"] = "Enter time as HH:mm; the end may be 24:00.",
        ["使用本地时间；结束可填写24:00。应用筛选后更新图表。"] = "Use local time; 24:00 is the end of the day. Apply filters to update the charts.",
        ["包含开始和结束日期，最多366天；空日期按零使用显示。"] = "Includes both dates, up to 366 days. Dates without usage stay visible as zero.",
        ["正在统计…"] = "Loading statistics…",
        ["统计更新失败"] = "Statistics update failed",
        ["无法更新统计：{0}"] = "Unable to update statistics: {0}",
        ["已应用范围：{0}"] = "Applied scope: {0}",
        ["更新于 {0:HH:mm:ss}"] = "Updated at {0:HH:mm:ss}",
        ["占比以所选范围的软件使用时长为分母；前五名外的软件合并为“其他软件”。"] = "Share is relative to app usage in this scope. Apps outside the top five are grouped as Other apps.",
        ["仅统计前台软件使用；暂停、未记录和独立计时不计入。当前进行中的尾段可能尚未保存。"] = "Foreground usage only; pauses, gaps and independent timers are excluded. The current live tail may not yet be saved.",
        ["小时"] = "h",
        ["分钟"] = "m",
        ["秒"] = "s",
    };
    private static readonly Template[] Templates = English.Where(pair => pair.Key.Contains('{') && !pair.Key.EndsWith("秒", StringComparison.Ordinal) && pair.Key != "{0:HH}:00 时段")
        .SelectMany(pair => new[] { new Template(Pattern(pair.Key), pair.Key, pair.Value), new Template(Pattern(pair.Value), pair.Key, pair.Value) }).ToArray();
}
