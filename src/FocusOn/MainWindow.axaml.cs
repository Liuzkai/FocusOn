using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Platform;

namespace FocusOn;

public partial class MainWindow : Window
{
    private readonly Func<DateOnly, Task<DayView>> readDay;
    private readonly Func<DateOnly, Task> deleteDay;
    private readonly Func<TimerAction, string?, Task> changeTimer;
    private readonly ObservableCollection<IntervalRow> rows = [];
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateOnly selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private bool followToday = true, started, stopped, refreshing, deleting, changingTimer, showGroups = true;
    private long timerVersion;
    private DayView? view;
    private TitleTimerState? timerState;
    private string search = string.Empty;

    public MainWindow() : this(_ => Task.FromException<DayView>(new InvalidOperationException("记录会话尚未初始化。")),
        _ => Task.CompletedTask, (_, _) => Task.CompletedTask) { }

    public MainWindow(Func<DateOnly, Task<DayView>> readDay, Func<DateOnly, Task> deleteDay, Func<TimerAction, string?, Task> changeTimer)
    {
        this.readDay = readDay; this.deleteDay = deleteDay; this.changeTimer = changeTimer;
        InitializeComponent();
        if (PlatformSettings is { } settings)
        {
            void ApplyTheme(PlatformColorValues colors)
            {
                if (Application.Current is { } app) ReviewTheme.Apply(app.Resources, colors.ContrastPreference == ColorContrastPreference.High);
                foreach (var row in rows) row.RefreshAppearance();
                TimelineView.InvalidateVisual();
                if (started && !stopped) _ = RefreshAsync();
            }
            ApplyTheme(settings.GetColorValues());
            EventHandler<PlatformColorValues> themeChanged = (_, colors) => Dispatcher.UIThread.Post(() => ApplyTheme(colors));
            settings.ColorValuesChanged += themeChanged;
            Closed += (_, _) => settings.ColorValuesChanged -= themeChanged;
        }
        LogGrid.ItemsSource = rows;
        PreviousDayButton.Click += (_, _) => { if (selectedDate.DayNumber > 0) SelectDate(selectedDate.AddDays(-1)); };
        NextDayButton.Click += (_, _) => SelectDate(selectedDate.AddDays(1));
        TodayButton.Click += (_, _) => SelectDate(DateOnly.FromDateTime(DateTime.Today));
        ViewDatePicker.SelectedDateChanged += (_, _) => { if (ViewDatePicker.SelectedDate is { } date) SelectDate(DateOnly.FromDateTime(date)); };
        PauseButton.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        ExitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        DeleteDayButton.Click += async (_, _) => await DeleteDayAsync();
        StartTimerButton.Click += async (_, _) => await ChangeTimerAsync(TimerAction.Start);
        PauseTimerButton.Click += async (_, _) => await ChangeTimerAsync(timerState?.IsRunning == true ? TimerAction.Pause : TimerAction.Resume);
        StopTimerButton.Click += async (_, _) => await ChangeTimerAsync(TimerAction.Stop);
        SearchBox.TextChanged += (_, _) => { search = SearchBox.Text?.Trim() ?? string.Empty; TimelineView.SetSearch(search); ApplyRows(); };
        DurationButton.Click += (_, _) => TimelineView.IsVisible = !TimelineView.IsVisible;
        GroupsButton.Click += (_, _) => { showGroups = !showGroups; GroupsButton.Content = showGroups ? "时段 ⊟" : "时段 ⊞"; ApplyRows(); };
        DetailsButton.Click += (_, _) => SetInspector(!InspectorPane.IsVisible);
        RecordedModeButton.Click += (_, _) => TimelineView.SetFullDay(false);
        FullDayModeButton.Click += (_, _) => TimelineView.SetFullDay(true);
        ZoomInButton.Click += (_, _) => TimelineView.ZoomIn();
        ZoomOutButton.Click += (_, _) => TimelineView.ZoomOut();
        ResetDayButton.Click += (_, _) => TimelineView.ResetDay();
        ClearRangeButton.Click += (_, _) => TimelineView.ClearRange();
        TimelineView.RangeChanged += _ => ApplyRows();
        TimelineView.ViewChanged += UpdateTimelineControls;
        TimelineView.IntervalSelected += SelectInterval;
        LogGrid.SelectionChanged += (_, _) =>
        {
            var row = LogGrid.SelectedItem as IntervalRow;
            DetailsPanel.DataContext = row; DetailsPanel.IsVisible = row is not null; DetailsEmptyText.IsVisible = row is null;
            if (row is not null) { TimelineView.SelectInterval(row.Id); SetInspector(true); }
            else TimelineView.ClearSelection();
        };
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        Closed += (_, _) => StopRefresh();
        UpdateDateControls(); UpdateTimelineControls();
    }

    public event EventHandler? PauseRequested;
    public event EventHandler? ExitRequested;
    public event Action<DayView>? DayRefreshed;
    public void SetPauseState(bool paused) => PauseButton.Content = paused ? "继续记录" : "暂停记录";
    public void StopRefresh() { stopped = true; refreshTimer.Stop(); }
    public async Task StartRefreshAsync()
    {
        if (started || stopped) return;
        started = true; await RefreshAsync(); if (!stopped) refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        if (refreshing || stopped || deleting) return;
        refreshing = true;
        if (followToday && selectedDate != DateOnly.FromDateTime(DateTime.Today)) SelectDate(DateOnly.FromDateTime(DateTime.Today));
        var requestedDate = selectedDate; var requestedTimerVersion = timerVersion;
        try
        {
            var incoming = await readDay(requestedDate);
            if (stopped || deleting || requestedDate != selectedDate || requestedTimerVersion != timerVersion) return;
            view = incoming; TimelineView.SetDay(incoming); StatusText.Text = incoming.Status;
            ShowWarning(incoming.Warning); ShowTimer(incoming.Timer); ApplyRows();
            TotalsList.ItemsSource = incoming.Totals.OrderByDescending(x => x.Duration).Select(x => new { x.AppName, Duration = DisplayText.Duration(x.Duration), Color = ReviewTheme.ApplicationBrush(x.AppKey) }).ToArray();
            TotalsEmptyText.Text = $"{incoming.Date:yyyy-MM-dd} 尚无软件使用片段。"; TotalsEmptyText.IsVisible = incoming.Totals.Count == 0;
            DayRefreshed?.Invoke(incoming);
        }
        catch (Exception error)
        {
            if (stopped || requestedDate != selectedDate) return;
            StatusText.Text = "回看更新失败"; ShowWarning($"无法更新回看：{error.Message}。已显示的记录可能不是最新。");
        }
        finally
        {
            refreshing = false;
            if (!stopped && (requestedDate != selectedDate || requestedTimerVersion != timerVersion)) _ = RefreshAsync();
        }
    }

    private void SelectDate(DateOnly day)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (day > today) day = today;
        followToday = day == today;
        if (selectedDate == day) { UpdateDateControls(); return; }
        selectedDate = day; view = null; TimelineView.Clear(); rows.Clear(); TotalsList.ItemsSource = null;
        LogEmptyText.Text = "正在读取所选日期…"; LogEmptyText.IsVisible = true;
        UpdateDateControls(); if (started) _ = RefreshAsync();
    }
    private void UpdateDateControls()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        ViewDatePicker.DisplayDateEnd = DateTime.Today;
        var date = selectedDate.ToDateTime(TimeOnly.MinValue);
        if (ViewDatePicker.SelectedDate != date) ViewDatePicker.SelectedDate = date;
        PreviousDayButton.IsEnabled = selectedDate.DayNumber > 0; NextDayButton.IsEnabled = selectedDate < today; TodayButton.IsEnabled = selectedDate != today;
        DateText.Text = $"{selectedDate:yyyy-MM-dd}"; Title = $"FocusOn · {selectedDate:yyyy-MM-dd}";
    }
    private void ApplyRows()
    {
        if (view is null) return;
        var filtered = TimelineView.Range is { } range ? view.FilterRange(range.StartUtc, range.EndUtc) : view.Intervals;
        var incoming = filtered.Where(x => DisplayText.MatchesSearch(x, search)).OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
        var ids = incoming.Select(x => x.Id).ToHashSet();
        for (var i = rows.Count - 1; i >= 0; i--) if (!ids.Contains(rows[i].Id)) rows.RemoveAt(i);
        var existing = rows.ToDictionary(x => x.Id);
        for (var i = 0; i < incoming.Length; i++)
        {
            var interval = incoming[i];
            if (existing.TryGetValue(interval.Id, out var row)) { row.Update(interval); if (rows[i] != row) rows.Move(rows.IndexOf(row), i); }
            else rows.Insert(i, new IntervalRow(interval));
            var local = interval.StartUtc.ToLocalTime();
            var previous = i > 0 ? incoming[i - 1].StartUtc.ToLocalTime() : (DateTimeOffset?)null;
            rows[i].SetGroupLabel(showGroups && (previous is null || previous.Value.Date != local.Date || previous.Value.Hour != local.Hour) ? $"{local:HH}:00 时段" : string.Empty);
        }
        LogEmptyText.Text = search.Length > 0 ? "没有匹配的软件或窗口标题。" : TimelineView.Range is not null ? "所选范围没有可用记录。" : $"{view.Date:yyyy-MM-dd} 没有可用记录。";
        LogEmptyText.IsVisible = rows.Count == 0; UpdateTimelineControls();
    }
    private void SelectInterval(long id)
    {
        var row = rows.FirstOrDefault(x => x.Id == id);
        if (row is null && search.Length > 0) { SearchBox.Text = string.Empty; row = rows.FirstOrDefault(x => x.Id == id); }
        if (row is null && TimelineView.Range is not null) { TimelineView.ClearRange(); row = rows.FirstOrDefault(x => x.Id == id); }
        if (row is null) return;
        LogGrid.SelectedItem = row; LogGrid.ScrollIntoView(row, null);
    }
    private void SetInspector(bool show) { InspectorPane.IsVisible = show; DetailsButton.Content = show ? "详情 ⊟" : "详情 ⊞"; }
    private void UpdateTimelineControls()
    {
        ZoomInButton.IsEnabled = TimelineView.CanZoomIn; ZoomOutButton.IsEnabled = TimelineView.CanZoomOut;
        ResetDayButton.IsEnabled = view is not null; ClearRangeButton.IsEnabled = TimelineView.Range is not null;
        RecordedModeButton.FontWeight = TimelineView.FullDay ? FontWeight.Normal : FontWeight.Bold;
        FullDayModeButton.FontWeight = TimelineView.FullDay ? FontWeight.Bold : FontWeight.Normal;
        RangeText.Text = TimelineView.Range is { } range ? $"日志范围：{DisplayText.Time(range.StartUtc)} — {DisplayText.Time(range.EndUtc)} · 全天汇总不变"
            : search.Length > 0 ? $"搜索：{search} · {rows.Count} 条匹配 · 全天汇总不变"
            : $"{(TimelineView.FullDay ? "本地全天" : "记录范围")} · 拖选筛选，滚轮缩放，右键平移或取消范围";
    }
    private void ShowTimer(TitleTimerState? state)
    {
        timerState = state; TimerTitleBox.IsReadOnly = state is not null;
        if (state is not null) TimerTitleBox.Text = state.Title;
        StartTimerButton.IsVisible = state is null; PauseTimerButton.IsVisible = StopTimerButton.IsVisible = state is not null;
        PauseTimerButton.Content = state?.IsRunning == true ? "暂停" : "继续";
        TimerElapsedText.Text = state is null ? string.Empty : $"{(int)state.Duration.TotalHours:00}:{state.Duration.Minutes:00}:{state.Duration.Seconds:00}";
    }
    private async Task ChangeTimerAsync(TimerAction action)
    {
        if (changingTimer || deleting || stopped) return;
        if (action == TimerAction.Start && string.IsNullOrWhiteSpace(TimerTitleBox.Text)) { TimerErrorText.Text = "请先填写计时标题"; TimerTitleBox.Focus(); return; }
        changingTimer = true; StartTimerButton.IsEnabled = PauseTimerButton.IsEnabled = StopTimerButton.IsEnabled = false; TimerErrorText.Text = string.Empty;
        try
        {
            await changeTimer(action, TimerTitleBox.Text); timerVersion++;
            if (action == TimerAction.Stop) TimerTitleBox.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception error) { TimerErrorText.Text = "计时操作失败：" + error.Message; }
        finally { changingTimer = false; StartTimerButton.IsEnabled = PauseTimerButton.IsEnabled = StopTimerButton.IsEnabled = true; }
    }
    private async Task DeleteDayAsync()
    {
        if (deleting || changingTimer || stopped) return;
        var day = selectedDate; var (start, end) = DailyProjection.Bounds(day, TimeZoneInfo.Local);
        if (!await MessageDialog.ShowAsync(this, $"删除 {day:yyyy-MM-dd} 的所有记录？\n本地范围：{DisplayText.Time(start, true)} 至 {DisplayText.Time(end, true)}（不含结束）。\n跨日记录保留其他日期部分。删除无法撤销，删除今天后继续记录未来活动。", confirm: true)) return;
        deleting = true; DeleteDayButton.IsEnabled = false;
        try { await deleteDay(day); TimelineView.ClearRange(); LogGrid.SelectedItem = null; }
        catch (Exception error) { await MessageDialog.ShowAsync(this, "删除操作出现异常：" + error.Message); }
        finally { deleting = false; DeleteDayButton.IsEnabled = !stopped; await RefreshAsync(); }
    }
    public void ShowWarning(string? message) { WarningText.Text = message; WarningPanel.IsVisible = !string.IsNullOrWhiteSpace(message); }
}

public sealed class IntervalRow(RecordedInterval interval) : INotifyPropertyChanged
{
    private RecordedInterval value = interval;
    public long Id => value.Id;
    public string Application => DisplayText.AppName(value) + (value.IsCurrent ? " · 进行中" : string.Empty);
    public string StartTime => DisplayText.Time(value.StartUtc);
    public string Title => DisplayText.Title(value);
    public string Duration => DisplayText.Duration(value.Duration);
    public string PreciseDuration => DisplayText.Duration(value.Duration, true);
    public string StartFull => DisplayText.Time(value.StartUtc, true);
    public string EndFull => DisplayText.Time(value.EndUtc, true);
    public string CurrentNote => value.IsCurrent ? "进行中；尾段可能尚未保存。" : string.Empty;
    public string Kind => DisplayText.Kind(value.Kind);
    public IBrush Color => value.Kind == IntervalKind.TaskTimer ? ReviewTheme.Accent : value.Kind == IntervalKind.Usage ? ReviewTheme.ApplicationBrush(value.AppKey) : ReviewTheme.Muted;
    public IBrush BadgeBackground => ReviewTheme.HighContrast ? ReviewTheme.Background : Color is SolidColorBrush color ? new SolidColorBrush(color.Color, 0.15) : ReviewTheme.Surface;
    public string GroupLabel { get; private set; } = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshAppearance() => PropertyChanged?.Invoke(this, new(null));
    public void Update(RecordedInterval incoming) { if (value == incoming) return; value = incoming; PropertyChanged?.Invoke(this, new(null)); }
    public void SetGroupLabel(string label) { if (label == GroupLabel) return; GroupLabel = label; PropertyChanged?.Invoke(this, new(nameof(GroupLabel))); }
}
