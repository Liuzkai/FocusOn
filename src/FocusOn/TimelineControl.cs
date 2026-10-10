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
    private const double BarTop = 19;
    private const double BarHeight = 34;
    private static readonly Brush[] AppPalette =
    [
        FrozenBrush(0x15, 0x5F, 0xA0), FrozenBrush(0x00, 0x6B, 0x5E),
        FrozenBrush(0x78, 0x45, 0x9B), FrozenBrush(0x9A, 0x4E, 0x00),
        FrozenBrush(0xA3, 0x33, 0x56), FrozenBrush(0x3D, 0x6C, 0x13),
        FrozenBrush(0x4D, 0x56, 0xA1), FrozenBrush(0x7B, 0x5B, 0x39)
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

    public TimelineControl()
    {
        Focusable = true;
        ClipToBounds = true;
        Cursor = Cursors.Cross;
        Height = 88;
        tip = new ToolTip { Placement = PlacementMode.Mouse, PlacementTarget = this, MaxWidth = 420 };
        ToolTip = tip;
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
    private double X(DateTimeOffset time) => (time - visibleStart).Ticks / (double)(visibleEnd - visibleStart).Ticks * ActualWidth;
    private DateTimeOffset TimeAt(double x) => visibleStart.AddTicks((long)(Math.Clamp(x / Math.Max(ActualWidth, 1), 0, 1) * (visibleEnd - visibleStart).Ticks));
    private RecordedInterval? Hit(DateTimeOffset time) => intervals.FirstOrDefault(x => x.StartUtc <= time && x.EndUtc > time);

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth <= 0) return;
        var bar = new Rect(0, BarTop, ActualWidth, BarHeight);
        context.DrawRectangle(SystemColors.WindowBrush, new Pen(SystemColors.ControlDarkBrush, 1), bar);
        if (day is null || visibleEnd <= visibleStart)
        {
            DrawText(context, day is null ? "正在读取时间线…" : "没有可用记录", new Point(8, BarTop + 8), SystemColors.WindowTextBrush);
            return;
        }

        var hatch = UnrecordedBrush();
        var hasVisibleRecord = false;
        foreach (var interval in intervals)
        {
            if (interval.EndUtc <= visibleStart || interval.StartUtc >= visibleEnd) continue;
            var left = Math.Max(0, X(interval.StartUtc));
            var right = Math.Min(ActualWidth, X(interval.EndUtc));
            if (right <= left) continue;
            hasVisibleRecord = true;
            var rectangle = new Rect(left, BarTop, right - left, BarHeight);
            var brush = interval.Kind == IntervalKind.Usage ? ApplicationBrush(interval.AppKey)
                : interval.Kind == IntervalKind.Unrecorded ? hatch : SystemColors.ControlBrush;
            context.DrawRectangle(brush, null, rectangle);
            var label = Text(DisplayText.AppName(interval), interval.Kind == IntervalKind.Usage
                ? SystemParameters.HighContrast ? SystemColors.WindowBrush : Brushes.White : SystemColors.ControlTextBrush);
            if (label.Width + 10 <= rectangle.Width)
                context.DrawText(label, new Point(rectangle.Left + 5, rectangle.Top + (rectangle.Height - label.Height) / 2));
            if (interval.Id == selectedId)
            {
                context.PushClip(new RectangleGeometry(rectangle));
                context.DrawRectangle(null, new Pen(SystemColors.HighlightBrush, 3), rectangle);
                context.Pop();
            }
            if (interval.IsCurrent)
            {
                var current = Text("进行中", SystemColors.WindowTextBrush);
                context.DrawText(current, new Point(Math.Clamp(right - current.Width, 0, Math.Max(0, ActualWidth - current.Width)), 0));
            }
        }

        var range = dragStart is { } first && dragEnd is { } last
            ? (StartUtc: first < last ? first : last, EndUtc: first < last ? last : first)
            : Range;
        if (range is { } selectedRange)
        {
            var left = Math.Clamp(X(selectedRange.StartUtc), 0, ActualWidth);
            var right = Math.Clamp(X(selectedRange.EndUtc), 0, ActualWidth);
            if (right > left)
            {
                context.PushOpacity(0.2);
                context.DrawRectangle(SystemColors.HighlightBrush, null, new Rect(left, BarTop, right - left, BarHeight));
                context.Pop();
                context.DrawLine(new Pen(SystemColors.HighlightBrush, 1), new Point(left, BarTop - 3), new Point(left, BarTop + BarHeight + 3));
                context.DrawLine(new Pen(SystemColors.HighlightBrush, 1), new Point(right, BarTop - 3), new Point(right, BarTop + BarHeight + 3));
            }
        }
        if (!hasVisibleRecord)
            DrawText(context, "没有可用记录", new Point(8, BarTop + 8), SystemColors.WindowTextBrush);
        DrawAxis(context);
        if (IsKeyboardFocused) context.DrawRectangle(null, new Pen(SystemColors.HighlightBrush, 1), new Rect(0, 1, ActualWidth, ActualHeight - 2));
    }

    private void DrawAxis(DrawingContext context)
    {
        var span = visibleEnd - visibleStart;
        var format = span < TimeSpan.FromSeconds(10) ? "HH:mm:ss.fff" : span < TimeSpan.FromHours(1) ? "HH:mm:ss" : "HH:mm";
        var first = Text(visibleStart.ToLocalTime().ToString(format, CultureInfo.CurrentCulture), SystemColors.WindowTextBrush);
        var endLocal = visibleEnd.ToLocalTime();
        var endLabel = (endLocal.Date > visibleStart.ToLocalTime().Date ? "次日 " : string.Empty) + endLocal.ToString(format, CultureInfo.CurrentCulture);
        var last = Text(endLabel, SystemColors.WindowTextBrush);
        context.DrawText(first, new Point(0, 61));
        context.DrawText(last, new Point(Math.Max(0, ActualWidth - last.Width), 61));
        var steps = new[] { 1, 5, 15, 60, 300, 900, 3600, 7200, 14400 };
        var step = TimeSpan.FromSeconds(steps.FirstOrDefault(x => x >= span.TotalSeconds / 7, steps[^1])).Ticks;
        var tick = visibleStart.UtcTicks - visibleStart.UtcTicks % step + step;
        for (; tick < visibleEnd.UtcTicks; tick += step)
        {
            var time = new DateTimeOffset(tick, TimeSpan.Zero);
            var x = X(time);
            var text = Text(time.ToLocalTime().ToString(format, CultureInfo.CurrentCulture), SystemColors.WindowTextBrush);
            if (x - text.Width / 2 <= first.Width + 12 || x + text.Width / 2 >= ActualWidth - last.Width - 12) continue;
            context.DrawLine(new Pen(SystemColors.ControlDarkBrush, 1), new Point(x, 55), new Point(x, 59));
            context.DrawText(text, new Point(x - text.Width / 2, 61));
        }
    }

    private static Brush UnrecordedBrush()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(SystemColors.WindowBrush, null, new Rect(0, 0, 8, 8));
            context.DrawLine(new Pen(SystemColors.GrayTextBrush, 1), new Point(0, 8), new Point(8, 0));
        }
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 8, 8)
        };
    }

    private FormattedText Text(string value, Brush brush) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface("Segoe UI"), 12, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
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
        if (day is null || visibleEnd <= visibleStart) return;
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
        if (dragStart is not null)
        {
            dragEnd = TimeAt(point.X);
            InvalidateVisual();
        }
        else
        {
            var overBar = point.Y >= BarTop && point.Y <= BarTop + BarHeight;
            ToolTipService.SetIsEnabled(this, overBar);
            if (overBar) UpdateTip(Hit(TimeAt(point.X)));
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
        else if (Hit(end) is { } interval)
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
        InvalidateVisual();
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
