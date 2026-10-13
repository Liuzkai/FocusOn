using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Automation;

namespace FocusOn;

public partial class MainWindow : Window
{
    private readonly Func<DateOnly, Task<DayView>> readDay;
    private readonly Func<DateOnly, Task> deleteDay;
    private readonly Func<TimerAction, string?, Task> changeTimer;
    private readonly Action<string>? saveLanguage;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<ReviewRequest, Task<ReviewView>>? readReview;
    private readonly ObservableCollection<IntervalRow> rows = [];
    private readonly LogEndFollower logEndFollower;
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateOnly selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private bool followToday = true, started, stopped, refreshing, deleting, changingTimer, showGroups = true, applyingLanguage, manualPaused;
    private long timerVersion, rangeVersion;
    private int? rangeHours = 1;
    private bool resetRange = true, applyingRows;
    private ReviewView? review;
    private DayView? view;
    private TitleTimerState? timerState;
    private string search = string.Empty;

    public MainWindow() : this(_ => Task.FromException<DayView>(new InvalidOperationException("记录会话尚未初始化。")),
        _ => Task.CompletedTask, (_, _) => Task.CompletedTask) { }

    public MainWindow(Func<DateOnly, Task<DayView>> readDay, Func<DateOnly, Task> deleteDay, Func<TimerAction, string?, Task> changeTimer,
        Action<string>? saveLanguage = null, Func<DateTimeOffset>? utcNow = null, Func<ReviewRequest, Task<ReviewView>>? readReview = null,
        Func<StatisticsRequest, Task<StatisticsView>>? readStatistics = null)
    {
        this.readDay = readDay; this.deleteDay = deleteDay; this.changeTimer = changeTimer; this.saveLanguage = saveLanguage;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow); this.readReview = readReview; selectedDate = Today();
        InitializeComponent();
        StatisticsView.Configure(readStatistics ?? ReadStatisticsFallback, this.utcNow);
        ReviewPageButton.Click += (_, _) => ShowPage(false);
        StatisticsPageButton.Click += (_, _) => ShowPage(true);
        PropertyChanged += (_, change) => { if (change.Property == IsVisibleProperty) StatisticsView.SetActive(IsVisible && StatisticsView.IsVisible); };
        ShowPage(false);
        if (PlatformSettings is { } settings)
        {
            void ApplyTheme(PlatformColorValues colors)
            {
                if (Application.Current is { } app) ReviewTheme.Apply(app.Resources, colors.ContrastPreference == ColorContrastPreference.High);
                foreach (var row in rows) row.RefreshAppearance();
                TimelineView.InvalidateVisual(); TotalsScrollViewer.InvalidateVisual();
                StatisticsView.ApplyLanguage(); ShowPage(StatisticsView.IsVisible);
                if (started && !stopped) _ = RefreshAsync();
            }
            ApplyTheme(settings.GetColorValues());
            EventHandler<PlatformColorValues> themeChanged = (_, colors) => Dispatcher.UIThread.Post(() => ApplyTheme(colors));
            settings.ColorValuesChanged += themeChanged;
            Closed += (_, _) => settings.ColorValuesChanged -= themeChanged;
        }
        LogGrid.ItemsSource = rows;
        logEndFollower = new(LogGrid, () => LogGrid.CollectionView.Cast<object>().LastOrDefault());
        PreviousDayButton.Click += (_, _) => { if (selectedDate.DayNumber > 0) SelectDate(selectedDate.AddDays(-1)); };
        NextDayButton.Click += (_, _) => SelectDate(selectedDate.AddDays(1));
        TodayButton.Click += (_, _) => SelectDate(Today());
        ViewDatePicker.SelectedDateChanged += (_, _) => { if (ViewDatePicker.SelectedDate is { } date) SelectDate(DateOnly.FromDateTime(date)); };
        PauseButton.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        ExitButton.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        DeleteDayButton.Click += async (_, _) => await DeleteDayAsync();
        StartTimerButton.Click += async (_, _) => await ChangeTimerAsync(TimerAction.Start);
        PauseTimerButton.Click += async (_, _) => await ChangeTimerAsync(timerState?.IsRunning == true ? TimerAction.Pause : TimerAction.Resume);
        StopTimerButton.Click += async (_, _) => await ChangeTimerAsync(TimerAction.Stop);
        SearchBox.TextChanged += (_, _) => { search = SearchBox.Text?.Trim() ?? string.Empty; TimelineView.SetSearch(search); ApplyRows(); };
        DurationButton.Click += (_, _) => TimelineView.IsVisible = !TimelineView.IsVisible;
        GroupsButton.Click += (_, _) => { showGroups = !showGroups; GroupsButton.Content = Localizer.Get(showGroups ? "时段 ⊟" : "时段 ⊞"); ApplyRows(); };
        DetailsButton.Click += (_, _) => SetInspector(!InspectorPane.IsVisible);
        RangePicker.SelectionChanged += (_, _) =>
        {
            if (applyingLanguage || RangePicker.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
            rangeHours = tag == "today" ? null : int.Parse(tag, System.Globalization.CultureInfo.InvariantCulture);
            rangeVersion++; resetRange = true;
            if (rangeHours is null) SelectDate(Today());
            if (started && !stopped) _ = RefreshAsync();
        };
        ZoomInButton.Click += (_, _) => TimelineView.ZoomIn();
        ZoomOutButton.Click += (_, _) => TimelineView.ZoomOut();
        ResetDayButton.Click += (_, _) => TimelineView.ResetDay();
        ClearRangeButton.Click += (_, _) => TimelineView.ClearRange();
        TimelineView.RangeChanged += _ => ApplyRows();
        TimelineView.ViewChanged += () => { UpdateTimelineControls(); ApplyRows(); };
        TimelineView.IntervalSelected += SelectInterval;
        LogGrid.SelectionChanged += (_, _) =>
        {
            var row = LogGrid.SelectedItem as IntervalRow;
            if (row is not null && row != rows.LastOrDefault()) logEndFollower.Pause();
            DetailsPanel.DataContext = row; DetailsPanel.IsVisible = row is not null; DetailsEmptyText.IsVisible = row is null;
            if (row is not null) { TimelineView.SelectInterval(row.Id); SetInspector(true); }
            else TimelineView.ClearSelection();
        };
        refreshTimer.Tick += async (_, _) => await RefreshAsync();
        Closed += (_, _) => StopRefresh();
        LanguagePicker.SelectionChanged += (_, _) =>
        {
            if (applyingLanguage || LanguagePicker.SelectedItem is not ComboBoxItem { Tag: string language } || language == Localizer.Language) return;
            try { this.saveLanguage?.Invoke(language); Localizer.SetLanguage(language); ApplyLanguage(); LanguageChanged?.Invoke(); }
            catch (Exception error) { ApplyLanguage(); ShowWarning(Localizer.Get("无法保存界面语言：{0}", Localizer.Translate(error.Message))); }
        };
        ApplyLanguage(); UpdateDateControls(); UpdateTimelineControls();
    }

    public event EventHandler? PauseRequested;
    private DateOnly Today() => DateOnly.FromDateTime(utcNow().LocalDateTime);
    public event EventHandler? ExitRequested;
    public event Action<DayView>? DayRefreshed;
    public event Action? LanguageChanged;
    public void SetPauseState(bool paused) { manualPaused = paused; PauseButton.Content = Localizer.Get(paused ? "继续记录" : "暂停记录"); }
    public void StopRefresh() { stopped = true; refreshTimer.Stop(); logEndFollower.Dispose(); StatisticsView.Stop(); }
    private void ShowPage(bool statistics)
    {
        ReviewPane.IsVisible = !statistics; StatisticsView.IsVisible = statistics;
        ReviewPageButton.Background = statistics ? Brushes.Transparent : ReviewTheme.Surface;
        StatisticsPageButton.Background = statistics ? ReviewTheme.Surface : Brushes.Transparent;
        ReviewPageButton.Foreground = statistics ? ReviewTheme.Muted : ReviewTheme.Accent;
        StatisticsPageButton.Foreground = statistics ? ReviewTheme.Accent : ReviewTheme.Muted;
        StatisticsView.SetActive(statistics && IsVisible);
    }
    private async Task<StatisticsView> ReadStatisticsFallback(StatisticsRequest request)
    {
        var plan = StatisticsProjection.Plan(request, utcNow(), TimeZoneInfo.Local);
        var intervals = new List<StatisticsAllocation>();
        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
            intervals.AddRange((await readDay(date)).Intervals.Select(StatisticsAllocation.From));
        return StatisticsProjection.Create(plan, intervals);
    }
    private bool trayAvailable = true;
    public void SetTrayAvailable(bool available)
    {
        trayAvailable = available;
        ResidencyText.IsVisible = !available;
        ResidencyText.Text = Localizer.Get("系统托盘不可用，关闭按钮会保留窗口。记录和计时继续运行；请使用“退出”保存并关闭程序。");
    }
    public async Task StartRefreshAsync()
    {
        if (started || stopped) return;
        started = true; await RefreshAsync(); if (!stopped) refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        if (refreshing || stopped || deleting) return;
        refreshing = true;
        if (followToday && selectedDate != Today()) SelectDate(Today(), !resetRange && !TimelineView.IsFollowingPreset);
        var requestedDate = selectedDate; var requestedTimerVersion = timerVersion; var requestedRangeVersion = rangeVersion;
        try
        {
            var request = new ReviewRequest(requestedDate, rangeHours, !resetRange && !TimelineView.IsFollowingPreset ? TimelineView.VisibleStart : null);
            ReviewView snapshot;
            if (readReview is not null) snapshot = await readReview(request);
            else
            {
                var daily = await readDay(requestedDate);
                var plan = ReviewProjection.Plan(request, utcNow(), TimeZoneInfo.Local);
                var days = new List<DayView> { daily };
                foreach (var date in plan.Dates.Where(date => date != requestedDate)) days.Add(await readDay(date));
                snapshot = ReviewProjection.Create(plan, days);
            }
            if (stopped || deleting || requestedDate != selectedDate || requestedTimerVersion != timerVersion || requestedRangeVersion != rangeVersion) return;
            var incoming = snapshot.Daily;
            ReviewTheme.RegisterApplications(snapshot.Intervals.Where(x => x.Kind == IntervalKind.Usage).Select(x => x.AppKey)
                .Concat(incoming.Totals.Select(x => x.AppKey)));
            review = snapshot; view = incoming; TimelineView.SetReview(snapshot, resetRange); resetRange = false; StatusText.Text = Localizer.Translate(incoming.Status);
            ShowWarning(incoming.Warning); ShowTimer(incoming.Timer); ApplyRows();
            UpdateTotals(incoming);
            DayRefreshed?.Invoke(incoming);
        }
        catch (Exception error)
        {
            if (stopped || requestedDate != selectedDate || requestedRangeVersion != rangeVersion) return;
            StatusText.Text = Localizer.Get("回看更新失败"); ShowWarning(Localizer.Get("无法更新回看：{0}。已显示的记录可能不是最新。", Localizer.Translate(error.Message)));
        }
        finally
        {
            refreshing = false;
            if (!stopped && (requestedDate != selectedDate || requestedTimerVersion != timerVersion || requestedRangeVersion != rangeVersion)) _ = RefreshAsync();
        }
    }

    private void SelectDate(DateOnly day, bool preserveViewport = false)
    {
        var today = Today();
        if (day > today) day = today;
        followToday = day == today;
        if (selectedDate == day) { UpdateDateControls(); return; }
        selectedDate = day;
        if (!preserveViewport)
        {
            view = null; review = null; resetRange = true; TimelineView.Clear(); logEndFollower.Reset(); rows.Clear(); TotalsList.ItemsSource = null;
            LogEmptyText.Text = Localizer.Get("正在读取所选日期…"); LogEmptyText.IsVisible = true;
        }
        UpdateDateControls(); if (started) _ = RefreshAsync();
    }
    private void UpdateDateControls()
    {
        var today = Today();
        ViewDatePicker.DisplayDateEnd = today.ToDateTime(TimeOnly.MinValue);
        var date = selectedDate.ToDateTime(TimeOnly.MinValue);
        if (ViewDatePicker.SelectedDate != date) ViewDatePicker.SelectedDate = date;
        PreviousDayButton.IsEnabled = selectedDate.DayNumber > 0; NextDayButton.IsEnabled = selectedDate < today; TodayButton.IsEnabled = selectedDate != today;
        DateText.Text = $"{selectedDate:yyyy-MM-dd}"; Title = $"FocusOn · {selectedDate:yyyy-MM-dd}";
    }
    private void ApplyRows()
    {
        if (review is null || view is null || applyingRows) return;
        applyingRows = true;
        logEndFollower.BeginUpdate();
        try
        {
            var start = TimelineView.VisibleStart; var end = TimelineView.VisibleEnd;
            if (TimelineView.Range is { } range) { if (range.StartUtc > start) start = range.StartUtc; if (range.EndUtc < end) end = range.EndUtc; }
            var filtered = review.Intervals.Where(x => x.StartUtc < end && x.EndUtc > start && end > start);
            var incoming = filtered.Where(x => DisplayText.MatchesSearch(x, search)).OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
            var crossDate = TimelineView.VisibleStart.ToLocalTime().Date != TimelineView.VisibleEnd.ToLocalTime().Date;
            LogGrid.Columns[0].Width = new DataGridLength(crossDate ? 150 : 115);
            var ids = incoming.Select(x => x.Id).ToHashSet();
            for (var i = rows.Count - 1; i >= 0; i--) if (!ids.Contains(rows[i].Id)) rows.RemoveAt(i);
            var existing = rows.ToDictionary(x => x.Id);
            for (var i = 0; i < incoming.Length; i++)
            {
                var interval = incoming[i];
                if (existing.TryGetValue(interval.Id, out var row)) { row.Update(interval); if (rows[i] != row) rows.Move(rows.IndexOf(row), i); }
                else rows.Insert(i, new IntervalRow(interval));
                rows[i].SetShowDate(crossDate);
                var local = interval.StartUtc.ToLocalTime();
                var previous = i > 0 ? incoming[i - 1].StartUtc.ToLocalTime() : (DateTimeOffset?)null;
                rows[i].SetGroupLabel(showGroups && (previous is null || previous.Value.Date != local.Date || previous.Value.Hour != local.Hour)
                    ? (crossDate ? local.ToString("MM-dd ") : string.Empty) + Localizer.Get("{0:HH}:00 时段", local) : string.Empty);
            }
            LogEmptyText.Text = search.Length > 0 ? Localizer.Get("没有匹配的软件或窗口标题。") : Localizer.Get("所选范围没有可用记录。");
            LogEmptyText.IsVisible = rows.Count == 0; UpdateTimelineControls();
        }
        finally { logEndFollower.EndUpdate(); applyingRows = false; }
    }
    private void SelectInterval(long id)
    {
        var row = rows.FirstOrDefault(x => x.Id == id);
        if (row is null && search.Length > 0) { SearchBox.Text = string.Empty; row = rows.FirstOrDefault(x => x.Id == id); }
        if (row is null && TimelineView.Range is not null) { TimelineView.ClearRange(); row = rows.FirstOrDefault(x => x.Id == id); }
        if (row is null) return;
        logEndFollower.Pause();
        LogGrid.SelectedItem = row; LogGrid.ScrollIntoView(row, null);
    }
    private void SetInspector(bool show) { InspectorPane.IsVisible = show; DetailsButton.Content = Localizer.Get(show ? "详情 ⊟" : "详情 ⊞"); }
    private void UpdateTimelineControls()
    {
        ZoomInButton.IsEnabled = TimelineView.CanZoomIn; ZoomOutButton.IsEnabled = TimelineView.CanZoomOut;
        ResetDayButton.IsEnabled = view is not null; ClearRangeButton.IsEnabled = TimelineView.Range is not null;
        var mode = !TimelineView.IsFollowingPreset ? Localizer.Get("自定义视窗") : rangeHours is { } hours ? Localizer.Get(hours == 1 ? "最近 1 小时" : "最近 {0} 小时", hours)
            : selectedDate == Today() ? Localizer.Get("今天") : selectedDate.ToString("yyyy-MM-dd");
        mode += " · " + TimelineView.VisibleStart.ToLocalTime().ToString("MM-dd HH:mm") + " — " + TimelineView.VisibleEnd.ToLocalTime().ToString("MM-dd HH:mm");
        RangeText.Text = TimelineView.Range is { } range ? Localizer.Get("日志范围：{0} — {1} · 全天汇总不变 · 右键点击放大选区", DisplayText.Time(range.StartUtc), DisplayText.Time(range.EndUtc))
            : search.Length > 0 ? Localizer.Get("搜索：{0} · {1} 条匹配 · 全天汇总不变", search, rows.Count)
            : Localizer.Get("{0} · 左键点击或拖选，滚轮缩放，右键拖动平移，右键点击放大选区", mode);
    }
    private void ShowTimer(TitleTimerState? state)
    {
        timerState = state; TimerTitleBox.IsReadOnly = state is not null;
        if (state is not null) TimerTitleBox.Text = state.Title;
        StartTimerButton.IsVisible = state is null; PauseTimerButton.IsVisible = StopTimerButton.IsVisible = state is not null;
        PauseTimerButton.Content = Localizer.Get(state?.IsRunning == true ? "暂停" : "继续");
        TimerElapsedText.Text = state is null ? string.Empty : $"{(int)state.Duration.TotalHours:00}:{state.Duration.Minutes:00}:{state.Duration.Seconds:00}";
    }
    private async Task ChangeTimerAsync(TimerAction action)
    {
        if (changingTimer || deleting || stopped) return;
        if (action == TimerAction.Start && string.IsNullOrWhiteSpace(TimerTitleBox.Text)) { TimerErrorText.Text = Localizer.Get("请先填写计时标题"); TimerTitleBox.Focus(); return; }
        changingTimer = true; StartTimerButton.IsEnabled = PauseTimerButton.IsEnabled = StopTimerButton.IsEnabled = false; TimerErrorText.Text = string.Empty;
        try
        {
            await changeTimer(action, TimerTitleBox.Text); timerVersion++;
            if (action == TimerAction.Stop) TimerTitleBox.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception error) { TimerErrorText.Text = Localizer.Get("计时操作失败：{0}", Localizer.Translate(error.Message)); }
        finally { changingTimer = false; StartTimerButton.IsEnabled = PauseTimerButton.IsEnabled = StopTimerButton.IsEnabled = true; }
    }
    private async Task DeleteDayAsync()
    {
        if (deleting || changingTimer || stopped) return;
        var day = selectedDate; var (start, end) = DailyProjection.Bounds(day, TimeZoneInfo.Local);
        if (!await MessageDialog.ShowAsync(this, Localizer.Get("删除 {0} 的所有记录？\n本地范围：{1} 至 {2}（不含结束）。\n跨日记录保留其他日期部分。删除无法撤销，删除今天后继续记录未来活动。", day.ToString("yyyy-MM-dd"), DisplayText.Time(start, true), DisplayText.Time(end, true)), confirm: true)) return;
        deleting = true; DeleteDayButton.IsEnabled = false;
        try { await deleteDay(day); TimelineView.ClearRange(); LogGrid.SelectedItem = null; }
        catch (Exception error) { await MessageDialog.ShowAsync(this, Localizer.Get("删除操作出现异常：{0}", Localizer.Translate(error.Message))); }
        finally { deleting = false; DeleteDayButton.IsEnabled = !stopped; await RefreshAsync(); }
    }
    public void ShowWarning(string? message) { WarningText.Text = Localizer.Translate(message); WarningPanel.IsVisible = !string.IsNullOrWhiteSpace(message); }

    private void UpdateTotals(DayView incoming)
    {
        TotalsList.ItemsSource = incoming.Totals.OrderByDescending(x => x.Duration).Select(x => new { AppName = x.AppKey == "unknown" ? Localizer.Get("未知软件") : x.AppName, Duration = DisplayText.Duration(x.Duration), Color = ReviewTheme.ApplicationBrush(x.AppKey) }).ToArray();
        TotalsEmptyText.Text = Localizer.Get("{0} 尚无软件使用片段。", incoming.Date.ToString("yyyy-MM-dd")); TotalsEmptyText.IsVisible = incoming.Totals.Count == 0;
    }

    private void ApplyLanguage()
    {
        SetTrayAvailable(trayAvailable);
        applyingLanguage = true;
        try
        {
            LanguagePicker.SelectedIndex = Localizer.Language == "en" ? 1 : 0;
            foreach (var pair in new (Button Control, string Text)[] {
                (TodayButton,"今天"), (ExitButton,"退出"), (StartTimerButton,"开始计时"), (StopTimerButton,"停止"),
                (DurationButton,"时长"), (GroupsButton,showGroups ? "时段 ⊟" : "时段 ⊞"),
                (ResetDayButton,"重置"), (ClearRangeButton,"清除范围"), (DeleteDayButton,"删除所选日期") }) pair.Control.Content = Localizer.Get(pair.Text);
            ReviewPageButton.Content = Localizer.Get("回看"); StatisticsPageButton.Content = Localizer.Get("统计"); StatisticsView.ApplyLanguage();
            foreach (var pair in new (Control Control, string Text)[] { (PreviousDayButton,"前一天"), (NextDayButton,"后一天"),
                (ViewDatePicker,"回看日期"), (TimerTitleBox,"计时标题"), (LanguagePicker,"界面语言"), (TotalsScrollViewer,"全天软件汇总"), (RangePicker,"时间范围"),
                (ZoomInButton,"放大时间线"), (ZoomOutButton,"缩小时间线"), (SearchBox,"搜索时间范围内的软件或窗口标题") })
                AutomationProperties.SetName(pair.Control, Localizer.Get(pair.Text));
            TimerTitleBox.Watermark = Localizer.Get("输入标题，开始独立计时"); SearchBox.Watermark = Localizer.Get("搜索时间范围内的软件或窗口标题"); TodayRangeItem.Content = Localizer.Get("今天");
            TotalsHeading.Text = Localizer.Get("全天软件汇总"); DetailsHeading.Text = Localizer.Get("记录详情"); DetailsEmptyText.Text = Localizer.Get("选择一条记录查看详情。");
            var headers = new[] { "开始时间", "软件 / 状态", "窗口标题 / 说明", "时长" };
            for (var i = 0; i < headers.Length; i++) LogGrid.Columns[i].Header = Localizer.Get(headers[i]);
            SetPauseState(manualPaused); SetInspector(InspectorPane.IsVisible); ShowTimer(timerState);
            TimerErrorText.Text = Localizer.Translate(TimerErrorText.Text);
            TimelineView.RefreshLanguage();
            foreach (var row in rows) row.RefreshAppearance();
            if (view is not null) { StatusText.Text = Localizer.Translate(view.Status); ShowWarning(view.Warning); UpdateTotals(view); ApplyRows(); }
            else { StatusText.Text = Localizer.Get("正在启动…"); LogEmptyText.Text = Localizer.Get("正在读取记录…"); }
            UpdateTimelineControls();
        }
        finally { applyingLanguage = false; }
    }
}

public sealed class IntervalRow(RecordedInterval interval) : INotifyPropertyChanged
{
    private RecordedInterval value = interval;
    public long Id => value.Id;
    public string Application => DisplayText.AppName(value) + (value.IsCurrent ? Localizer.Get(" · 进行中") : string.Empty);
    private bool showDate;
    public string StartTime => (showDate ? value.StartUtc.ToLocalTime().ToString("MM-dd ") : string.Empty) + DisplayText.Time(value.StartUtc);
    public void SetShowDate(bool show) { if (showDate == show) return; showDate = show; PropertyChanged?.Invoke(this, new(nameof(StartTime))); }
    public string Title => DisplayText.Title(value);
    public string Duration => DisplayText.Duration(value.Duration);
    public string PreciseDuration => DisplayText.Duration(value.Duration, true);
    public string StartFull => DisplayText.Time(value.StartUtc, true);
    public string EndFull => DisplayText.Time(value.EndUtc, true);
    public string CurrentNote => value.IsCurrent ? Localizer.Get("进行中；尾段可能尚未保存。") : string.Empty;
    public string Kind => DisplayText.Kind(value.Kind);
    public IBrush Color => value.Kind == IntervalKind.TaskTimer ? ReviewTheme.Accent : value.Kind == IntervalKind.Usage ? ReviewTheme.ApplicationBrush(value.AppKey) : ReviewTheme.Muted;
    public IBrush BadgeBackground => ReviewTheme.HighContrast ? ReviewTheme.Background : Color is SolidColorBrush color ? new SolidColorBrush(color.Color, 0.15) : ReviewTheme.Surface;
    public string GroupLabel { get; private set; } = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshAppearance() => PropertyChanged?.Invoke(this, new(null));
    public void Update(RecordedInterval incoming) { if (value == incoming) return; value = incoming; PropertyChanged?.Invoke(this, new(null)); }
    public void SetGroupLabel(string label) { if (label == GroupLabel) return; GroupLabel = label; PropertyChanged?.Invoke(this, new(nameof(GroupLabel))); }
}
