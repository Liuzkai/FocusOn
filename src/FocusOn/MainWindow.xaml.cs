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
    private readonly ObservableCollection<IntervalRow> _rows = [];
    private readonly DispatcherTimer _refreshTimer;
    private bool _refreshing;
    private bool _started;
    private bool _stopped;
    private DateOnly? _shownDay;

    public MainWindow(Func<DateOnly, Task<DayView>> readDay)
    {
        _readDay = readDay ?? throw new ArgumentNullException(nameof(readDay));
        InitializeComponent();
        LogGrid.ItemsSource = _rows;
        DateText.Text = DateTime.Today.ToString("yyyy年M月d日 dddd", CultureInfo.CurrentCulture);
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started || _stopped) return;
        _started = true;
        await RefreshAsync();
        if (!_stopped) _refreshTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshing || _stopped) return;
        _refreshing = true;
        try
        {
            var view = await _readDay(DateOnly.FromDateTime(DateTime.Today));
            if (_stopped) return;
            ShowDay(view);
        }
        catch (Exception ex)
        {
            if (_stopped) return;
            StatusText.Text = "回看更新失败";
            ShowWarning($"无法更新回看：{ex.Message}。已显示的记录可能不是最新。请检查存储是否可用。");
            if (_rows.Count == 0) LogEmptyText.Text = "目前无法读取记录。";
            if (TotalsList.Items.Count == 0) TotalsEmptyText.Text = "目前无法读取软件汇总。";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ShowDay(DayView view)
    {
        var newDay = _shownDay != view.Date;
        if (newDay)
        {
            _shownDay = view.Date;
            _rows.Clear();
        }
        DateText.Text = view.Date.ToDateTime(TimeOnly.MinValue).ToString("yyyy年M月d日 dddd", CultureInfo.CurrentCulture);
        StatusText.Text = view.Status;
        ShowWarning(view.Warning);

        var intervals = view.Intervals.OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
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
        }

        TotalsList.ItemsSource = view.Totals.OrderByDescending(x => x.Duration).ThenBy(x => x.AppName)
            .Select(x => new { x.AppName, Duration = DisplayText.Duration(x.Duration) }).ToArray();
        TotalsEmptyText.Text = "今天尚无软件使用片段。";
        TotalsEmptyText.Visibility = view.Totals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LogEmptyText.Text = "今天没有可用记录。";
        LogEmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (newDay && _rows.Count > 0)
        {
            LogGrid.SelectedItem = _rows[^1];
            LogGrid.ScrollIntoView(LogGrid.SelectedItem);
        }
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
    }
}

public sealed class IntervalRow(RecordedInterval interval) : INotifyPropertyChanged
{
    private RecordedInterval _interval = interval;
    public long Id => _interval.Id;
    public string TimeRange => $"{DisplayText.Time(_interval.StartUtc)} – {DisplayText.Time(_interval.EndUtc)}";
    public string Application => DisplayText.AppName(_interval) + (_interval.IsCurrent ? " · 进行中" : string.Empty);
    public string Kind => DisplayText.Kind(_interval.Kind);
    public string Title => DisplayText.Title(_interval);
    public string Duration => DisplayText.Duration(_interval.Duration);
    public string PreciseDuration => DisplayText.Duration(_interval.Duration, precise: true);
    public string StartFull => DisplayText.Time(_interval.StartUtc, full: true);
    public string EndFull => DisplayText.Time(_interval.EndUtc, full: true);
    public string CurrentNote => _interval.IsCurrent ? "进行中；尾段可能尚未保存。" : string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(RecordedInterval value)
    {
        if (_interval == value) return;
        _interval = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
