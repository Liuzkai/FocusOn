using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace FocusOn.UiTests;

public sealed class ReviewInteractionTests
{
    private static DayView View(int count)
    {
        var day = DateOnly.FromDateTime(DateTime.Today);
        var (start, end) = DailyProjection.Bounds(day, TimeZoneInfo.Local);
        var intervals = Enumerable.Range(0, count).Select(i => new RecordedInterval(i + 1, IntervalKind.Usage,
            start.AddHours(8).AddMinutes(i), start.AddHours(8).AddMinutes(i + 1), TimeSpan.FromMinutes(1),
            i % 2 == 0 ? "editor" : "browser", i % 2 == 0 ? "Editor" : "Browser", "Document " + (i + 1))).ToArray();
        return new(day, start, end, intervals, [new("editor", "Editor", TimeSpan.FromMinutes((count + 1) / 2)),
            new("browser", "Browser", TimeSpan.FromMinutes(count / 2))], "正在记录", null);
    }
    private static void Pump(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
    }
    private static ScrollBar Bar(DataGrid grid) => grid.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PART_VerticalScrollbar");
    private static Point Plot(TimelineControl timeline, Window window, double fraction, double y = 14) => timeline.TranslatePoint(new Point(54 + (timeline.Bounds.Width - 54) * fraction, y), window)!.Value;
    private static void AtEnd(DataGrid grid) => Assert.InRange(Bar(grid).Maximum - Bar(grid).Value, -0.01, 1);

    [AvaloniaFact]
    public async Task Totals_scrollbar_occupies_its_own_row_and_refresh_keeps_the_horizontal_offset()
    {
        var incoming = View(40) with { Totals = Enumerable.Range(0, 20)
            .Select(i => new AppTotal("app" + i, "Application number " + i, TimeSpan.FromMinutes(20 - i))).ToArray() };
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask) { Width = 800 };
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var viewer = window.FindControl<ScrollViewer>("TotalsScrollViewer")!;
            var bar = viewer.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PART_HorizontalScrollBar");
            var list = window.FindControl<ItemsControl>("TotalsList")!;
            Assert.True(bar.IsVisible); Assert.True(bar.Maximum > 1000);
            var contentBottom = list.TranslatePoint(new Point(0, list.Bounds.Height), viewer)!.Value.Y;
            var scrollbarTop = bar.TranslatePoint(default, viewer)!.Value.Y;
            Assert.True(contentBottom <= scrollbarTop, "The scrollbar must not cover the totals chips.");
            viewer.Offset = new Vector(bar.Maximum, 0); Pump(window);
            var offset = viewer.Offset.X; Assert.True(offset > 1000);
            var lastChip = list.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Text == "Application number 19");
            var position = lastChip.TranslatePoint(default, viewer)!.Value;
            Assert.InRange(position.X, 0, viewer.Bounds.Width - lastChip.Bounds.Width);
            await window.RefreshAsync(); Pump(window); Assert.Equal(offset, viewer.Offset.X, 1);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Right_click_zooms_the_range_without_clearing_it_and_right_drag_still_pans()
    {
        var incoming = View(20);
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var from = Plot(timeline, window, .2); var to = Plot(timeline, window, .4);
            window.MouseDown(from, MouseButton.Left); window.MouseMove(to); window.MouseUp(to, MouseButton.Left); Pump(window);
            var range = timeline.Range!.Value;
            var ids = window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Select(x => x.Id).ToArray();
            window.MouseDown(to, MouseButton.Right); window.MouseUp(to, MouseButton.Right); Pump(window);
            Assert.Equal(range.StartUtc, timeline.VisibleStart); Assert.Equal(range.EndUtc, timeline.VisibleEnd);
            Assert.Equal(range, timeline.Range);
            Assert.Equal(ids, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Select(x => x.Id));
            Assert.Equal(2, window.FindControl<ItemsControl>("TotalsList")!.ItemCount);
            var width = timeline.VisibleEnd - timeline.VisibleStart; var previous = timeline.VisibleStart;
            from = Plot(timeline, window, .6); to = Plot(timeline, window, .4);
            window.MouseDown(from, MouseButton.Right); window.MouseMove(to); window.MouseUp(to, MouseButton.Right); Pump(window);
            Assert.Equal(width, timeline.VisibleEnd - timeline.VisibleStart); Assert.True(timeline.VisibleStart > previous); Assert.Equal(range, timeline.Range);
            await window.RefreshAsync(); Pump(window); Assert.Equal(width, timeline.VisibleEnd - timeline.VisibleStart);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None); Pump(window);
            Assert.Null(timeline.Range); Assert.Equal(20, window.FindControl<DataGrid>("LogGrid")!.ItemsSource.Cast<IntervalRow>().Count());
            window.MouseWheel(Plot(timeline, window, .5), new Vector(0, 1)); Pump(window);
            Assert.True(timeline.VisibleEnd - timeline.VisibleStart < width);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public void App_colors_are_distinct_readable_and_survive_new_apps_reordering_and_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-colors-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"language\":\"zh-CN\"}");
            var colors = new ApplicationColors(directory); colors.Register(["FocusOn", "WorkBuddy"]);
            var focus = Assert.IsType<Avalonia.Media.SolidColorBrush>(colors.Get("FocusOn")).Color;
            var buddy = Assert.IsType<Avalonia.Media.SolidColorBrush>(colors.Get("WorkBuddy")).Color;
            Assert.NotEqual(focus, buddy);
            Assert.True(Math.Abs(focus.R - buddy.R) + Math.Abs(focus.G - buddy.G) + Math.Abs(focus.B - buddy.B) > 190);
            var keys = Enumerable.Range(0, 142).Select(x => "app" + x).ToArray(); colors.Register(keys);
            var saved = keys.Append("FocusOn").Append("WorkBuddy").ToDictionary(x => x, x => Assert.IsType<Avalonia.Media.SolidColorBrush>(colors.Get(x)).Color);
            Assert.Equal(saved.Count, saved.Values.Distinct().Count());
            static double Luminance(Avalonia.Media.Color color)
            {
                static double Channel(byte value) => value <= 10 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
                return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
            }
            foreach (var color in saved.Values)
                Assert.True((Luminance(color) + .05) / (Luminance(Avalonia.Media.Color.Parse("#303034")) + .05) >= 4.5);
            var restarted = new ApplicationColors(directory); restarted.Register(keys.Reverse().Append("newly installed app"));
            foreach (var (key, color) in saved) Assert.Equal(color, Assert.IsType<Avalonia.Media.SolidColorBrush>(restarted.Get(key)).Color);
            Assert.Equal("{\"language\":\"zh-CN\"}", File.ReadAllText(Path.Combine(directory, "settings.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public void Invalid_color_preferences_are_preserved_and_do_not_prevent_session_colors()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-colors-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "app-colors.json"); File.WriteAllText(path, "{\"app\":null}");
            var colors = new ApplicationColors(directory); var before = colors.Get("FocusOn"); colors.Register(["WorkBuddy"]);
            Assert.Same(before, colors.Get("FocusOn")); Assert.Equal("{\"app\":null}", File.ReadAllText(path));
            var unwritable = new ApplicationColors(path); var session = unwritable.Get("FocusOn");
            unwritable.Register(["WorkBuddy"]); Assert.Same(session, unwritable.Get("FocusOn"));
            Assert.Equal("{\"app\":null}", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    [AvaloniaFact]
    public async Task Hover_follows_the_mouse_within_one_interval_and_reopens_without_a_delay()
    {
        Localizer.SetLanguage("en");
        var incoming = View(2);
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var popup = timeline.GetLogicalDescendants().OfType<Popup>().Single(x => x.Name == "TimelineHoverPopup");
            window.MouseMove(Plot(timeline, window, 0.08)); Pump(window);
            Assert.True(popup.IsOpen); Assert.NotNull(popup.Host);
            var tip = Assert.IsType<ToolTip>(popup.Child); var text = Assert.IsType<TextBlock>(tip.Content);
            Assert.Contains("Document 1", text.Text); var first = tip.PointToScreen(default);
            window.MouseMove(Plot(timeline, window, 0.2)); Pump(window);
            Assert.True(popup.IsOpen); Assert.Same(tip, popup.Child);
            Assert.True(tip.PointToScreen(default).X > first.X + 40, "The visible hint must move with the pointer inside the same interval.");
            window.MouseMove(Plot(timeline, window, 0.7)); Pump(window);
            Assert.Contains("Document 2", text.Text); Assert.Same(tip, popup.Child);
            window.MouseMove(Plot(timeline, window, 0.3, 50)); Pump(window); Assert.False(popup.IsOpen);
            window.MouseMove(Plot(timeline, window, 0.2)); Pump(window); Assert.True(popup.IsOpen);
            incoming = incoming with { Intervals = [incoming.Intervals[0] with { WindowTitle = "Updated title" }, incoming.Intervals[1]] };
            await window.RefreshAsync(); Pump(window); Assert.Contains("Updated title", text.Text);
            window.MouseMove(timeline.TranslatePoint(new Point(10, 14), window)!.Value); Pump(window); Assert.False(popup.IsOpen);
        }
        finally { window.StopRefresh(); window.Close(); Localizer.SetLanguage("zh-CN"); }
    }

    [AvaloniaFact]
    public async Task Hover_closes_for_drag_collapse_window_hiding_and_shutdown()
    {
        var window = new MainWindow(_ => Task.FromResult(View(2)), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var popup = timeline.GetLogicalDescendants().OfType<Popup>().Single(x => x.Name == "TimelineHoverPopup");
            var start = Plot(timeline, window, 0.2); var end = Plot(timeline, window, 0.4);
            window.MouseMove(start); Pump(window); Assert.True(popup.IsOpen);
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end); Pump(window); Assert.False(popup.IsOpen);
            window.MouseUp(end, MouseButton.Left); timeline.ClearRange();
            window.MouseMove(start); Pump(window); Assert.True(popup.IsOpen);
            timeline.IsVisible = false; Pump(window); Assert.False(popup.IsOpen);
            timeline.IsVisible = true; Pump(window); window.MouseMove(Plot(timeline, window, 0.25)); Pump(window); Assert.True(popup.IsOpen);
            window.Hide(); Assert.False(popup.IsOpen);
            window.Show(); Pump(window); window.MouseMove(Plot(timeline, window, 0.3)); Pump(window); Assert.True(popup.IsOpen);
            window.Close(); Assert.False(popup.IsOpen); Assert.Null(popup.Host);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Decorative_app_boundary_keeps_half_open_selection_and_exact_duration()
    {
        var incoming = View(2);
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var timeline = window.FindControl<TimelineControl>("TimelineView")!;
            var boundary = Plot(timeline, window, 0.5);
            window.MouseDown(boundary, MouseButton.Left); window.MouseUp(boundary, MouseButton.Left); Pump(window);
            var selected = Assert.IsType<IntervalRow>(window.FindControl<DataGrid>("LogGrid")!.SelectedItem);
            Assert.Equal(2, selected.Id); Assert.Equal("Document 2", selected.Title);
            Assert.Equal(DisplayText.Duration(incoming.Intervals[1].Duration, true), selected.PreciseDuration);
            Assert.Equal(incoming.Intervals[1].StartUtc, timeline.VisibleStart.AddTicks((timeline.VisibleEnd - timeline.VisibleStart).Ticks / 2));
            Assert.Equal(2, window.FindControl<ItemsControl>("TotalsList")!.ItemCount);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Log_end_follows_multiple_appends_and_resumes_after_scrolling_back_to_the_end()
    {
        var incoming = View(40);
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var grid = window.FindControl<DataGrid>("LogGrid")!; Assert.True(Bar(grid).Maximum > 500); Assert.Equal(0, Bar(grid).Value);
            var selected = grid.ItemsSource.Cast<IntervalRow>().Last(); grid.SelectedItem = selected;
            grid.ScrollIntoView(selected, null); Pump(window); AtEnd(grid);
            for (var count = 41; count <= 45; count++)
            {
                incoming = View(count); await window.RefreshAsync(); Pump(window); AtEnd(grid); Assert.Same(selected, grid.SelectedItem);
            }
            var point = grid.TranslatePoint(new Point(400, 80), window)!.Value;
            window.MouseWheel(point, new Vector(0, 5)); Pump(window);
            Assert.True(Bar(grid).Value < Bar(grid).Maximum - 100); var oldOffset = Bar(grid).Value;
            incoming = View(46); await window.RefreshAsync(); Pump(window); Assert.Equal(oldOffset, Bar(grid).Value, 1); Assert.Same(selected, grid.SelectedItem);
            grid.ScrollIntoView(grid.ItemsSource.Cast<IntervalRow>().Last(), null); Pump(window); AtEnd(grid);
            incoming = View(47); await window.RefreshAsync(); Pump(window); AtEnd(grid);
        }
        finally { window.StopRefresh(); window.Close(); }
    }

    [AvaloniaFact]
    public async Task Selecting_history_at_the_bottom_pauses_following_and_a_pending_append_does_not_override_scroll_up()
    {
        var incoming = View(40);
        var window = new MainWindow(_ => Task.FromResult(incoming), _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        try
        {
            window.Show(); await window.StartRefreshAsync(); Pump(window);
            var grid = window.FindControl<DataGrid>("LogGrid")!;
            grid.ScrollIntoView(grid.ItemsSource.Cast<IntervalRow>().Last(), null); Pump(window); AtEnd(grid);
            var historical = grid.ItemsSource.Cast<IntervalRow>().ElementAt(38); grid.SelectedItem = historical; Pump(window); var oldOffset = Bar(grid).Value;
            incoming = View(41); await window.RefreshAsync(); Pump(window); Assert.Equal(oldOffset, Bar(grid).Value, 1); Assert.Same(historical, grid.SelectedItem);
            grid.ScrollIntoView(grid.ItemsSource.Cast<IntervalRow>().Last(), null); Pump(window); AtEnd(grid);
            incoming = View(42); await window.RefreshAsync();
            window.MouseWheel(grid.TranslatePoint(new Point(400, 80), window)!.Value, new Vector(0, 4)); Pump(window);
            Assert.True(Bar(grid).Value < Bar(grid).Maximum - 100); oldOffset = Bar(grid).Value;
            incoming = View(43); await window.RefreshAsync(); Pump(window); Assert.Equal(oldOffset, Bar(grid).Value, 1);
        }
        finally { window.StopRefresh(); window.Close(); }
    }
}
