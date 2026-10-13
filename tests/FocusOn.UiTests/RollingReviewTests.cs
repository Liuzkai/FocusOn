using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace FocusOn.UiTests;

public sealed class RollingReviewTests
{
    private static readonly DateOnly Date = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateTimeOffset Midnight = DailyProjection.Bounds(Date, TimeZoneInfo.Local).StartUtc;
    private static DayView Day(DateOnly date)
    {
        var (start, end) = DailyProjection.Bounds(date, TimeZoneInfo.Local);
        RecordedInterval[] records = [
            new(1, IntervalKind.Usage, Midnight.AddMinutes(-50), Midnight.AddMinutes(-20), TimeSpan.FromMinutes(30), "browser", "Browser", "Before midnight"),
            new(2, IntervalKind.Usage, Midnight.AddMinutes(-20), Midnight.AddMinutes(10), TimeSpan.FromMinutes(30), "editor", "Editor", "Across midnight"),
            new(3, IntervalKind.Usage, Midnight.AddMinutes(10), Midnight.AddMinutes(20), TimeSpan.FromMinutes(10), "chat", "Chat", "Today record"),
            new(4, IntervalKind.TaskTimer, Midnight.AddMinutes(-10), Midnight.AddMinutes(30), TimeSpan.FromMinutes(40), null, null, "User timer"),
            new(5, IntervalKind.Usage, Midnight.AddHours(-8), Midnight.AddHours(-7), TimeSpan.FromHours(1), "older", "Older app", "Older record")];
        var intervals = records.Select(x => DailyProjection.Clip(x, start, end)).OfType<RecordedInterval>().ToArray();
        return new(date, start, end, intervals, intervals.Where(x => x.Kind == IntervalKind.Usage).GroupBy(x => x.AppKey)
            .Select(g => new AppTotal(g.Key!, g.First().AppName!, TimeSpan.FromTicks(g.Sum(x => x.Duration.Ticks)))).ToArray(),
            "正在记录", null, new("User timer", TimeSpan.FromMinutes(40), true));
    }
    private static ReviewView Review(ReviewRequest request, DateTimeOffset now)
    {
        var plan = ReviewProjection.Plan(request, now, TimeZoneInfo.Local);
        return ReviewProjection.Create(plan, plan.Dates.Select(Day).ToArray());
    }
    private static void Click(MainWindow window, string name) => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static IntervalRow[] Rows(MainWindow window) => window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().ToArray();

    [AvaloniaFact]
    public async Task Presets_show_cross_day_logs_while_Today_and_daily_totals_keep_local_date_scope()
    {
        Localizer.SetLanguage("en");
        var now = Midnight.AddMinutes(30);
        var window = new MainWindow(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => now, readReview: request => Task.FromResult(Review(request, now)));
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var picker = window.FindControl<ComboBox>("RangePicker")!;
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            Assert.Equal(0, picker.SelectedIndex);
            Assert.Equal(new[] { "1h", "2h", "4h", "6h", "8h", "12h", "24h", "Today" }, picker.Items.Cast<ComboBoxItem>().Select(x => x.Content));
            Assert.Equal(TimeSpan.FromHours(1), timeline.VisibleEnd - timeline.VisibleStart);
            Assert.Equal(new long[] { 1, 2, 4, 3 }, Rows(window).Select(x => x.Id));
            var editor = Rows(window).Single(x => x.Id == 2);
            Assert.Equal("Across midnight", editor.Title); Assert.StartsWith(Date.AddDays(-1).ToString("MM-dd"), editor.StartTime);
            Assert.Equal(DisplayText.Duration(TimeSpan.FromMinutes(30), true), editor.PreciseDuration);
            var totals = window.FindControl<ItemsControl>("TotalsList")!;
            Assert.Equal(new[] { "Chat", "Editor" }, totals.ItemsSource!.Cast<object>().Select(x => (string)x.GetType().GetProperty("AppName")!.GetValue(x)!).Order());
            for (var index = 1; index <= 6; index++)
            {
                picker.SelectedIndex = index; await window.RefreshAsync(); Pump(window);
                Assert.Equal(TimeSpan.FromHours(new[] { 1, 2, 4, 6, 8, 12, 24 }[index]), timeline.VisibleEnd - timeline.VisibleStart);
            }
            Assert.Contains(Rows(window), x => x.Id == 5); Assert.Equal(2, totals.ItemCount);
            picker.SelectedIndex = 7; await window.RefreshAsync(); Pump(window);
            Assert.Equal(Midnight, timeline.VisibleStart); Assert.Equal(now, timeline.VisibleEnd);
            Assert.Equal(new long[] { 2, 4, 3 }, Rows(window).Select(x => x.Id));
            Assert.Equal(DisplayText.Duration(TimeSpan.FromMinutes(10), true), Rows(window).Single(x => x.Id == 2).PreciseDuration);
            Assert.Equal(2, totals.ItemCount); Assert.Equal("User timer", window.FindControl<TextBox>("TimerTitleBox")!.Text);
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 0;
            Assert.Equal("今天", picker.Items.Cast<ComboBoxItem>().Last().Content); Assert.Equal(7, picker.SelectedIndex);
            Assert.Equal(now, timeline.VisibleEnd); Assert.Equal("Across midnight", Rows(window).First().Title);
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public async Task Refresh_follows_preset_until_zoom_then_Reset_restores_live_following()
    {
        var now = Midnight.AddMinutes(30);
        var window = new MainWindow(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => now, readReview: request => Task.FromResult(Review(request, now)));
        try
        {
            window.Show(); await window.StartRefreshAsync();
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            now = now.AddMinutes(1); await window.RefreshAsync();
            Assert.Equal(now, timeline.VisibleEnd); Assert.True(timeline.IsFollowingPreset);
            Click(window, "ZoomInButton"); var start = timeline.VisibleStart; var end = timeline.VisibleEnd;
            Assert.False(timeline.IsFollowingPreset); Assert.Equal(TimeSpan.FromMinutes(30), end - start);
            now = now.AddMinutes(10); await window.RefreshAsync();
            Assert.Equal(start, timeline.VisibleStart); Assert.Equal(end, timeline.VisibleEnd);
            Click(window, "ResetDayButton"); Assert.True(timeline.IsFollowingPreset); Assert.Equal(now, timeline.VisibleEnd);
            Assert.Equal(TimeSpan.FromHours(1), timeline.VisibleEnd - timeline.VisibleStart);
            now = now.AddMinutes(1); await window.RefreshAsync(); Assert.Equal(now, timeline.VisibleEnd);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Manual_viewport_survives_midnight_while_daily_date_advances_and_Reset_returns_to_now()
    {
        var now = Midnight.AddMinutes(-1);
        var window = new MainWindow(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => now, readReview: request => Task.FromResult(Review(request, now)));
        try
        {
            window.Show(); await window.StartRefreshAsync(); Click(window, "ZoomInButton");
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var start = timeline.VisibleStart; var end = timeline.VisibleEnd;
            now = Midnight.AddMinutes(1); await window.RefreshAsync(); Pump(window);
            Assert.Equal(Date.ToDateTime(TimeOnly.MinValue), window.FindControl<CalendarDatePicker>("ViewDatePicker")!.SelectedDate);
            Assert.Equal(start, timeline.VisibleStart); Assert.Equal(end, timeline.VisibleEnd); Assert.False(timeline.IsFollowingPreset);
            Click(window, "ResetDayButton"); Assert.Equal(now, timeline.VisibleEnd);
            Assert.Equal(TimeSpan.FromHours(1), timeline.VisibleEnd - timeline.VisibleStart); Assert.True(timeline.IsFollowingPreset);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Stale_inflight_snapshot_cannot_overwrite_a_new_preset()
    {
        var now = Midnight.AddMinutes(30);
        var pending = new TaskCompletionSource<ReviewView>();
        var requests = new List<ReviewRequest>();
        Task<ReviewView> Read(ReviewRequest request) { requests.Add(request); return requests.Count == 1 ? pending.Task : Task.FromResult(Review(request, now)); }
        var window = new MainWindow(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => now, readReview: Read);
        try
        {
            window.Show(); var initial = window.StartRefreshAsync();
            window.FindControl<ComboBox>("RangePicker")!.SelectedIndex = 6;
            pending.SetResult(Review(requests[0], now)); await initial; Pump(window);
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            Assert.Equal(new int?[] { 1, 24 }, requests.Select(x => x.Hours));
            Assert.Equal(TimeSpan.FromHours(24), timeline.VisibleEnd - timeline.VisibleStart);
            Assert.Contains(Rows(window), x => x.Id == 5);
        }
        finally { pending.TrySetResult(Review(new(Date, 1), now)); window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Choosing_Today_returns_from_history_and_empty_windows_are_explicit()
    {
        var now = Midnight.AddMinutes(30);
        var window = new MainWindow(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => now, readReview: request => Task.FromResult(Review(request, now)));
        try
        {
            window.Show(); await window.StartRefreshAsync();
            var datePicker = window.FindControl<CalendarDatePicker>("ViewDatePicker")!;
            datePicker.SelectedDate = Date.AddDays(-3).ToDateTime(TimeOnly.MinValue); Pump(window); await window.RefreshAsync();
            Assert.Empty(Rows(window)); Assert.True(window.FindControl<TextBlock>("LogEmptyText")!.IsVisible);
            window.FindControl<ComboBox>("RangePicker")!.SelectedIndex = 7; await window.RefreshAsync();
            Assert.Equal(Date.ToDateTime(TimeOnly.MinValue), datePicker.SelectedDate);
            Assert.Equal(Midnight, window.FindControl<TimelineControl>("TimelineView")!.VisibleStart);
            Assert.NotEmpty(Rows(window));
        }
        finally { window.StopRefresh(); window.Close(); }
    }
}
