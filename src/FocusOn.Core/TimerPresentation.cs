namespace FocusOn;

public static class TimerPresentation
{
    public static string Clock(TimeSpan value, bool ceil = false)
    {
        var seconds = (long)Math.Max(0, ceil ? Math.Ceiling(value.TotalSeconds) : Math.Floor(value.TotalSeconds));
        return seconds >= 3600 ? $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
    }
    public static string Time(TitleTimerState state) => Clock(state.IsCountdown ? state.Remaining : state.Duration, state.IsCountdown);
    public static string Summary(TitleTimerState state) => state.IsCompleted ? Localizer.Get("已完成 · {0}", Time(state)) :
        Localizer.Get(state.IsCountdown ? "剩余 {0}" : "已用 {0}", Time(state)) + (state.IsRunning ? string.Empty : " · " + Localizer.Get("已暂停"));
}
