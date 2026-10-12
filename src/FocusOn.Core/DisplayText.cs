using System.Globalization;

namespace FocusOn;

public static class DisplayText
{
    public static bool MatchesSearch(RecordedInterval interval, string query) => query.Length == 0
        || AppName(interval).Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Title(interval).Contains(query, StringComparison.CurrentCultureIgnoreCase);

    public static string Duration(TimeSpan value, bool precise = false)
    {
        value = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        var hours = (int)value.TotalHours;
        var seconds = precise
            ? value.Seconds.ToString("0", CultureInfo.CurrentCulture) + "." + value.Milliseconds.ToString("000", CultureInfo.CurrentCulture)
            : value.Seconds.ToString(CultureInfo.CurrentCulture);
        if (hours > 0) return Localizer.Get("{0}小时 {1}分 {2}秒", hours, value.Minutes, seconds);
        if (value.Minutes > 0) return Localizer.Get("{0}分 {1}秒", value.Minutes, seconds);
        if (!precise && value < TimeSpan.FromSeconds(1)) return Localizer.Get("小于1秒");
        return Localizer.Get("{0}秒", seconds);
    }

    public static string Kind(IntervalKind kind) => Localizer.Get(kind switch
    {
        IntervalKind.Usage => "软件使用",
        IntervalKind.ManualPause => "手动暂停",
        IntervalKind.Sleep => "睡眠",
        IntervalKind.Locked => "锁屏",
        IntervalKind.DisplayOff => "灭屏",
        IntervalKind.TaskTimer => "标题计时",
        _ => "未记录"
    });

    public static string AppName(RecordedInterval interval) => interval.Kind == IntervalKind.Usage
        ? interval.AppKey == "unknown" || string.IsNullOrWhiteSpace(interval.AppName) ? Localizer.Get("未知软件") : interval.AppName
        : Kind(interval.Kind);

    public static string Title(RecordedInterval interval) => interval.Kind is IntervalKind.Usage or IntervalKind.TaskTimer
        ? string.IsNullOrWhiteSpace(interval.WindowTitle) || interval.Kind == IntervalKind.Usage && interval.WindowTitle == "标题不可用" ? Localizer.Get("标题不可用") : interval.WindowTitle
        : Localizer.Get("此区间不累计软件使用时长。");

    public static string Time(DateTimeOffset utc, bool full = false) => utc.ToLocalTime()
        .ToString(full ? "yyyy-MM-dd HH:mm:ss.fff zzz" : "HH:mm:ss", CultureInfo.CurrentCulture);
}
