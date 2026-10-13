using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Xunit;
using Avalonia.Input;
using Avalonia.VisualTree;

[assembly: AvaloniaTestApplication(typeof(FocusOn.UiTests.TestAppBuilder))]

namespace FocusOn.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://FocusOn.UiTests/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
    }
}

public sealed class MainWindowTests
{
    private static DayView View()
    {
        var day = DateOnly.FromDateTime(DateTime.Today);
        var (start, end) = DailyProjection.Bounds(day, TimeZoneInfo.Local);
        return new(day, start, end,
            [new(1, IntervalKind.Usage, start.AddHours(8), start.AddHours(9), TimeSpan.FromHours(1), "editor", "编辑器", "计划.md"),
             new(2, IntervalKind.Usage, start.AddHours(9), start.AddHours(10), TimeSpan.FromHours(1), "browser", "浏览器", "搜索页面")],
            [new("editor", "编辑器", TimeSpan.FromHours(1)), new("browser", "浏览器", TimeSpan.FromHours(1))], "正在记录", null);
    }
    private static void Click(MainWindow window, string name) => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static MainWindow CreateWindow(Func<DateOnly, Task<DayView>> read, Func<DateOnly, Task> delete,
        Func<TimerAction, string?, Task> timer, Action<string>? language = null)
    {
        var window = new MainWindow(read, delete, timer, language, () => View().StartUtc.AddHours(10));
        window.FindControl<ComboBox>("RangePicker")!.SelectedIndex = 1;
        return window;
    }

    [AvaloniaFact]
    public async Task Switching_languages_preserves_records_timer_selection_and_zoom_and_saves_preference()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-language-" + Guid.NewGuid().ToString("N"));
        var timerChanges = 0;
        Localizer.SetLanguage("zh-CN");
        var original = View() with { Timer = new("用户标题 · 不翻译", TimeSpan.FromMinutes(5), true), Warning = "Linux：无法确认锁屏和会话状态，请在支持 logind 的本地桌面会话运行。标题计时和历史回看仍可使用。" };
        var window = CreateWindow(_ => Task.FromResult(original), _ => Task.CompletedTask, (_, _) => { timerChanges++; return Task.CompletedTask; }, language => LanguagePreference.Save(directory, language));
        try
        {
            window.Show(); await window.StartRefreshAsync();
            var log = window.FindControl<DataGrid>("LogGrid")!; var selected = log.ItemsSource.Cast<IntervalRow>().First(); log.SelectedItem = selected;
            var timeline = window.FindControl<TimelineControl>("TimelineView")!; timeline.ZoomIn(); var start = timeline.VisibleStart; var end = timeline.VisibleEnd;
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 1;
            Assert.Equal("en", LanguagePreference.Load(directory, System.Globalization.CultureInfo.GetCultureInfo("zh-CN")));
            Assert.Equal("Pause recording", window.FindControl<Button>("PauseButton")!.Content);
            Assert.Equal("Daily app totals", window.FindControl<TextBlock>("TotalsHeading")!.Text);
            Assert.Equal("App / status", log.Columns[1].Header); Assert.Equal("App usage", selected.Kind);
            Assert.StartsWith("Linux: Lock and session state cannot be confirmed.", window.FindControl<TextBlock>("WarningText")!.Text);
            Assert.Equal("用户标题 · 不翻译", window.FindControl<TextBox>("TimerTitleBox")!.Text);
            Assert.Equal("计划.md", selected.Title); Assert.Equal("编辑器", selected.Application);
            Assert.Same(selected, log.SelectedItem); Assert.Equal(start, timeline.VisibleStart); Assert.Equal(end, timeline.VisibleEnd);
            Assert.Equal(2, window.FindControl<ItemsControl>("TotalsList")!.ItemCount); Assert.Equal(0, timerChanges);
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 0;
            Assert.Equal("zh-CN", LanguagePreference.Load(directory, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal("软件使用", selected.Kind); Assert.Equal("暂停记录", window.FindControl<Button>("PauseButton")!.Content);
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task Failed_language_save_keeps_the_previous_language_and_recording_state()
    {
        Localizer.SetLanguage("zh-CN");
        var window = CreateWindow(_ => Task.FromResult(View()), _ => Task.CompletedTask, (_, _) => Task.CompletedTask, _ => throw new IOException("denied"));
        try
        {
            window.Show(); await window.StartRefreshAsync(); window.SetPauseState(true);
            window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex = 1;
            Assert.Equal("zh-CN", Localizer.Language); Assert.Equal(0, window.FindControl<ComboBox>("LanguagePicker")!.SelectedIndex);
            Assert.Equal("继续记录", window.FindControl<Button>("PauseButton")!.Content);
            Assert.Contains("denied", window.FindControl<TextBlock>("WarningText")!.Text);
            Assert.Equal(2, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Count());
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public async Task English_delete_confirmation_and_cached_diagnostics_leave_user_text_unchanged()
    {
        Localizer.SetLanguage("en");
        var deleted = false;
        var window = CreateWindow(_ => Task.FromResult(View()), _ => { deleted = true; return Task.CompletedTask; }, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Click(window, "DeleteDayButton");
            var dialog = Assert.Single(window.OwnedWindows);
            Assert.Equal("Confirm day deletion", dialog.Title);
            var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Equal("Delete", buttons.Single(x => x.Name == "AcceptButton").Content);
            buttons.Single(x => x.Name == "CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Yield(); Assert.False(deleted);
            Assert.Equal("Unable to save records: database is locked", Localizer.Translate("无法保存记录：database is locked"));
            Assert.Equal("custom diagnostics", Localizer.Translate("custom diagnostics"));
            var userRecord = View().Intervals[0] with { WindowTitle = "记录异常 · 未记录 · 自己的标题" };
            Assert.Equal(userRecord.WindowTitle, DisplayText.Title(userRecord));
            Localizer.SetLanguage("zh-CN");
            Assert.Equal("无法保存记录：database is locked", Localizer.Translate("Unable to save records: database is locked"));
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public async Task Search_changes_logs_and_preserves_full_day_totals_and_selection_details()
    {
        var window = CreateWindow(_ => Task.FromResult(View()), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            var log = window.FindControl<DataGrid>("LogGrid")!; var totals = window.FindControl<ItemsControl>("TotalsList")!;
            Assert.Equal(2, log.ItemsSource.Cast<IntervalRow>().Count()); Assert.Equal(2, totals.ItemCount);
            window.FindControl<TextBox>("SearchBox")!.Text = "计划";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var row = Assert.Single(log.ItemsSource.Cast<IntervalRow>());
            Assert.Equal("计划.md", row.Title); Assert.Equal(2, totals.ItemCount);
            log.SelectedItem = row;
            Assert.True(window.FindControl<Border>("InspectorPane")!.IsVisible);
            Assert.Equal(row, window.FindControl<StackPanel>("DetailsPanel")!.DataContext);
            await window.RefreshAsync(); Assert.Same(row, log.SelectedItem);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Timer_buttons_follow_saved_state_and_stop_leaves_no_active_timer()
    {
        TitleTimerState? timer = null;
        var actions = new List<TimerAction>();
        var window = CreateWindow(_ => Task.FromResult(View() with { Timer = timer }), _ => Task.CompletedTask, (action, title) =>
        {
            actions.Add(action);
            timer = action == TimerAction.Stop ? null : new(title!, TimeSpan.FromSeconds(5), action != TimerAction.Pause);
            return Task.CompletedTask;
        });
        try
        {
            window.Show(); await window.StartRefreshAsync();
            window.FindControl<TextBox>("TimerTitleBox")!.Text = "阅读";
            Click(window, "StartTimerButton");
            Assert.False(window.FindControl<Button>("StartTimerButton")!.IsVisible);
            Assert.True(window.FindControl<TextBox>("TimerTitleBox")!.IsReadOnly);
            Click(window, "PauseTimerButton"); Assert.Equal("继续", window.FindControl<Button>("PauseTimerButton")!.Content);
            Click(window, "PauseTimerButton"); Assert.Equal("暂停", window.FindControl<Button>("PauseTimerButton")!.Content);
            Click(window, "StopTimerButton"); Assert.True(window.FindControl<Button>("StartTimerButton")!.IsVisible);
            Assert.Equal(new[] { TimerAction.Start, TimerAction.Pause, TimerAction.Resume, TimerAction.Stop }, actions);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Timeline_uses_recorded_bounds_and_keeps_zoom_during_refresh()
    {
        var window = CreateWindow(_ => Task.FromResult(View()), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            Assert.Equal(TimeSpan.FromHours(2), timeline.VisibleEnd - timeline.VisibleStart);
            Click(window, "ZoomInButton"); var first = timeline.VisibleStart; var last = timeline.VisibleEnd;
            Assert.Equal(TimeSpan.FromHours(1), last - first);
            await window.RefreshAsync(); Assert.Equal(first, timeline.VisibleStart); Assert.Equal(last, timeline.VisibleEnd);
            window.FindControl<ComboBox>("RangePicker")!.SelectedIndex = 7; await window.RefreshAsync(); Assert.Equal(TimeSpan.FromHours(10), timeline.VisibleEnd - timeline.VisibleStart);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Missing_capture_permission_is_visible_while_history_stays_readable()
    {
        var window = CreateWindow(_ => Task.FromResult(View() with { Status = "前台采集不可用", Warning = "请授权辅助功能" }), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            Assert.True(window.FindControl<Border>("WarningPanel")!.IsVisible);
            Assert.Contains("辅助功能", window.FindControl<TextBlock>("WarningText")!.Text);
            Assert.Equal(2, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Count());
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Drag_range_filters_intersecting_logs_without_changing_full_day_totals()
    {
        var window = CreateWindow(_ => Task.FromResult(View()), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var plot = timeline.Bounds.Width - 54;
            var start = timeline.TranslatePoint(new Point(54 + plot * 0.05, 14), window)!.Value;
            var end = timeline.TranslatePoint(new Point(54 + plot * 0.45, 14), window)!.Value;
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left);
            Assert.NotNull(timeline.Range);
            Assert.Single(window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>());
            Assert.Equal(2, window.FindControl<ItemsControl>("TotalsList")!.ItemCount);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Assert.Null(timeline.Range);
            Assert.Equal(2, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Count());
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Date_deletion_requires_confirmation_and_cancellation_preserves_records()
    {
        var deleted = new List<DateOnly>();
        var window = CreateWindow(_ => Task.FromResult(View()), day => { deleted.Add(day); return Task.CompletedTask; }, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            Click(window, "DeleteDayButton");
            var dialog = Assert.Single(window.OwnedWindows);
            dialog.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Yield();
            Assert.Empty(deleted);
            Assert.Equal(2, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Count());
            Click(window, "DeleteDayButton"); dialog = Assert.Single(window.OwnedWindows);
            dialog.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "AcceptButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Yield();
            Assert.Equal(View().Date, Assert.Single(deleted));
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task High_contrast_keeps_readable_labels_and_replaces_application_colors()
    {
        var window = CreateWindow(_ => Task.FromResult(View()), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync();
            ReviewTheme.Apply(Application.Current!.Resources, true);
            Assert.Equal(Avalonia.Media.Brushes.White, ReviewTheme.ApplicationBrush("editor"));
            var row = window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().First();
            Assert.Equal("编辑器", row.Application); Assert.Equal("软件使用", row.Kind);
            Assert.Equal(Avalonia.Media.Brushes.Black, row.BadgeBackground);
        }
        finally { ReviewTheme.Apply(Application.Current!.Resources, false); window.StopRefresh(); window.Close(); }
    }
}
