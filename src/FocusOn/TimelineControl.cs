using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FocusOn;

public sealed class TimelineControl : FrameworkElement
{
    private const double LabelWidth = 54;
    private const double BarTop = 7;
    private const double BarHeight = 8;
    private const double LaneSpacing = 14;
    private const double PlotHeight = 50;
    private static readonly Brush[] AppPalette =
    [
        FrozenBrush(0x82, 0xA6, 0xE3), FrozenBrush(0x68, 0xC0, 0x91),
        FrozenBrush(0xAC, 0x88, 0xC4), FrozenBrush(0xD4, 0x9A, 0x48),
        FrozenBrush(0xD8, 0x8B, 0xAB), FrozenBrush(0x96, 0xBB, 0x77),
        FrozenBrush(0x95, 0xA0, 0xDA), FrozenBrush(0xC4, 0xA2, 0x80)
    ];
    private readonly ToolTip tip;
    private DayView? day;
    private RecordedInterval[] intervals = [];
    private DateTimeOffset visibleStart;
    private DateTimeOffset visibleEnd;
    private long? selectedId;
    private DateTimeOffset? dragStart;
    private double dragStartX;
    private DateTimeOffset? dragEnd;
    private RecordedInterval? keyboardAnchor;
    private long? hoveredId;
    private string searchText = string.Empty;
    private double? panStartX;
    private DateTimeOffset panStartUtc;
    private DateTimeOffset panEndUtc;

    public TimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        Height = 74;
        tip = new ToolTip { Placement = PlacementMode.Mouse, PlacementTarget = this, MaxWidth = 420 };
        ToolTip = tip;
        ToolTipService.SetInitialShowDelay(this, 500);
        AutomationProperties.SetName(this, "使用时间线。方向键选择记录，Shift加方向键选择范围，加减键缩放，Escape取消范围。日志提供相同记录与详情。");
    }

    public event Action<long>? IntervalSelected;
    public event Action<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)?>? RangeChanged;
    public event Action? ViewChanged;
    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc)? Range { get; private set; }
    public DateTimeOffset VisibleStart => visibleStart;
    public DateTimeOffset VisibleEnd => visibleEnd;
    public bool CanZoomIn => day is not null && visibleEnd - visibleStart > TimeSpan.FromSeconds(2);
    public bool CanZoomOut => day is not null && visibleEnd - visibleStart < day.EndUtc - day.StartUtc;

    public void SetSearch(string query)
    {
        searchText = query;
        InvalidateVisual();
    }

    public void SetDay(DayView value)
    {
        var changedDay = day is null || day.Date != value.Date || day.StartUtc != value.StartUtc || day.EndUtc != value.EndUtc;
        day = value;
        intervals = value.Intervals.OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
        if (changedDay)
        {
            visibleStart = value.StartUtc;
            visibleEnd = value.EndUtc;
            Range = null;
            selectedId = null;
            keyboardAnchor = null;
            dragStart = null;
            ReleaseMouseCapture();
        }
        if (selectedId is long id && !intervals.Any(x => x.Id == id)) selectedId = null;
        if (hoveredId is long hovered) UpdateTip(intervals.FirstOrDefault(x => x.Id == hovered));
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void Clear()
    {
        day = null;
        intervals = [];
        selectedId = null;
        keyboardAnchor = null;
        Range = null;
        dragStart = null;
        tip.IsOpen = false;
        ReleaseMouseCapture();
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void SelectInterval(long id, bool reveal = true)
    {
        var interval = intervals.FirstOrDefault(x => x.Id == id);
        if (interval is null) return;
        if (!IsKeyboardFocused) keyboardAnchor = null;
        selectedId = id;
        if (reveal && (interval.EndUtc <= visibleStart || interval.StartUtc >= visibleEnd))
            SetViewport(Middle(interval.StartUtc, interval.EndUtc), (visibleEnd - visibleStart).Ticks);
        AutomationProperties.SetName(this, $"已选：{TipText(interval)}。方向键选择记录，Shift加方向键选择范围，加减键缩放。");
        InvalidateVisual();
    }

    public void ClearSelection()
    {
        selectedId = null;
        keyboardAnchor = null;
        InvalidateVisual();
    }

    public void ZoomIn()
    {
        if (!CanZoomIn) return;
        var width = (visibleEnd - visibleStart).Ticks / 2;
        var center = Middle(visibleStart, visibleEnd);
        if (Range is { } range)
        {
            center = Middle(range.StartUtc, range.EndUtc);
            width = Math.Min(width, (range.EndUtc - range.StartUtc).Ticks);
        }
        else if (intervals.FirstOrDefault(x => x.Id == selectedId) is { } selected)
        {
            center = Middle(selected.StartUtc, selected.EndUtc);
            width = Math.Min(width, Math.Max(TimeSpan.FromSeconds(2).Ticks, (selected.EndUtc - selected.StartUtc).Ticks * 4));
        }
        SetViewport(center, width);
    }

    public void ZoomOut()
    {
        if (CanZoomOut) SetViewport(Middle(visibleStart, visibleEnd), (visibleEnd - visibleStart).Ticks * 2);
    }

    public void ResetDay()
    {
        if (day is null) return;
        visibleStart = day.StartUtc;
        visibleEnd = day.EndUtc;
        ClearRange();
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void ClearRange()
    {
        Range = null;
        keyboardAnchor = null;
        dragStart = null;
        dragEnd = null;
        ReleaseMouseCapture();
        InvalidateVisual();
        RangeChanged?.Invoke(null);
    }

    public static Brush ApplicationBrush(string? key)
    {
        if (SystemParameters.HighContrast) return SystemColors.WindowTextBrush;
        uint hash = 2166136261;
        foreach (var character in key ?? "unknown") hash = unchecked((hash ^ character) * 16777619);
        return AppPalette[hash % AppPalette.Length];
    }

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private void SetViewport(DateTimeOffset center, long requestedTicks)
    {
        if (day is null) return;
        var width = Math.Clamp(requestedTicks, Math.Min(TimeSpan.FromSeconds(2).Ticks, (day.EndUtc - day.StartUtc).Ticks),
            (day.EndUtc - day.StartUtc).Ticks);
        var first = Math.Clamp(center.UtcTicks - width / 2, day.StartUtc.UtcTicks, day.EndUtc.UtcTicks - width);
        visibleStart = new DateTimeOffset(first, TimeSpan.Zero);
        visibleEnd = visibleStart.AddTicks(width);
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    private static DateTimeOffset Middle(DateTimeOffset start, DateTimeOffset end) => start.AddTicks((end - start).Ticks / 2);
    private double PlotWidth => Math.Max(1, ActualWidth - LabelWidth);
    private double X(DateTimeOffset time) => LabelWidth + (time - visibleStart).Ticks / (double)(visibleEnd - visibleStart).Ticks * PlotWidth;
    private DateTimeOffset TimeAt(double x) => visibleStart.AddTicks((long)(Math.Clamp((x - LabelWidth) / PlotWidth, 0, 1) * (visibleEnd - visibleStart).Ticks));
    private static int Lane(RecordedInterval interval) => interval.Kind == IntervalKind.Usage ? 0 : interval.Kind == IntervalKind.Unrecorded ? 2 : 1;
    private RecordedInterval? Hit(DateTimeOffset time, double y) => intervals.FirstOrDefault(x => x.StartUtc <= time && x.EndUtc > time
        && y >= BarTop + Lane(x) * LaneSpacing && y <= BarTop + Lane(x) * LaneSpacing + BarHeight);

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth <= LabelWidth) return;
        context.DrawRectangle(ReviewTheme.Surface, null, new Rect(0, 0, ActualWidth, PlotHeight));
        context.DrawLine(new Pen(ReviewTheme.Border, 0.5), new Point(LabelWidth, 0), new Point(LabelWidth, PlotHeight));
        var laneLabels = new[] { "使用", "停止", "未记录" };
        for (var lane = 0; lane < laneLabels.Length; lane++)
        {
            var label = Text(laneLabels[lane], ReviewTheme.Muted);
            context.DrawText(label, new Point(LabelWidth - label.Width - 5, BarTop + lane * LaneSpacing - 3));
        }
        if (day is null || visibleEnd <= visibleStart)
        {
            DrawText(context, day is null ? "正在读取时间线…" : "没有可用记录", new Point(LabelWidth + 10, 18), ReviewTheme.Muted);
            return;
        }

        var hatch = UnrecordedBrush();
        var hasVisibleRecord = false;
        foreach (var interval in intervals)
        {
            if (interval.EndUtc <= visibleStart || interval.StartUtc >= visibleEnd) continue;
            var left = Math.Max(LabelWidth, X(interval.StartUtc));
            var right = Math.Min(ActualWidth, X(interval.EndUtc));
            if (right <= left) continue;
            hasVisibleRecord = true;
            var rectangle = new Rect(left, BarTop + Lane(interval) * LaneSpacing, right - left, BarHeight);
            var brush = interval.Kind == IntervalKind.Usage ? ApplicationBrush(interval.AppKey)
                : interval.Kind == IntervalKind.Unrecorded ? hatch : ReviewTheme.Muted;
            var matches = DisplayText.MatchesSearch(interval, searchText);
            context.PushOpacity(matches ? 0.85 : 0.16);
            context.DrawRoundedRectangle(brush, null, rectangle, Math.Min(1, rectangle.Width / 2), 1);
            context.Pop();
            if (interval.Id == selectedId)
            {
                context.PushClip(new RectangleGeometry(rectangle));
                context.DrawRectangle(SystemParameters.HighContrast ? ReviewTheme.Background : null, new Pen(ReviewTheme.Accent, 2), rectangle);
                context.Pop();
            }
            if (interval.IsCurrent)
            {
                context.DrawLine(new Pen(ReviewTheme.Accent, 1), new Point(right, rectangle.Top - 2), new Point(right, rectangle.Bottom + 2));
            }
        }

        var range = dragStart is { } first && dragEnd is { } last
            ? (StartUtc: first < last ? first : last, EndUtc: first < last ? last : first)
            : Range;
        if (range is { } selectedRange)
        {
            var left = Math.Clamp(X(selectedRange.StartUtc), LabelWidth, ActualWidth);
            var right = Math.Clamp(X(selectedRange.EndUtc), LabelWidth, ActualWidth);
            if (right > left)
            {
                context.PushOpacity(0.15);
                context.DrawRectangle(ReviewTheme.Accent, null, new Rect(left, 0, right - left, PlotHeight));
                context.Pop();
                context.DrawLine(new Pen(ReviewTheme.Accent, 1), new Point(left, 0), new Point(left, PlotHeight));
                context.DrawLine(new Pen(ReviewTheme.Accent, 1), new Point(right, 0), new Point(right, PlotHeight));
            }
        }
        if (!hasVisibleRecord)
            DrawText(context, "没有可用记录", new Point(LabelWidth + 10, 18), ReviewTheme.Muted);
        DrawAxis(context);
        if (IsKeyboardFocused) context.DrawRectangle(null, new Pen(ReviewTheme.Accent, 1), new Rect(0, 1, ActualWidth, ActualHeight - 2));
    }

    private void DrawAxis(DrawingContext context)
    {
        var span = visibleEnd - visibleStart;
        var format = span < TimeSpan.FromSeconds(10) ? "HH:mm:ss.fff" : span < TimeSpan.FromHours(1) ? "HH:mm:ss" : "HH:mm";
        var first = Text(visibleStart.ToLocalTime().ToString(format, CultureInfo.CurrentCulture), ReviewTheme.Muted);
        var endLocal = visibleEnd.ToLocalTime();
        var endLabel = (endLocal.Date > visibleStart.ToLocalTime().Date ? "次日 " : string.Empty) + endLocal.ToString(format, CultureInfo.CurrentCulture);
        var last = Text(endLabel, ReviewTheme.Muted);
        context.DrawText(first, new Point(LabelWidth, 55));
        context.DrawText(last, new Point(Math.Max(LabelWidth, ActualWidth - last.Width), 55));
        var steps = new[] { 1, 5, 15, 60, 300, 900, 3600, 7200, 14400 };
        var step = TimeSpan.FromSeconds(steps.FirstOrDefault(x => x >= span.TotalSeconds / 7, steps[^1])).Ticks;
        var tick = visibleStart.UtcTicks - visibleStart.UtcTicks % step + step;
        for (; tick < visibleEnd.UtcTicks; tick += step)
        {
            var time = new DateTimeOffset(tick, TimeSpan.Zero);
            var x = X(time);
            var text = Text(time.ToLocalTime().ToString(format, CultureInfo.CurrentCulture), ReviewTheme.Muted);
            if (x - text.Width / 2 <= LabelWidth + first.Width + 12 || x + text.Width / 2 >= ActualWidth - last.Width - 12) continue;
            context.DrawLine(new Pen(ReviewTheme.Border, 0.5), new Point(x, 0), new Point(x, PlotHeight));
            context.DrawText(text, new Point(x - text.Width / 2, 55));
        }
    }

    private static Brush UnrecordedBrush()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(ReviewTheme.Surface, null, new Rect(0, 0, 8, 8));
            context.DrawLine(new Pen(ReviewTheme.Muted, 1), new Point(0, 8), new Point(8, 0));
        }
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 8, 8)
        };
    }

    private FormattedText Text(string value, Brush brush) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface("Segoe UI"), 11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private void DrawText(DrawingContext context, string value, Point position, Brush brush) => context.DrawText(Text(value, brush), position);
    private static string TipText(RecordedInterval interval) => $"{DisplayText.AppName(interval)}{(interval.IsCurrent ? " · 进行中" : string.Empty)}\n开始：{DisplayText.Time(interval.StartUtc, true)}\n结束：{DisplayText.Time(interval.EndUtc, true)}\n时长：{DisplayText.Duration(interval.Duration, true)}";

    private void UpdateTip(RecordedInterval? interval)
    {
        tip.Content = interval is null ? "此范围没有可用记录。" : TipText(interval);
        hoveredId = interval?.Id;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (day is null || visibleEnd <= visibleStart || e.GetPosition(this).X < LabelWidth || e.GetPosition(this).Y > PlotHeight) return;
        Focus();
        dragStartX = e.GetPosition(this).X;
        dragStart = TimeAt(dragStartX);
        dragEnd = dragStart;
        keyboardAnchor = null;
        tip.IsOpen = false;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (day is null || visibleEnd <= visibleStart) return;
        var point = e.GetPosition(this);
        if (panStartX is { } pan)
        {
            var span = (panEndUtc - panStartUtc).Ticks;
            var shift = (long)((pan - point.X) / PlotWidth * span);
            SetViewport(Middle(panStartUtc, panEndUtc).AddTicks(shift), span);
        }
        else if (dragStart is not null)
        {
            dragEnd = TimeAt(point.X);
            InvalidateVisual();
        }
        else
        {
            var overBar = point.X >= LabelWidth && point.Y >= 0 && point.Y <= PlotHeight;
            ToolTipService.SetIsEnabled(this, overBar);
            if (overBar) UpdateTip(Hit(TimeAt(point.X), point.Y));
            else tip.IsOpen = false;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (dragStart is not { } start) return;
        var point = e.GetPosition(this);
        var end = TimeAt(point.X);
        dragStart = null;
        dragEnd = null;
        ReleaseMouseCapture();
        if (Math.Abs(point.X - dragStartX) >= SystemParameters.MinimumHorizontalDragDistance && start != end)
            SetRange(start < end ? start : end, start < end ? end : start);
        else if (Hit(end, point.Y) is { } interval)
        {
            SelectInterval(interval.Id);
            IntervalSelected?.Invoke(interval.Id);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        dragStart = null;
        dragEnd = null;
        panStartX = null;
        Cursor = Cursors.Cross;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (day is null || e.GetPosition(this).X < LabelWidth) return;
        var center = TimeAt(e.GetPosition(this).X);
        var span = (visibleEnd - visibleStart).Ticks;
        SetViewport(center, e.Delta > 0 ? span / 2 : span * 2);
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (day is null || e.GetPosition(this).X < LabelWidth) return;
        panStartX = e.GetPosition(this).X;
        panStartUtc = visibleStart;
        panEndUtc = visibleEnd;
        tip.IsOpen = false;
        Cursor = Cursors.ScrollWE;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (panStartX is not { } start) return;
        var clear = Math.Abs(e.GetPosition(this).X - start) < SystemParameters.MinimumHorizontalDragDistance;
        panStartX = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Cross;
        if (clear) ClearRange();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Add or Key.OemPlus) { ZoomIn(); e.Handled = true; return; }
        if (e.Key is Key.Subtract or Key.OemMinus) { ZoomOut(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { ClearRange(); e.Handled = true; return; }
        if (intervals.Length == 0 || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Enter)) return;
        var index = Array.FindIndex(intervals, x => x.Id == selectedId);
        var next = e.Key == Key.Home ? 0 : e.Key == Key.End ? intervals.Length - 1
            : index < 0 ? 0 : Math.Clamp(index + (e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0), 0, intervals.Length - 1);
        var target = intervals[next];
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            keyboardAnchor ??= index >= 0 ? intervals[index] : target;
            var anchor = keyboardAnchor;
            SetRange(keyboardAnchor.StartUtc < target.StartUtc ? keyboardAnchor.StartUtc : target.StartUtc,
                keyboardAnchor.EndUtc > target.EndUtc ? keyboardAnchor.EndUtc : target.EndUtc);
            keyboardAnchor = anchor;
        }
        else keyboardAnchor = null;
        SelectInterval(target.Id);
        IntervalSelected?.Invoke(target.Id);
        e.Handled = true;
    }

    private void SetRange(DateTimeOffset start, DateTimeOffset end)
    {
        Range = (start, end);
        InvalidateVisual();
        RangeChanged?.Invoke(Range);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
