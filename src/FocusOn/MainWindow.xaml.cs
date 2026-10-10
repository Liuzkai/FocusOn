using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FocusOn;

public partial class MainWindow : Window
{
    private readonly Func<DateOnly, Task<DayView>> _readDay;
    private readonly Func<DateOnly, Task> _deleteDay;
    private readonly ObservableCollection<IntervalRow> _rows = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _refreshing;
    private bool _started;
    private bool _stopped;
    private bool _deleting;
    private DateOnly? _shownDay;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private bool _followToday = true;
    private DayView? _dayView;
    private string _searchText = string.Empty;
    private bool _showGroups = true;

    public MainWindow(Func<DateOnly, Task<DayView>> readDay, Func<DateOnly, Task> deleteDay)
    {
        _readDay = readDay ?? throw new ArgumentNullException(nameof(readDay));
        _deleteDay = deleteDay ?? throw new ArgumentNullException(nameof(deleteDay));
        InitializeComponent();
        ReviewTheme.Apply(Resources);
        SystemParameters.StaticPropertyChanged += SystemThemeChanged;
        Closed += (_, _) => { SystemParameters.StaticPropertyChanged -= SystemThemeChanged; StopRefresh(); };
        LogGrid.ItemsSource = _rows;
        TimelineView.IntervalSelected += Timeline_IntervalSelected;
        TimelineView.RangeChanged += Timeline_RangeChanged;
        TimelineView.ViewChanged += UpdateTimelineControls;
        UpdateDateControls();
        UpdateTimelineControls();
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += MainWindow_Loaded;
    }

    public void StopRefresh()
    {
        _stopped = true;
        _refreshTimer.Stop();
    }

    public event EventHandler? PauseRequested;

    public void SetPauseState(bool paused) => PauseButton.Content = paused ? "继续" : "暂停";

    private void PauseButton_Click(object sender, RoutedEventArgs e) => PauseRequested?.Invoke(this, EventArgs.Empty);

    public async Task StartRefreshAsync()
    {
        if (_started || _stopped) return;
        _started = true;
        await RefreshAsync();
        if (!_stopped) _refreshTimer.Start();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) => await StartRefreshAsync();

    private async void RefreshTimer_Tick(object? sender, EventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshing || _stopped || _deleting) return;
        _refreshing = true;
        var requestedDate = _selectedDate;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (_followToday && _selectedDate != today) SelectDate(today);
            UpdateDateControls();
            requestedDate = _selectedDate;
            var view = await _readDay(requestedDate);
            if (_stopped || _deleting || requestedDate != _selectedDate) return;
            ShowDay(view);
        }
        catch (Exception ex)
        {
            if (_stopped || _deleting || requestedDate != _selectedDate) return;
            StatusText.Text = "回看更新失败";
            ShowWarning($"无法更新回看：{ex.Message}。已显示的记录可能不是最新。请检查存储是否可用。");
            if (_rows.Count == 0) LogEmptyText.Text = "目前无法读取记录。";
            if (TotalsList.Items.Count == 0) TotalsEmptyText.Text = "目前无法读取软件汇总。";
        }
        finally
        {
            _refreshing = false;
            if (!_stopped && requestedDate != _selectedDate) _ = RefreshAsync();
        }
    }

    private void ViewDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewDatePicker.SelectedDate is DateTime date) SelectDate(DateOnly.FromDateTime(date));
    }

    private void PreviousDay_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDate.DayNumber > 0) SelectDate(_selectedDate.AddDays(-1));
    }

    private void NextDay_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDate < DateOnly.FromDateTime(DateTime.Today)) SelectDate(_selectedDate.AddDays(1));
    }

    private void Today_Click(object sender, RoutedEventArgs e) => SelectDate(DateOnly.FromDateTime(DateTime.Today));

    private async void DeleteDay_Click(object sender, RoutedEventArgs e)
    {
        if (_deleting || _stopped) return;
        var day = _selectedDate;
        var (start, end) = DailyProjection.Bounds(day, TimeZoneInfo.Local);
        var answer = MessageBox.Show(this,
            $"删除 {day:yyyy-MM-dd} 的所有记录？\n本地范围：{DisplayText.Time(start, full: true)} 至 {DisplayText.Time(end, full: true)}（不含结束时刻）。\n跨日记录保留其他日期部分。删除无法撤销，删除今天后继续记录未来活动。",
            "确认删除日期", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        _deleting = true;
        DeleteDayButton.IsEnabled = false;
        try
        {
            await _deleteDay(day);
            TimelineView.ClearRange();
            LogGrid.SelectedItem = null;
        }
        catch (Exception error)
        {
            MessageBox.Show(this, $"删除操作出现异常：{error.Message}", "FocusOn", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _deleting = false;
            DeleteDayButton.IsEnabled = !_stopped;
            await RefreshAsync();
        }
    }

    private void SelectDate(DateOnly day)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (day > today) day = today;
        _followToday = day == today;
        if (_selectedDate == day)
        {
            UpdateDateControls();
            return;
        }
        _selectedDate = day;
        UpdateDateControls();
        _shownDay = null;
        _dayView = null;
        TimelineView.Clear();
        _rows.Clear();
        TotalsList.ItemsSource = null;
        LogEmptyText.Text = "正在读取所选日期…";
        TotalsEmptyText.Text = "正在读取软件汇总…";
        LogEmptyText.Visibility = Visibility.Visible;
        TotalsEmptyText.Visibility = Visibility.Visible;
        if (_started && !_stopped) _ = RefreshAsync();
    }

    private void UpdateDateControls()
    {
        var date = _selectedDate.ToDateTime(TimeOnly.MinValue);
        ViewDatePicker.DisplayDateEnd = DateTime.Today;
        if (ViewDatePicker.SelectedDate != date) ViewDatePicker.SelectedDate = date;
        PreviousDayButton.IsEnabled = _selectedDate.DayNumber > 0;
        NextDayButton.IsEnabled = _selectedDate < DateOnly.FromDateTime(DateTime.Today);
        TodayButton.IsEnabled = _selectedDate != DateOnly.FromDateTime(DateTime.Today);
        DateText.Text = date.ToString("yyyy年M月d日 dddd", CultureInfo.CurrentCulture);
        Title = $"FocusOn · {date:yyyy-MM-dd}";
    }

    private void ShowDay(DayView view)
    {
        var newDay = _shownDay != view.Date;
        if (newDay)
        {
            _shownDay = view.Date;
            _rows.Clear();
        }
        _dayView = view;
        TimelineView.SetDay(view);
        TimelineView.SetSearch(_searchText);
        UpdateDateControls();
        StatusText.Text = view.Status;
        ShowWarning(view.Warning);

        ApplyRows();
        TotalsList.ItemsSource = view.Totals.OrderByDescending(x => x.Duration).ThenBy(x => x.AppName)
            .Select(x => new { x.AppName, Duration = DisplayText.Duration(x.Duration), Color = TimelineControl.ApplicationBrush(x.AppKey) }).ToArray();
        TotalsEmptyText.Text = $"{view.Date:yyyy-MM-dd} 尚无软件使用片段。";
        TotalsEmptyText.Visibility = view.Totals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (newDay && _rows.Count > 0)
        {
            LogGrid.ScrollIntoView(_rows[^1]);
        }
    }

    private void ApplyRows()
    {
        if (_dayView is null) return;
        var filtered = TimelineView.Range is { } range ? _dayView.FilterRange(range.StartUtc, range.EndUtc) : _dayView.Intervals;
        var intervals = filtered.Where(x => DisplayText.MatchesSearch(x, _searchText)).OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
        var incomingIds = intervals.Select(x => x.Id).ToHashSet();
        for (var i = _rows.Count - 1; i >= 0; i--)
            if (!incomingIds.Contains(_rows[i].Id)) _rows.RemoveAt(i);

        var existing = _rows.ToDictionary(x => x.Id);
        for (var i = 0; i < intervals.Length; i++)
        {
            var interval = intervals[i];
            if (existing.TryGetValue(interval.Id, out var row))
            {
                row.Update(interval);
                if (_rows[i] != row) _rows.Move(_rows.IndexOf(row), i);
            }
            else
            {
                _rows.Insert(i, new IntervalRow(interval));
            }
            var local = interval.StartUtc.ToLocalTime();
            var previous = i > 0 ? intervals[i - 1].StartUtc.ToLocalTime() : (DateTimeOffset?)null;
            var startsGroup = previous is null || previous.Value.Date != local.Date || previous.Value.Hour != local.Hour;
            _rows[i].SetGroupLabel(_showGroups && startsGroup ? $"{local:HH}:00 时段" : string.Empty);
        }

        LogEmptyText.Text = _searchText.Length > 0 ? "没有匹配的软件或窗口标题。"
            : TimelineView.Range is not null ? "所选范围没有可用记录。" : $"{_dayView.Date:yyyy-MM-dd} 没有可用记录。";
        LogEmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateTimelineControls();
    }

    private void Timeline_IntervalSelected(long id)
    {
        var row = _rows.FirstOrDefault(x => x.Id == id);
        if (row is null && _searchText.Length > 0)
        {
            SearchBox.Text = string.Empty;
            row = _rows.FirstOrDefault(x => x.Id == id);
        }
        if (row is null && TimelineView.Range is not null)
        {
            TimelineView.ClearRange();
            row = _rows.FirstOrDefault(x => x.Id == id);
        }
        if (row is null) return;
        LogGrid.SelectedItem = row;
        LogGrid.ScrollIntoView(row);
    }

    private void Timeline_RangeChanged((DateTimeOffset StartUtc, DateTimeOffset EndUtc)? range) => ApplyRows();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => TimelineView.ZoomIn();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => TimelineView.ZoomOut();
    private void ResetDay_Click(object sender, RoutedEventArgs e) => TimelineView.ResetDay();
    private void ClearRange_Click(object sender, RoutedEventArgs e) => TimelineView.ClearRange();

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchBox.Text.Trim();
        if (_dayView is null) return;
        TimelineView.SetSearch(_searchText);
        ApplyRows();
    }

    private void Duration_Click(object sender, RoutedEventArgs e)
    {
        var show = TimelinePanel.Visibility != Visibility.Visible;
        TimelinePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DurationButton.SetResourceReference(BackgroundProperty, show ? "ReviewHover" : "ReviewBackground");
        DurationButton.SetResourceReference(ForegroundProperty, show ? "ReviewText" : "ReviewMuted");
    }

    private void Groups_Click(object sender, RoutedEventArgs e)
    {
        _showGroups = !_showGroups;
        GroupsButton.Content = _showGroups ? "时段  ⊟" : "时段  ⊞";
        ApplyRows();
    }

    private void Details_Click(object sender, RoutedEventArgs e) => SetInspector(InspectorPane.Visibility != Visibility.Visible);

    private void SetInspector(bool show)
    {
        InspectorPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        InspectorColumn.Width = new GridLength(show ? 280 : 0);
        DetailsButton.Content = show ? "详情  ⊟" : "详情  ⊞";
    }

    private void SystemThemeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.HighContrast)) return;
        Dispatcher.InvokeAsync(() =>
        {
            ReviewTheme.Apply(Resources);
            if (_dayView is not null) ShowDay(_dayView);
            foreach (var row in _rows) row.RefreshAppearance();
            TimelineView.InvalidateVisual();
        });
    }

    private void UpdateTimelineControls()
    {
        ZoomInButton.IsEnabled = TimelineView.CanZoomIn;
        ZoomOutButton.IsEnabled = TimelineView.CanZoomOut;
        ResetDayButton.IsEnabled = _dayView is not null;
        ClearRangeButton.IsEnabled = TimelineView.Range is not null;
        RangeText.Text = TimelineView.Range is { } range
            ? $"日志范围：{DisplayText.Time(range.StartUtc, true)} — {DisplayText.Time(range.EndUtc, true)}（仅筛选日志）"
            : _searchText.Length > 0 ? $"搜索：{_searchText} · {_rows.Count} 条匹配 · 全天汇总不变"
            : "全天记录 · 拖选筛选，滚轮缩放，右键取消范围";
        ViewRangeText.Text = _dayView is null ? string.Empty : TimelineView.CanZoomOut
            ? $"显示：{DisplayText.Time(TimelineView.VisibleStart)} — {DisplayText.Time(TimelineView.VisibleEnd)}"
            : "显示：全天";
    }

    private void ShowWarning(string? warning)
    {
        WarningText.Text = warning ?? string.Empty;
        WarningPanel.Visibility = string.IsNullOrWhiteSpace(warning) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LogGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = LogGrid.SelectedItem as IntervalRow;
        DetailsPanel.DataContext = row;
        DetailsPanel.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
        DetailsEmptyText.Visibility = row is null ? Visibility.Visible : Visibility.Collapsed;
        if (row is null) TimelineView.ClearSelection();
        else
        {
            TimelineView.SelectInterval(row.Id);
            SetInspector(true);
        }
    }
}

public sealed class IntervalRow(RecordedInterval interval) : INotifyPropertyChanged
{
    private RecordedInterval _interval = interval;
    private string _groupLabel = string.Empty;
    public long Id => _interval.Id;
    public string TimeRange => $"{DisplayText.Time(_interval.StartUtc)} – {DisplayText.Time(_interval.EndUtc)}";
    public string Application => DisplayText.AppName(_interval) + (_interval.IsCurrent ? " · 进行中" : string.Empty);
    public string BadgeLabel => DisplayText.AppName(_interval) + (_interval.IsCurrent ? " ·" : string.Empty);
    public string StartTime => DisplayText.Time(_interval.StartUtc);
    public string GroupLabel => _groupLabel;
    public System.Windows.Media.Brush Color => _interval.Kind == IntervalKind.Usage
        ? TimelineControl.ApplicationBrush(_interval.AppKey) : ReviewTheme.Muted;
    public System.Windows.Media.Brush BadgeBackground => ReviewTheme.BadgeBackground(Color);
    public string Kind => DisplayText.Kind(_interval.Kind);
    public string Title => DisplayText.Title(_interval);
    public string Duration => DisplayText.Duration(_interval.Duration);
    public string PreciseDuration => DisplayText.Duration(_interval.Duration, precise: true);
    public string StartFull => DisplayText.Time(_interval.StartUtc, full: true);
    public string EndFull => DisplayText.Time(_interval.EndUtc, full: true);
    public string CurrentNote => _interval.IsCurrent ? "进行中；尾段可能尚未保存。" : string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetGroupLabel(string label)
    {
        if (_groupLabel == label) return;
        _groupLabel = label;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupLabel)));
    }

    public void RefreshAppearance() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

    public void Update(RecordedInterval value)
    {
        if (_interval == value) return;
        _interval = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
