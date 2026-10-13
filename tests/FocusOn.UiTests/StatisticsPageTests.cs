using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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
            Assert.Equal(TimeSpan.FromHours(1), page.CurrentView.Duration);
            Assert.All(page.CurrentView.Apps, app => Assert.Equal(TimeSpan.FromMinutes(30), app.Duration));
            var scope = page.FindControl<TextBlock>("ScopeText")!.Text;
            Assert.Contains("09:30 — 10:30", scope); Assert.Contains("1", page.FindControl<TextBlock>("AverageText")!.Text);
            page.FindControl<TextBox>("ToBox")!.Text = "25:00"; await page.ApplyFiltersAsync();
            Assert.True(page.FindControl<TextBlock>("ErrorText")!.IsVisible); Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            await page.RefreshAsync(); Assert.True(page.FindControl<TextBlock>("ErrorText")!.IsVisible); Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            page.FindControl<TextBox>("ToBox")!.Text = "09:00"; await page.ApplyFiltersAsync();
            Assert.Equal(scope, page.FindControl<TextBlock>("ScopeText")!.Text);
            Click(page, "WholeDayButton"); Pump(window); Assert.Equal(TimeSpan.FromDays(1), page.CurrentView!.Plan.Request.EndTime);
            Assert.Equal(TimeSpan.FromHours(2), page.CurrentView.Duration);
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
            Assert.True(page.CurrentView!.Plan.Request.IsPeriod); Assert.Equal(TimeSpan.FromMinutes(30), page.CurrentView.Duration);
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
