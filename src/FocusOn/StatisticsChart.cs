using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace FocusOn;

public enum StatisticsChartKind { Donut, Bars, Lines }
public sealed record StatisticsAppRow(int Rank, string Key, string Name, TimeSpan Value, double Fraction, IBrush Color)
{
    public string Duration => DisplayText.Duration(Value);
    public string Share => Fraction.ToString("P1", CultureInfo.CurrentCulture);
    public string Pattern => Rank switch { 1 => "—", 2 => "– –", _ => "···" };
}

public sealed class StatisticsChart : Control
{
    private StatisticsView? view;
    private readonly Popup hint;
    private readonly TextBlock hintText = new() { TextWrapping = TextWrapping.Wrap };
    private Point? hover;
    private Window? owner;
    private int keyboardIndex = -1;
    private int activeIndex = -1;
    private readonly HoverMotion motion;
    public StatisticsChartKind Kind { get; set; }

    public StatisticsChart()
    {
        motion = new(InvalidateVisual);
        ClipToBounds = true; Focusable = true;
        hint = new Popup { PlacementTarget = this, Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.BottomRight, PlacementGravity = PopupGravity.BottomRight,
            HorizontalOffset = 12, VerticalOffset = 16, ShouldUseOverlayLayer = true, IsHitTestVisible = false,
            TakesFocusFromNativeControl = false, Child = new ToolTip { Content = hintText, MaxWidth = 380, Opacity = 1, IsHitTestVisible = false } };
        LogicalChildren.Add(hint);
    }
    public void SetData(StatisticsView value)
    {
        view = value;
        AutomationProperties.SetName(this, Localizer.Get(Kind switch { StatisticsChartKind.Donut => "软件使用占比", StatisticsChartKind.Bars => "使用时长分布", _ => "软件使用趋势" })
            + " · " + DisplayText.Duration(value.Duration));
        InvalidateVisual(); RefreshHint();
    }
    public static IReadOnlyList<StatisticsAppRow> Rows(StatisticsView value) => value.Apps.Select((x, i) => new StatisticsAppRow(i + 1, x.AppKey,
        x.AppKey == "unknown" ? Localizer.Get("未知软件") : x.AppName, x.Duration, value.Duration.Ticks > 0 ? (double)x.Duration.Ticks / value.Duration.Ticks : 0,
        ReviewTheme.ApplicationBrush(x.AppKey))).ToArray();
    public static IReadOnlyList<StatisticsAppRow> Shares(StatisticsView value)
    {
        var rows = Rows(value).Take(5).ToList();
        if (value.Apps.Count > 5)
        {
            var ticks = value.Apps.Skip(5).Sum(x => x.Duration.Ticks);
            rows.Add(new(6, "", Localizer.Get("其他软件"), TimeSpan.FromTicks(ticks), (double)ticks / value.Duration.Ticks, ReviewTheme.Muted));
        }
        return rows;
    }
    public static string Short(TimeSpan value) => value.TotalHours >= 1
        ? value.TotalHours.ToString("0.#", CultureInfo.CurrentCulture) + Localizer.Get("小时")
        : value.TotalMinutes >= 1 ? value.TotalMinutes.ToString("0.#", CultureInfo.CurrentCulture) + Localizer.Get("分钟")
        : value.TotalSeconds.ToString("0.#", CultureInfo.CurrentCulture) + Localizer.Get("秒");

    private static FormattedText Text(string value, IBrush brush, double size = 11) => new(value, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, new Typeface("Inter"), size, brush);
    private static void CenterText(DrawingContext context, string text, Point center, IBrush brush, double size = 11)
    {
        var value = Text(text, brush, size); context.DrawText(value, new Point(center.X - value.Width / 2, center.Y - value.Height / 2));
    }
    private double Emphasis(int index) => index >= 0 && index < motion.Values.Length ? motion.Values[index] : 0;
    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);
    private double Radius => Math.Max(1, Math.Min(Bounds.Width / 2 - 20, Bounds.Height / 2 - 14));
    private static Point Polar(Point center, double radius, double degrees)
    { var angle = degrees * Math.PI / 180; return center + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius); }
    private Rect Plot => new(48, 20, Math.Max(1, Bounds.Width - 64), Math.Max(1, Bounds.Height - 52));
    private double Maximum(bool lines)
    {
        if (view is null || view.Buckets.Count == 0) return 1;
        var values = lines ? view.Buckets.SelectMany(x => x.Apps.Where(a => view.Apps.Take(3).Any(t => t.AppKey == a.AppKey)).Select(a => a.Duration.TotalHours))
            : view.Buckets.Select(x => x.Duration.TotalHours);
        var max = values.DefaultIfEmpty(0).Max();
        return max <= 1 ? Math.Max(.25, Math.Ceiling(max * 4) / 4) : Math.Ceiling(max);
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // Empty plot space must receive pointer input too, including between thin trend lines.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (Bounds.Width < 70 || Bounds.Height < 80) return;
        if (Kind == StatisticsChartKind.Donut) { RenderDonut(context); return; }
        var plot = Plot; var maximum = Maximum(Kind == StatisticsChartKind.Lines);
        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - plot.Height * i / 4;
            using (context.PushOpacity(.35)) context.DrawLine(new Pen(ReviewTheme.Border, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Text((maximum * i / 4).ToString("0.##", CultureInfo.CurrentCulture) + Localizer.Get("小时"), ReviewTheme.Muted);
            context.DrawText(label, new Point(plot.Left - label.Width - 7, y - label.Height / 2));
        }
        if (view is null || view.Buckets.Count == 0) return;
        var step = plot.Width / view.Buckets.Count;
        var stride = Math.Max(1, (int)Math.Ceiling(view.Buckets.Count * 68.0 / plot.Width));
        for (var i = 0; i < view.Buckets.Count; i++)
        {
            var bucket = view.Buckets[i]; var x = plot.Left + step * (i + .5);
            var emphasis = Emphasis(i);
            if (emphasis > .001)
            {
                using (context.PushOpacity(ReviewTheme.HighContrast ? .22 : .09 * emphasis)) context.DrawRectangle(ReviewTheme.Accent, null,
                    new Rect(x - step * .43, plot.Top, step * .86, plot.Height), 5, 5);
                using (context.PushOpacity(ReviewTheme.HighContrast ? 1 : emphasis)) context.DrawLine(new Pen(ReviewTheme.Accent, 1, DashStyle.Dot),
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
            }
            if (i % stride == 0)
                CenterText(context, bucket.Period.Label, new Point(x, plot.Bottom + 17), i == activeIndex ? ReviewTheme.Foreground : ReviewTheme.Muted);
            if (Kind != StatisticsChartKind.Bars) continue;
            var width = Math.Min(34, step * .65) * (1 + .12 * emphasis);
            using (context.PushOpacity(.45)) context.DrawRectangle(ReviewTheme.Border, null, new Rect(x - width / 2, plot.Top, width, plot.Height), width / 2, width / 2);
            var height = plot.Height * bucket.Duration.TotalHours / maximum;
            if (height > 0)
                context.DrawRectangle(bucket.Apps.Count > 0 ? ReviewTheme.ApplicationBrush(bucket.Apps[0].AppKey) : ReviewTheme.Accent,
                    null, new Rect(x - width / 2, plot.Bottom - height, width, height), Math.Min(width / 2, height / 2), Math.Min(width / 2, height / 2));
            if (step >= 60 && height > 0) CenterText(context, Short(bucket.Duration), new Point(x, Math.Max(8, plot.Bottom - height - 11)), ReviewTheme.Foreground);
        }
        if (Kind != StatisticsChartKind.Lines) return;
        var series = 0;
        foreach (var app in view.Apps.Take(3))
        {
            var color = ReviewTheme.ApplicationBrush(app.AppKey); var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                for (var i = 0; i < view.Buckets.Count; i++)
                {
                    var duration = view.Buckets[i].Apps.FirstOrDefault(x => x.AppKey == app.AppKey)?.Duration ?? TimeSpan.Zero;
                    var point = new Point(plot.Left + step * (i + .5), plot.Bottom - plot.Height * duration.TotalHours / maximum);
                    if (i == 0) path.BeginFigure(point, false); else path.LineTo(point);
                    var emphasis = Emphasis(i);
                    if (emphasis > .001)
                    {
                        using (context.PushOpacity(ReviewTheme.HighContrast ? .3 : .18 * emphasis)) context.DrawEllipse(color, null, point, 10, 10);
                        using (context.PushOpacity(emphasis)) context.DrawEllipse(null, new Pen(color, 1.5), point, 8, 8);
                    }
                    if (step >= 28 || emphasis > .001) context.DrawEllipse(color, new Pen(ReviewTheme.Surface, 1), point, 3.5 + 2 * emphasis, 3.5 + 2 * emphasis);
                }
                path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(color, 2, series == 0 ? null : series == 1 ? DashStyle.Dash : DashStyle.Dot, PenLineCap.Round), geometry);
            series++;
        }
    }
    private void RenderDonut(DrawingContext context)
    {
        var center = Center; var radius = Radius; var inner = radius * .67;
        using (context.PushOpacity(.45))
            foreach (var ring in new[] { radius + 10, radius - 7, inner - 8 }) context.DrawEllipse(null, new Pen(ReviewTheme.Border, 1), center, ring, ring);
        context.DrawEllipse(null, new Pen(ReviewTheme.Border, radius - inner), center, (radius + inner) / 2, (radius + inner) / 2);
        if (view is not null && view.Duration > TimeSpan.Zero)
        {
            var angle = -90.0;
            var index = 0;
            foreach (var row in Shares(view))
            {
                var sweep = row.Fraction * 360; var gap = Math.Min(1.8, sweep * .1);
                var from = angle + gap / 2; var to = angle + sweep - gap / 2;
                var emphasis = Emphasis(index++);
                var sliceCenter = StatisticsMotion.Enabled(this) ? Polar(center, 6 * emphasis, angle + sweep / 2) : center;
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(Polar(sliceCenter, radius, from), true);
                    path.ArcTo(Polar(sliceCenter, radius, to), new Size(radius, radius), 0, to - from > 180, SweepDirection.Clockwise);
                    path.LineTo(Polar(sliceCenter, inner, to));
                    path.ArcTo(Polar(sliceCenter, inner, from), new Size(inner, inner), 0, to - from > 180, SweepDirection.CounterClockwise);
                    path.EndFigure(true);
                }
                context.DrawGeometry(row.Color, null, geometry);
                if (emphasis > .001) using (context.PushOpacity(emphasis)) context.DrawGeometry(null, new Pen(ReviewTheme.HighContrast ? ReviewTheme.Accent : ReviewTheme.Foreground, 1.5), geometry);
                if (row.Fraction >= .08) CenterText(context, row.Fraction.ToString("P0", CultureInfo.CurrentCulture), Polar(sliceCenter, (radius + inner) / 2, angle + sweep / 2), ReviewTheme.Background, 12);
                angle += sweep;
            }
        }
        CenterText(context, Short(view?.Duration ?? TimeSpan.Zero), center - new Vector(0, 9), ReviewTheme.Foreground, 23);
        CenterText(context, Localizer.Get("前台使用时长"), center + new Vector(0, 17), ReviewTheme.Muted);
    }
    private void RefreshHint()
    {
        if (!StatisticsMotion.Active(this) || hover is not { } point || view is null) { hint.IsOpen = false; SetEmphasis(-1); return; }
        string? text = null;
        var index = -1;
        if (Kind == StatisticsChartKind.Donut)
        {
            var delta = point - Center; var distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            // The small hit margin also covers a lifted slice, avoiding hover oscillation at its edge.
            if (distance >= Radius * .67 - 7 && distance <= Radius + 7)
            {
                var angle = (Math.Atan2(delta.Y, delta.X) * 180 / Math.PI + 450) % 360; var position = 0.0;
                var rowIndex = 0;
                foreach (var row in Shares(view)) { position += row.Fraction * 360; if (angle < position) { index = rowIndex; text = row.Name + " · " + row.Share + "\n" + DisplayText.Duration(row.Value, true); break; } rowIndex++; }
            }
        }
        else if (Plot.Contains(point) && view.Buckets.Count > 0)
        {
            index = Math.Clamp((int)((point.X - Plot.Left) / Plot.Width * view.Buckets.Count), 0, view.Buckets.Count - 1);
            var bucket = view.Buckets[index];
            text = DisplayText.Time(bucket.Period.StartUtc, true) + " — " + DisplayText.Time(bucket.Period.EndUtc, true) + "\n"
                + Localizer.Get("前台使用时长") + " · " + DisplayText.Duration(bucket.Duration, true)
                + string.Concat(bucket.Apps.Take(3).Select(x => "\n" + x.AppName + " · " + DisplayText.Duration(x.Duration)));
        }
        SetEmphasis(index); AutomationProperties.SetItemStatus(this, text ?? string.Empty);
        if (text is null) { hint.IsOpen = false; return; }
        hintText.Text = text; hint.PlacementRect = new Rect(point, new Size(1, 1)); hint.IsOpen = true;
    }
    private void SetEmphasis(int index)
    {
        activeIndex = index;
        var count = view is null ? 0 : Kind == StatisticsChartKind.Donut ? Shares(view).Count : view.Buckets.Count;
        var values = new double[count]; if (index >= 0 && index < count) values[index] = 1;
        motion.Set(values, StatisticsMotion.Active(this) && StatisticsMotion.Enabled(this));
    }
    public void HideHint() { hover = null; hint.IsOpen = false; keyboardIndex = activeIndex = -1; motion.Clear(); AutomationProperties.SetItemStatus(this, string.Empty); InvalidateVisual(); }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); keyboardIndex = -1; hover = e.GetPosition(this); RefreshHint(); InvalidateVisual(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (view is null || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End or Key.Escape)) return;
        if (e.Key == Key.Escape) { HideHint(); e.Handled = true; return; }
        var count = Kind == StatisticsChartKind.Donut ? Shares(view).Count : view.Buckets.Count;
        if (count == 0) return;
        keyboardIndex = e.Key switch { Key.Home => 0, Key.End => count - 1, Key.Left => Math.Max(0, keyboardIndex - 1), _ => Math.Min(count - 1, keyboardIndex + 1) };
        if (Kind == StatisticsChartKind.Donut)
        {
            var rows = Shares(view); var angle = rows.Take(keyboardIndex).Sum(x => x.Fraction) * 360 + rows[keyboardIndex].Fraction * 180 - 90;
            hover = Polar(Center, Radius * .82, angle);
        }
        else hover = new Point(Plot.Left + Plot.Width / count * (keyboardIndex + .5), Plot.Center.Y);
        RefreshHint(); InvalidateVisual(); e.Handled = true;
    }
    protected override void OnLostFocus(RoutedEventArgs e) { HideHint(); base.OnLostFocus(e); }
    protected override void OnPointerExited(PointerEventArgs e) { hover = null; hint.IsOpen = false; keyboardIndex = -1; SetEmphasis(-1); AutomationProperties.SetItemStatus(this, string.Empty); base.OnPointerExited(e); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatisticsMotion.EnabledProperty) { motion?.Snap(); InvalidateVisual(); }
        if ((change.Property == StatisticsMotion.ActiveProperty && !StatisticsMotion.Active(this)) || (change.Property == IsVisibleProperty && !IsVisible)) HideHint();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) { owner.Deactivated += Deactivated; owner.PropertyChanged += OwnerChanged; }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        HideHint(); if (owner is not null) { owner.Deactivated -= Deactivated; owner.PropertyChanged -= OwnerChanged; owner = null; }
        base.OnDetachedFromVisualTree(e);
    }
    private void Deactivated(object? sender, EventArgs e) => HideHint();
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == IsVisibleProperty && owner?.IsVisible != true) HideHint(); }
}
