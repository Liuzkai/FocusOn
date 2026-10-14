using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace FocusOn;

public partial class StatisticsPage : UserControl
{
    private Func<StatisticsRequest, Task<StatisticsView>>? read;
    private Func<DateTimeOffset> now = () => DateTimeOffset.UtcNow;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private StatisticsRequest? request;
    private long revision;
    private bool reading, refreshAgain, active, stopped, changingLanguage;
    private string? error, validationError;
    private bool? wideLayout;
    public StatisticsView? CurrentView { get; private set; }

    public StatisticsPage()
    {
        InitializeComponent();
        ApplyButton.Click += async (_, _) => await ApplyFiltersAsync();
        ModePicker.SelectionChanged += (_, _) =>
        {
            if (changingLanguage) return;
            if (ModePicker.SelectedIndex == 1) StartDatePicker.SelectedDate = EndDatePicker.SelectedDate ?? now().LocalDateTime.Date;
            validationError = null; UpdateMode(); UpdateError();
        };
        SevenDaysButton.Click += async (_, _) => await PresetAsync(7);
        ThirtyDaysButton.Click += async (_, _) => await PresetAsync(30);
        WholeDayButton.Click += async (_, _) => { FromBox.Text = "00:00"; ToBox.Text = "24:00"; await ApplyFiltersAsync(); };
        timer.Tick += async (_, _) => await RefreshAsync();
        SizeChanged += (_, _) =>
        {
            var wide = Bounds.Width >= 900;
            if (wideLayout == wide) return;
            wideLayout = wide;
            ChartsLayout.ColumnDefinitions = new ColumnDefinitions(wide ? "*,*" : "*");
            ChartsLayout.RowDefinitions = new RowDefinitions(wide ? "Auto" : "Auto,Auto");
            Grid.SetColumn(TrendCard, wide ? 1 : 0); Grid.SetRow(TrendCard, wide ? 0 : 1);
        };
        ApplyLanguage();
    }
    public void Configure(Func<StatisticsRequest, Task<StatisticsView>> reader, Func<DateTimeOffset> clock)
    {
        read = reader; now = clock;
        var today = DateOnly.FromDateTime(now().LocalDateTime);
        StartDatePicker.SelectedDate = today.AddDays(-6).ToDateTime(TimeOnly.MinValue);
        EndDatePicker.SelectedDate = today.ToDateTime(TimeOnly.MinValue);
        UpdateLimits();
    }
    private void UpdateLimits()
    {
        var today = now().LocalDateTime.Date;
        StartDatePicker.DisplayDateEnd = EndDatePicker.DisplayDateEnd = today;
    }
    public void SetActive(bool value)
    {
        active = value;
        SetValue(StatisticsMotion.ActiveProperty, value && !stopped);
        if (!value) { timer.Stop(); ShareChart.HideHint(); BarsChart.HideHint(); TrendChart.HideHint(); return; }
        if (stopped) return;
        timer.Start(); _ = request is null ? ApplyFiltersAsync() : RefreshAsync();
    }
    public void SetMotionEnabled(bool value)
    {
        SetValue(StatisticsMotion.EnabledProperty, value && !ReviewTheme.HighContrast);
        ShareChart.InvalidateVisual(); BarsChart.InvalidateVisual(); TrendChart.InvalidateVisual();
        foreach (var card in this.GetLogicalDescendants().OfType<StatisticsCard>()) card.UpdateAppearance();
    }
    public void Stop() { stopped = true; SetActive(false); revision++; }
    private async Task PresetAsync(int days)
    {
        ModePicker.SelectedIndex = 0;
        var today = now().LocalDateTime.Date;
        StartDatePicker.SelectedDate = today.AddDays(1 - days); EndDatePicker.SelectedDate = today;
        await ApplyFiltersAsync();
    }
    private void UpdateMode()
    {
        var period = ModePicker.SelectedIndex == 1;
        EndDatePanel.IsVisible = SevenDaysButton.IsVisible = ThirtyDaysButton.IsVisible = !period;
        FromPanel.IsVisible = ToPanel.IsVisible = WholeDayButton.IsVisible = period;
        StartDateLabel.Text = Localizer.Get(period ? "日期" : "开始日期");
        FilterHelp.Text = Localizer.Get(period ? "使用本地时间；结束可填写24:00。应用筛选后更新图表。" : "包含开始和结束日期，最多366天；空日期按零使用显示。");
    }
    public async Task ApplyFiltersAsync()
    {
        if (stopped) return;
        try
        {
            if (StartDatePicker.SelectedDate is not { } start) throw new ArgumentException("请选择日期。");
            var first = DateOnly.FromDateTime(start);
            StatisticsRequest next;
            if (ModePicker.SelectedIndex == 1) next = new(first, first, ParseTime(FromBox.Text, false), ParseTime(ToBox.Text, true));
            else
            {
                if (EndDatePicker.SelectedDate is not { } end) throw new ArgumentException("请选择日期。");
                next = new(first, DateOnly.FromDateTime(end));
            }
            StatisticsProjection.Plan(next, now(), TimeZoneInfo.Local);
            request = next; revision++; error = validationError = null; ErrorText.IsVisible = false;
            await RefreshAsync();
        }
        catch (Exception failure) { validationError = failure.Message; UpdateError(); }
    }
    private static TimeSpan ParseTime(string? text, bool end)
    {
        if (end && text == "24:00") return TimeSpan.FromDays(1);
        if (TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return value.ToTimeSpan();
        throw new ArgumentException("请输入HH:mm格式的时间；结束可为24:00。");
    }
    public async Task RefreshAsync()
    {
        if (stopped || request is null || read is null) return;
        if (reading) { refreshAgain = true; return; }
        reading = true; var version = revision; var scope = request;
        LoadStatus.Text = Localizer.Get("正在统计…");
        try
        {
            var value = await read(scope);
            if (stopped || version != revision) return;
            CurrentView = value; error = null;
            ReviewTheme.RegisterApplications(value.Apps.Select(x => x.AppKey));
            Present(value);
        }
        catch (Exception failure)
        {
            if (!stopped && version == revision) { error = "无法更新统计：" + failure.Message; UpdateError(); LoadStatus.Text = Localizer.Get("统计更新失败"); }
        }
        finally
        {
            reading = false;
            if (!stopped && active && (version != revision || refreshAgain)) { refreshAgain = false; _ = RefreshAsync(); }
        }
    }
    private void Present(StatisticsView value)
    {
        var applied = value.Plan.Request;
        var scope = applied.IsPeriod ? $"{applied.StartDate:yyyy-MM-dd} · {FormatTime(applied.StartTime!.Value)} — {FormatTime(applied.EndTime!.Value)}"
            : $"{applied.StartDate:yyyy-MM-dd} — {applied.EndDate:yyyy-MM-dd}";
        ScopeText.Text = Localizer.Get("已应用范围：{0}", scope);
        TotalText.Text = StatisticsChart.Short(value.Duration); AppsText.Text = value.Apps.Count.ToString(CultureInfo.CurrentCulture);
        var elapsed = (value.Plan.EndUtc - value.Plan.StartUtc).TotalHours;
        var divisor = applied.IsPeriod ? elapsed > 0 ? elapsed : 1 : applied.EndDate.DayNumber - applied.StartDate.DayNumber + 1;
        AverageText.Text = StatisticsChart.Short(TimeSpan.FromTicks((long)(value.Duration.Ticks / divisor)));
        AverageLabel.Text = Localizer.Get(applied.IsPeriod ? "每小时平均" : "平均每天");
        BarsHeading.Text = Localizer.Get(applied.IsPeriod ? "时段使用时长" : "每日使用时长");
        ShareLegend.ItemsSource = StatisticsChart.Shares(value); TrendLegend.ItemsSource = StatisticsChart.Rows(value).Take(3).ToArray();
        AppList.ItemsSource = StatisticsChart.Rows(value);
        ShareChart.SetData(value); BarsChart.SetData(value); TrendChart.SetData(value);
        EmptyText.IsVisible = value.Apps.Count == 0;
        UpdateError();
        LoadStatus.Text = Localizer.Get("更新于 {0:HH:mm:ss}", value.Plan.SnapshotUtc.ToLocalTime());
        Footnote.Text = Localizer.Get("仅统计前台软件使用；暂停、未记录和独立计时不计入。当前进行中的尾段可能尚未保存。");
        UpdateLimits();
    }
    private static string FormatTime(TimeSpan value) => value == TimeSpan.FromDays(1) ? "24:00" : value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    private void UpdateError()
    {
        var message = validationError ?? error ?? CurrentView?.Warning;
        ErrorText.Text = Localizer.Translate(message); ErrorText.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
    public void ApplyLanguage()
    {
        foreach (var (control, text) in new (TextBlock, string)[] { (Heading,"统计"), (Subtitle,"查看多日或一天内指定时段的前台软件使用情况。"),
            (ModeLabel,"统计范围"), (EndDateLabel,"结束日期"), (FromLabel,"开始时间"), (ToLabel,"结束时间"), (TotalLabel,"前台使用时长"),
            (AppsLabel,"使用软件数"), (ShareHeading,"软件使用占比"), (TrendHeading,"软件使用趋势"), (TrendNote,"时长排名前三的软件"),
            (ListHeading,"软件明细"), (DurationHeader,"时长 · 占比"), (EmptyText,"所选范围没有软件使用记录。"),
            (ShareNote,"占比以所选范围的软件使用时长为分母；前五名外的软件合并为“其他软件”。") }) control.Text = Localizer.Get(text);
        foreach (var (control, text) in new (Button, string)[] { (ApplyButton,"应用筛选"), (SevenDaysButton,"近7天"), (ThirtyDaysButton,"近30天"), (WholeDayButton,"全天") }) control.Content = Localizer.Get(text);
        foreach (var (control, text) in new (Control, string)[] { (ModePicker,"统计范围"), (StartDatePicker,"开始日期"), (EndDatePicker,"结束日期"), (FromBox,"开始时间"), (ToBox,"结束时间") }) AutomationProperties.SetName(control, Localizer.Get(text));
        var multi = Localizer.Get("多日"); var period = Localizer.Get("单日时段");
        if (!Equals(MultiDayItem.Content, multi) || !Equals(PeriodItem.Content, period))
        {
            changingLanguage = true;
            try
            {
                var selected = ModePicker.SelectedIndex;
                MultiDayItem.Content = multi; PeriodItem.Content = period;
                ModePicker.SelectedIndex = -1; ModePicker.SelectedIndex = selected;
            }
            finally { changingLanguage = false; }
        }
        UpdateMode();
        if (CurrentView is { } value) Present(value);
        UpdateError();
    }
}
