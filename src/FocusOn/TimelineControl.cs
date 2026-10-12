using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace FocusOn;

public sealed class TimelineControl : Control
{
    private const double LabelWidth = 54, LaneHeight = 22, BarTop = 8, BarHeight = 12;
    private DayView? day;
    private RecordedInterval[] intervals = [];
    private DateTimeOffset boundsStart, boundsEnd, visibleStart, visibleEnd;
    private bool autoViewport = true;
    private long? selectedId;
    private string search = string.Empty;
    private Point? dragOrigin;
    private DateTimeOffset dragAt, dragEnd, panStart, panEnd;
    private bool panning;
    private RecordedInterval? keyboardAnchor;
    private IPointer? captured;

    public TimelineControl()
    {
        Focusable = true; ClipToBounds = true; Height = 96;
        Cursor = new Cursor(StandardCursorType.Cross);
        AutomationProperties.SetName(this, "使用时间线。方向键选择，Shift 加方向键选择范围，加减键缩放，Escape 取消范围。");
    }

    public event Action<long>? IntervalSelected;
    public event Action<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)?>? RangeChanged;
    public event Action? ViewChanged;
    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc)? Range { get; private set; }
    public bool FullDay { get; private set; }
    public DateTimeOffset VisibleStart => visibleStart;
    public DateTimeOffset VisibleEnd => visibleEnd;
    public bool CanZoomIn => day is not null && visibleEnd - visibleStart > TimeSpan.FromSeconds(2);
    public bool CanZoomOut => day is not null && visibleEnd - visibleStart < boundsEnd - boundsStart;
    public void SetSearch(string query) { search = query; InvalidateVisual(); }

    public void SetDay(DayView value)
    {
        var newDay = day is null || day.Date != value.Date || day.StartUtc != value.StartUtc || day.EndUtc != value.EndUtc;
        day = value;
        intervals = value.Intervals.OrderBy(x => x.StartUtc).ThenBy(x => x.Id).ToArray();
        (boundsStart, boundsEnd) = TimelineBounds.For(value, FullDay);
        if (newDay || autoViewport) { visibleStart = boundsStart; visibleEnd = boundsEnd; autoViewport = true; }
        else SetViewport(Middle(visibleStart, visibleEnd), (visibleEnd - visibleStart).Ticks);
        if (newDay) { Range = null; selectedId = null; keyboardAnchor = null; CancelDrag(); }
        if (selectedId is { } id && !intervals.Any(x => x.Id == id)) selectedId = null;
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void Clear()
    {
        day = null; intervals = []; selectedId = null; keyboardAnchor = null; Range = null; CancelDrag();
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void SelectInterval(long id)
    {
        var target = intervals.FirstOrDefault(x => x.Id == id);
        if (target is null) return;
        selectedId = id;
        if (target.EndUtc <= boundsStart || target.StartUtc >= boundsEnd) SetFullDay(true);
        if (target.EndUtc <= visibleStart || target.StartUtc >= visibleEnd)
            SetViewport(Middle(target.StartUtc, target.EndUtc), (visibleEnd - visibleStart).Ticks);
        AutomationProperties.SetName(this, "已选：" + TipText(target));
        InvalidateVisual();
    }
    public void ClearSelection() { selectedId = null; keyboardAnchor = null; InvalidateVisual(); }
    public void ClearRange() { Range = null; keyboardAnchor = null; CancelDrag(); InvalidateVisual(); RangeChanged?.Invoke(null); }
    public void ZoomIn()
    {
        if (!CanZoomIn) return;
        var center = Middle(visibleStart, visibleEnd);
        var width = (visibleEnd - visibleStart).Ticks / 2;
        if (Range is { } range) { center = Middle(range.StartUtc, range.EndUtc); width = Math.Min(width, (range.EndUtc - range.StartUtc).Ticks); }
        else if (intervals.FirstOrDefault(x => x.Id == selectedId) is { } selected)
        { center = Middle(selected.StartUtc, selected.EndUtc); width = Math.Min(width, Math.Max(TimeSpan.FromSeconds(2).Ticks, (selected.EndUtc - selected.StartUtc).Ticks * 4)); }
        SetViewport(center, width);
    }
    public void ZoomOut() { if (CanZoomOut) SetViewport(Middle(visibleStart, visibleEnd), (visibleEnd - visibleStart).Ticks * 2); }
    public void ResetDay()
    {
        if (day is null) return;
        autoViewport = true; (boundsStart, boundsEnd) = TimelineBounds.For(day, FullDay);
        visibleStart = boundsStart; visibleEnd = boundsEnd; ClearRange(); InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void SetFullDay(bool value) { FullDay = value; ResetDay(); }
    public static IBrush ApplicationBrush(string? key) => ReviewTheme.ApplicationBrush(key);
    private static DateTimeOffset Middle(DateTimeOffset start, DateTimeOffset end) => start.AddTicks((end - start).Ticks / 2);
    private void SetViewport(DateTimeOffset center, long requested)
    {
        if (day is null) return;
        var total = (boundsEnd - boundsStart).Ticks;
        var width = Math.Clamp(requested, Math.Min(TimeSpan.FromSeconds(2).Ticks, total), total);
        visibleStart = new(Math.Clamp(center.UtcTicks - width / 2, boundsStart.UtcTicks, boundsEnd.UtcTicks - width), TimeSpan.Zero);
        visibleEnd = visibleStart.AddTicks(width); autoViewport = width == total;
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    private double X(DateTimeOffset at) => LabelWidth + Math.Clamp((at - visibleStart).TotalSeconds / Math.Max(0.000001, (visibleEnd - visibleStart).TotalSeconds), 0, 1) * Math.Max(0, Bounds.Width - LabelWidth);
    private DateTimeOffset TimeAt(double x) => visibleStart.AddTicks((long)(Math.Clamp((x - LabelWidth) / Math.Max(1, Bounds.Width - LabelWidth), 0, 1) * (visibleEnd - visibleStart).Ticks));
    private static int Lane(RecordedInterval interval) => interval.Kind == IntervalKind.TaskTimer ? 2 : interval.Kind is IntervalKind.Usage or IntervalKind.Unrecorded ? 0 : 1;
    private RecordedInterval? Hit(Point point)
    {
        var at = TimeAt(point.X);
        return intervals.FirstOrDefault(x => x.StartUtc <= at && x.EndUtc > at
            && point.Y >= BarTop + Lane(x) * LaneHeight && point.Y <= BarTop + Lane(x) * LaneHeight + BarHeight);
    }
    private static string TipText(RecordedInterval value) => $"{DisplayText.AppName(value)} · {DisplayText.Title(value)}\n{DisplayText.Time(value.StartUtc, true)} — {DisplayText.Time(value.EndUtc, true)}\n{DisplayText.Duration(value.Duration, true)}";
    private static FormattedText Text(string text, IBrush brush) => new(text, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface("Inter"), 11, brush);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= LabelWidth) return;
        context.DrawRectangle(ReviewTheme.Surface, null, new Rect(0, 0, Bounds.Width, 72));
        for (var lane = 0; lane < 3; lane++) context.DrawText(Text(new[] { "使用", "停止", "计时" }[lane], ReviewTheme.Muted), new Point(8, BarTop + LaneHeight * lane));
        if (day is null || visibleEnd <= visibleStart) return;
        foreach (var interval in intervals)
        {
            if (interval.EndUtc <= visibleStart || interval.StartUtc >= visibleEnd) continue;
            var rectangle = new Rect(X(interval.StartUtc), BarTop + Lane(interval) * LaneHeight, X(interval.EndUtc) - X(interval.StartUtc), BarHeight);
            var brush = interval.Kind == IntervalKind.Usage ? ApplicationBrush(interval.AppKey)
                : interval.Kind == IntervalKind.TaskTimer ? ReviewTheme.Accent : ReviewTheme.Muted;
            using (context.PushOpacity(DisplayText.MatchesSearch(interval, search) ? 0.85 : 0.16))
            {
                context.DrawRectangle(brush, null, rectangle, Math.Min(1, rectangle.Width / 2), 1);
                if (interval.Kind == IntervalKind.Unrecorded)
                {
                    using (context.PushClip(rectangle))
                        for (var x = rectangle.Left - BarHeight; x < rectangle.Right; x += 6)
                            context.DrawLine(new Pen(ReviewTheme.Background, 2), new Point(x, rectangle.Bottom), new Point(x + BarHeight, rectangle.Top));
                }
            }
            if (interval.Id == selectedId) context.DrawRectangle(null, new Pen(ReviewTheme.Accent, 2), rectangle);
            if (interval.IsCurrent) context.DrawLine(new Pen(ReviewTheme.Accent, 1), rectangle.TopRight - new Vector(0, 2), rectangle.BottomRight + new Vector(0, 2));
        }
        var selection = dragOrigin is not null && !panning ? (StartUtc: dragAt < dragEnd ? dragAt : dragEnd, EndUtc: dragAt < dragEnd ? dragEnd : dragAt) : Range;
        if (selection is { } selected && selected.EndUtc > selected.StartUtc)
        {
            using (context.PushOpacity(0.2)) context.DrawRectangle(ReviewTheme.Accent, null, new Rect(X(selected.StartUtc), 0, X(selected.EndUtc) - X(selected.StartUtc), 72));
        }
        var span = visibleEnd - visibleStart;
        var format = span < TimeSpan.FromHours(1) ? "HH:mm:ss" : "HH:mm";
        for (var i = 0; i <= 6; i++)
        {
            var at = visibleStart.AddTicks(span.Ticks * i / 6);
            var label = Text(at.ToLocalTime().ToString(format), ReviewTheme.Muted);
            context.DrawText(label, new Point(Math.Clamp(X(at) - label.Width / 2, LabelWidth, Math.Max(LabelWidth, Bounds.Width - label.Width)), 77));
        }
        if (IsFocused) context.DrawRectangle(null, new Pen(ReviewTheme.Accent, 1), new Rect(1, 1, Bounds.Width - 2, Bounds.Height - 2));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (day is null || point.Position.X < LabelWidth) return;
        Focus(); dragOrigin = point.Position; dragAt = dragEnd = TimeAt(point.Position.X);
        panning = point.Properties.IsRightButtonPressed;
        panStart = visibleStart; panEnd = visibleEnd; captured = e.Pointer; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (dragOrigin is { } origin)
        {
            if (panning)
            {
                var shift = (long)((origin.X - point.X) / Math.Max(1, Bounds.Width - LabelWidth) * (panEnd - panStart).Ticks);
                SetViewport(Middle(panStart, panEnd).AddTicks(shift), (panEnd - panStart).Ticks);
            }
            else { dragEnd = TimeAt(point.X); InvalidateVisual(); }
            e.Handled = true;
        }
        else ToolTip.SetTip(this, Hit(point) is { } target ? TipText(target) : null);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (dragOrigin is not { } origin) return;
        var point = e.GetPosition(this);
        var start = dragAt; var end = TimeAt(point.X); var pan = panning;
        CancelDrag();
        if (pan) { if (Math.Abs(point.X - origin.X) < 4) ClearRange(); }
        else if (Math.Abs(point.X - origin.X) >= 4 && start != end) SetRange(start < end ? start : end, start < end ? end : start);
        else if (Hit(point) is { } target) { SelectInterval(target.Id); IntervalSelected?.Invoke(target.Id); }
        InvalidateVisual(); e.Handled = true;
    }
    private void CancelDrag() { dragOrigin = null; var pointer = captured; captured = null; pointer?.Capture(null); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { dragOrigin = null; captured = null; base.OnPointerCaptureLost(e); InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (day is null || e.GetPosition(this).X < LabelWidth) return;
        var span = (visibleEnd - visibleStart).Ticks;
        SetViewport(TimeAt(e.GetPosition(this).X), e.Delta.Y > 0 ? span / 2 : span * 2); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Add or Key.OemPlus) { ZoomIn(); e.Handled = true; return; }
        if (e.Key is Key.Subtract or Key.OemMinus) { ZoomOut(); e.Handled = true; return; }
        if (e.Key == Key.Escape) { ClearRange(); e.Handled = true; return; }
        if (intervals.Length == 0 || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Enter)) return;
        var index = Array.FindIndex(intervals, x => x.Id == selectedId);
        var next = e.Key == Key.Home ? 0 : e.Key == Key.End ? intervals.Length - 1 : index < 0 ? 0
            : Math.Clamp(index + (e.Key == Key.Left ? -1 : e.Key == Key.Right ? 1 : 0), 0, intervals.Length - 1);
        var target = intervals[next];
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            keyboardAnchor ??= index >= 0 ? intervals[index] : target;
            SetRange(keyboardAnchor.StartUtc < target.StartUtc ? keyboardAnchor.StartUtc : target.StartUtc,
                keyboardAnchor.EndUtc > target.EndUtc ? keyboardAnchor.EndUtc : target.EndUtc);
        }
        else keyboardAnchor = null;
        SelectInterval(target.Id); IntervalSelected?.Invoke(target.Id); e.Handled = true;
    }
    private void SetRange(DateTimeOffset start, DateTimeOffset end) { Range = (start, end); InvalidateVisual(); RangeChanged?.Invoke(Range); }
}
