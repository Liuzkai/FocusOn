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
        ["前一天"] = "Previous day", ["后一天"] = "Next day", ["回看日期"] = "Review date",
        ["今天"] = "Today", ["正在启动…"] = "Starting…", ["正在启动"] = "Starting",
        ["暂停记录"] = "Pause recording", ["继续记录"] = "Resume recording", ["退出"] = "Exit",
        ["输入标题，开始独立计时"] = "Enter a title to start an independent timer", ["计时标题"] = "Timer title",
        ["开始计时"] = "Start timer", ["暂停"] = "Pause", ["继续"] = "Resume", ["停止"] = "Stop",
        ["界面语言"] = "Interface language", ["全天软件汇总"] = "Daily app totals",
        ["时长"] = "Duration", ["时段 ⊟"] = "Hours ⊟", ["时段 ⊞"] = "Hours ⊞",
        ["详情 ⊟"] = "Details ⊟", ["详情 ⊞"] = "Details ⊞", ["记录范围"] = "Recorded range",
        ["放大时间线"] = "Zoom in", ["缩小时间线"] = "Zoom out", ["重置"] = "Reset",
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
        ["日志范围：{0} — {1} · 全天汇总不变"] = "Log range: {0} — {1} · Daily totals unchanged",
        ["搜索：{0} · {1} 条匹配 · 全天汇总不变"] = "Search: {0} · {1} matches · Daily totals unchanged",
        ["{0} · 拖选筛选，滚轮缩放，右键平移或取消范围"] = "{0} · Drag to filter, scroll to zoom, right-drag to pan or right-click to clear",
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
    };
    private static readonly Template[] Templates = English.Where(pair => pair.Key.Contains('{') && !pair.Key.EndsWith("秒", StringComparison.Ordinal) && pair.Key != "{0:HH}:00 时段")
        .SelectMany(pair => new[] { new Template(Pattern(pair.Key), pair.Key, pair.Value), new Template(Pattern(pair.Value), pair.Key, pair.Value) }).ToArray();
}
