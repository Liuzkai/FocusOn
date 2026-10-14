using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace FocusOn.UiTests;

public sealed class StatisticsPageTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);
    private static readonly DateTimeOffset Now = DailyProjection.Bounds(Today, TimeZoneInfo.Local).StartUtc.AddHours(12);
    private static DayView Day(DateOnly date)
    {
        var (start, end) = DailyProjection.Bounds(date, TimeZoneInfo.Local);
        return new(date, start, end,
            [new(date.DayNumber * 10 + 1, IntervalKind.Usage, start.AddHours(9), start.AddHours(10), TimeSpan.FromHours(1), "editor", "用户编辑器", "User title"),
             new(date.DayNumber * 10 + 2, IntervalKind.Usage, start.AddHours(10), start.AddHours(11), TimeSpan.FromHours(1), "browser", "Browser", "Other title")],
            [new("editor", "用户编辑器", TimeSpan.FromHours(1)), new("browser", "Browser", TimeSpan.FromHours(1))], "正在记录", null, new("User timer", TimeSpan.FromMinutes(20), true));
    }
    private static StatisticsView Stats(StatisticsRequest request)
    {
        var plan = StatisticsProjection.Plan(request, Now, TimeZoneInfo.Local);
        var values = Enumerable.Range(0, request.EndDate.DayNumber - request.StartDate.DayNumber + 1)
            .SelectMany(i => Day(request.StartDate.AddDays(i)).Intervals).Select(StatisticsAllocation.From).ToArray();
        return StatisticsProjection.Create(plan, values);
    }
    private static MainWindow Window(Func<StatisticsRequest, Task<StatisticsView>>? stats = null) =>
        new(date => Task.FromResult(Day(date)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            utcNow: () => Now, readStatistics: stats ?? (request => Task.FromResult(Stats(request))));
    private static void Click(Control parent, string name) => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact]
    public async Task Language_refreshes_the_visible_scope_caption_without_resetting_a_period_or_its_validation()
    {
        Localizer.SetLanguage("en"); var window = Window();
        try
        {
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            var mode = page.FindControl<ComboBox>("ModePicker")!; mode.SelectedIndex = 1;
            var date = Today.AddDays(-1); page.FindControl<CalendarDatePicker>("StartDatePicker")!.SelectedDate = date.ToDateTime(TimeOnly.MinValue);
            await page.ApplyFiltersAsync();
            var applied = page.CurrentView!.Plan.Request;
            page.FindControl<TextBox>("ToBox")!.Text = "25:00"; await page.ApplyFiltersAsync();
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 0; Pump(window);
            Assert.Contains(mode.GetVisualDescendants().OfType<TextBlock>(), x => x.Text == "单日时段");
            Assert.Equal(date.ToDateTime(TimeOnly.MinValue), page.FindControl<CalendarDatePicker>("StartDatePicker")!.SelectedDate);
            Assert.Equal("25:00", page.FindControl<TextBox>("ToBox")!.Text);
            Assert.True(page.FindControl<TextBlock>("ErrorText")!.IsVisible); Assert.Equal(applied, page.CurrentView.Plan.Request);
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public void Static_hover_follows_buckets_and_exposes_the_same_details_to_accessibility_without_changing_totals()
    {
        var window = Window(); window.Height = 1340;
        try
        {
            window.ConfigureSettings(Path.GetTempPath(), true, new(StatisticsAnimations: false), _ => { }, _ => Task.CompletedTask, _ => Task.CompletedTask);
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            var chart = page.FindControl<StatisticsChart>("BarsChart")!;
            var original = page.CurrentView!;
            void Move(int index)
            {
                var point = chart.TranslatePoint(new Point(48 + (chart.Bounds.Width - 64) / 7 * (index + .5), 100), window)!.Value;
                window.MouseMove(point); Pump(window);
            }
            Move(0); Assert.Contains(Today.AddDays(-6).ToString("yyyy-MM-dd"), AutomationProperties.GetItemStatus(chart));
            var hint = chart.GetLogicalDescendants().OfType<Popup>().Single(); Assert.True(hint.IsOpen);
            Assert.True(hint.Child!.Opacity > 0); // A logically open popup must be visible under the Fluent tooltip style.
            Move(6); Assert.Contains(Today.ToString("yyyy-MM-dd"), AutomationProperties.GetItemStatus(chart));
            Assert.Equal(((TextBlock)((ToolTip)hint.Child!).Content!).Text, AutomationProperties.GetItemStatus(chart));
            Assert.Same(original, page.CurrentView); Assert.Equal(TimeSpan.FromHours(14), page.CurrentView!.Duration);
            Click(window, "ReviewPageButton"); Assert.False(hint.IsOpen); Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(chart));
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public void High_contrast_keyboard_details_remain_available_and_window_hiding_clears_interaction()
    {
        var window = Window(); window.Height = 1340;
        try
        {
            ReviewTheme.Apply(Application.Current!.Resources, true);
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!; page.SetMotionEnabled(true);
            var chart = page.FindControl<StatisticsChart>("TrendChart")!;
            chart.Focus(); window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None); Pump(window);
            var hint = chart.GetLogicalDescendants().OfType<Popup>().Single(); Assert.True(hint.IsOpen);
            Assert.Contains(Today.ToString("yyyy-MM-dd"), AutomationProperties.GetItemStatus(chart));
            window.Hide(); Assert.False(hint.IsOpen); Assert.Equal(string.Empty, AutomationProperties.GetItemStatus(chart));
            window.Show(); Pump(window); Assert.False(hint.IsOpen);
        }
        finally { ReviewTheme.Apply(Application.Current!.Resources, false); window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Navigation_loads_seven_days_and_keeps_review_zoom_selection_timer_and_language()
    {
        Localizer.SetLanguage("zh-CN");
        var window = Window(); window.FindControl<ComboBox>("RangePicker")!.SelectedIndex = 2;
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var log = window.FindControl<DataGrid>("LogGrid")!; var selected = log.ItemsSource.Cast<IntervalRow>().First(); log.SelectedItem = selected;
            var timeline = window.FindControl<TimelineControl>("TimelineView")!; timeline.ZoomIn(); var start = timeline.VisibleStart; var end = timeline.VisibleEnd;
            Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            Assert.True(page.IsVisible); Assert.False(window.FindControl<Grid>("ReviewPane")!.IsVisible);
            Assert.Equal(TimeSpan.FromHours(14), page.CurrentView!.Duration);
            Assert.Equal(7, page.CurrentView.Buckets.Count); Assert.Equal("14小时", page.FindControl<TextBlock>("TotalText")!.Text);
            Assert.Equal(2, page.FindControl<ItemsControl>("AppList")!.ItemCount);
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 1;
            Assert.Equal("Statistics", page.FindControl<TextBlock>("Heading")!.Text);
            Assert.Equal("Single-day period", page.FindControl<ComboBoxItem>("PeriodItem")!.Content);
            Assert.Contains(page.FindControl<ItemsControl>("AppList")!.ItemsSource!.Cast<StatisticsAppRow>(), x => x.Name == "用户编辑器");
            Click(window, "ReviewPageButton"); Pump(window);
            Assert.Same(selected, log.SelectedItem); Assert.Equal(start, timeline.VisibleStart); Assert.Equal(end, timeline.VisibleEnd);
            Assert.Equal("User timer", window.FindControl<TextBox>("TimerTitleBox")!.Text);
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public async Task A_single_day_period_clips_usage_and_invalid_filters_keep_the_applied_scope()
    {
        var window = Window();
        try
        {
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            page.FindControl<ComboBox>("ModePicker")!.SelectedIndex = 1;
            page.FindControl<TextBox>("FromBox")!.Text = "09:30"; page.FindControl<TextBox>("ToBox")!.Text = "10:30";
            await page.ApplyFiltersAsync(); Pump(window);
            Assert.Equal(Today, page.CurrentView!.Plan.Request.StartDate);
            Assert.Equal(TimeSpan.FromHours(1), page.CurrentView!.Duration);
            Assert.All(page.CurrentView.Apps, app => Assert.Equal(TimeSpan.FromMinutes(30), app.Duration));
            var scope = page.FindControl<TextBlock>("ScopeText")!.Text;
            Assert.Contains("09:30 — 10:30", scope); Assert.Contains("1", page.FindControl<TextBlock>("AverageText")!.Text);
            page.FindControl<TextBox>("ToBox")!.Text = "25:00"; await page.ApplyFiltersAsync();
            Assert.True(page.FindControl<TextBlock>("ErrorText")!.IsVisible); Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            await page.RefreshAsync(); Assert.True(page.FindControl<TextBlock>("ErrorText")!.IsVisible); Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            page.FindControl<TextBox>("ToBox")!.Text = "09:00"; await page.ApplyFiltersAsync();
            Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            Click(page, "WholeDayButton"); Pump(window); Assert.Equal(TimeSpan.FromDays(1), page.CurrentView!.Plan.Request.EndTime);
            Assert.Equal(TimeSpan.FromHours(2), page.CurrentView!.Duration);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_late_old_query_never_overwrites_the_new_period_and_close_cancels_presentation()
    {
        var pending = new TaskCompletionSource<StatisticsView>(); var requests = new List<StatisticsRequest>();
        Task<StatisticsView> Read(StatisticsRequest request) { requests.Add(request); return requests.Count == 1 ? pending.Task : Task.FromResult(Stats(request)); }
        var window = Window(Read);
        try
        {
            window.Show(); Click(window, "StatisticsPageButton");
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            page.FindControl<ComboBox>("ModePicker")!.SelectedIndex = 1;
            page.FindControl<TextBox>("FromBox")!.Text = "09:30"; page.FindControl<TextBox>("ToBox")!.Text = "10:00";
            await page.ApplyFiltersAsync(); pending.SetResult(Stats(requests[0])); await Task.Yield(); Pump(window);
            Assert.True(page.CurrentView!.Plan.Request.IsPeriod); Assert.Equal(TimeSpan.FromMinutes(30), page.CurrentView!.Duration);
            Assert.Equal("1小时", page.FindControl<TextBlock>("AverageText")!.Text);
            window.StopRefresh(); var count = requests.Count; await page.RefreshAsync(); Assert.Equal(count, requests.Count);
        }
        finally { pending.TrySetResult(Stats(new(Today, Today))); window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Narrow_layout_stacks_charts_and_keeps_the_full_app_list_readable_in_high_contrast()
    {
        var window = Window(); window.Width = 760;
        try
        {
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            var bars = page.FindControl<Border>("BarsCard")!; var trends = page.FindControl<Border>("TrendCard")!;
            Assert.Equal(0, Grid.GetColumn(trends)); Assert.Equal(1, Grid.GetRow(trends));
            Assert.True(trends.Bounds.Y > bars.Bounds.Y + bars.Bounds.Height);
            ReviewTheme.Apply(Application.Current!.Resources, true); page.ApplyLanguage(); Pump(window);
            Assert.All(page.FindControl<ItemsControl>("AppList")!.ItemsSource!.Cast<StatisticsAppRow>(), row => Assert.Equal(Avalonia.Media.Brushes.White, row.Color));
            Assert.Equal(TimeSpan.FromHours(14), page.CurrentView!.Duration);
            window.Width = 1100; Pump(window); Assert.Equal(1, Grid.GetColumn(trends)); Assert.Equal(0, Grid.GetRow(trends));
            Assert.Equal(bars.Bounds.Y, trends.Bounds.Y);
        }
        finally { ReviewTheme.Apply(Application.Current!.Resources, false); window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public void Charts_expose_bucket_details_by_keyboard_and_Escape_closes_the_hint()
    {
        var window = Window();
        try
        {
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            var chart = page.FindControl<StatisticsChart>("BarsChart")!;
            chart.Focus(); window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None); Pump(window);
            var hint = chart.GetLogicalDescendants().OfType<Popup>().Single(); Assert.True(hint.IsOpen);
            var text = (TextBlock)((ToolTip)hint.Child!).Content!; Assert.Contains(Today.AddDays(-6).ToString("yyyy-MM-dd"), text.Text);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None); Pump(window); Assert.Contains(Today.ToString("yyyy-MM-dd"), text.Text);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Assert.False(hint.IsOpen);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Empty_and_error_states_are_readable_and_chart_pointer_details_close_on_navigation()
    {
        var fail = false;
        var window = Window(request => fail ? Task.FromException<StatisticsView>(new IOException("disk unavailable")) : Task.FromResult(Stats(request)));
        try
        {
            window.Show(); Click(window, "StatisticsPageButton"); Pump(window);
            var page = window.FindControl<StatisticsPage>("StatisticsView")!;
            var chart = page.FindControl<StatisticsChart>("ShareChart")!;
            var point = chart.TranslatePoint(new Point(chart.Bounds.Width / 2 + 80, chart.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point); Pump(window);
            var hint = chart.GetLogicalDescendants().OfType<Popup>().Single(); Assert.True(hint.IsOpen);
            Assert.Contains("50", ((TextBlock)((ToolTip)hint.Child!).Content!).Text);
            Click(window, "ReviewPageButton"); Assert.False(hint.IsOpen);
            Click(window, "StatisticsPageButton"); fail = true; await page.RefreshAsync();
            Assert.Contains("disk unavailable", page.FindControl<TextBlock>("ErrorText")!.Text); Assert.NotNull(page.CurrentView);
            fail = false; page.FindControl<ComboBox>("ModePicker")!.SelectedIndex = 1;
            page.FindControl<TextBox>("FromBox")!.Text = "06:00"; page.FindControl<TextBox>("ToBox")!.Text = "07:00";
            await page.ApplyFiltersAsync(); Assert.True(page.FindControl<TextBlock>("EmptyText")!.IsVisible);
            Assert.Equal("0", page.FindControl<TextBlock>("AppsText")!.Text);
        }
        finally { window.StopRefresh(); window.Close(); }
    }
}
