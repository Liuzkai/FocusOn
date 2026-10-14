using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace FocusOn;

internal static class StatisticsMotion
{
    public static readonly AttachedProperty<bool> EnabledProperty = AvaloniaProperty.RegisterAttached<StatisticsPage, Control, bool>("HoverMotionEnabled", true, inherits: true);
    public static readonly AttachedProperty<bool> ActiveProperty = AvaloniaProperty.RegisterAttached<StatisticsPage, Control, bool>("HoverInteractionActive", true, inherits: true);
    public static bool Enabled(Control control) => control.GetValue(EnabledProperty) && !ReviewTheme.HighContrast;
    public static bool Active(Control control) => control.GetValue(ActiveProperty);
}

// One finite clock per chart/card; no idle polling or repeating animation.
internal sealed class HoverMotion
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Action changed;
    private double[] from = [], target = [];
    private long started;
    public double[] Values { get; private set; } = [];
    public HoverMotion(Action changed)
    {
        this.changed = changed;
        timer.Tick += (_, _) => Advance();
    }
    public void Set(double[] next, bool animate)
    {
        Advance();
        if (target.SequenceEqual(next)) { if (!animate) Snap(); return; }
        timer.Stop();
        if (Values.Length != next.Length) Values = new double[next.Length];
        from = Values.ToArray(); target = next; started = Stopwatch.GetTimestamp();
        if (animate && !Values.SequenceEqual(target)) timer.Start(); else Values = target.ToArray();
        changed();
    }
    private void Advance()
    {
        if (!timer.IsEnabled) return;
        var time = Math.Clamp(Stopwatch.GetElapsedTime(started).TotalMilliseconds / 170, 0, 1);
        var eased = 1 - Math.Pow(1 - time, 3);
        for (var i = 0; i < Values.Length; i++) Values[i] = from[i] + (target[i] - from[i]) * eased;
        if (time >= 1) timer.Stop();
        changed();
    }
    public void Snap() { timer.Stop(); Values = target.ToArray(); changed(); }
    public void Clear() { timer.Stop(); Values = from = target = []; changed(); }
}

public sealed class StatisticsCard : Border
{
    private readonly HoverMotion motion;
    private Window? owner;
    protected override Type StyleKeyOverride => typeof(Border);
    public StatisticsCard() { motion = new(UpdateAppearance); BorderThickness = new Thickness(1); UpdateAppearance(); }
    private void RefreshEmphasis()
    {
        if (!StatisticsMotion.Active(this)) { motion?.Clear(); return; }
        motion?.Set([IsPointerOver || IsKeyboardFocusWithin ? 1 : 0], StatisticsMotion.Enabled(this));
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty || change.Property == StatisticsMotion.EnabledProperty) RefreshEmphasis();
        if (change.Property == StatisticsMotion.ActiveProperty && !StatisticsMotion.Active(this)) motion?.Clear();
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); RefreshEmphasis(); }
    internal void UpdateAppearance()
    {
        var amount = motion.Values.FirstOrDefault();
        var surface = ((ISolidColorBrush)ReviewTheme.Surface).Color;
        var accent = ((ISolidColorBrush)ReviewTheme.Accent).Color;
        var mix = ReviewTheme.HighContrast ? 0 : .065 * amount;
        byte Blend(byte a, byte b) => (byte)(a + (b - a) * mix);
        Background = new SolidColorBrush(Color.FromRgb(Blend(surface.R, accent.R), Blend(surface.G, accent.G), Blend(surface.B, accent.B)));
        BorderBrush = new SolidColorBrush(Color.FromArgb((byte)((ReviewTheme.HighContrast ? 255 : 80) * amount), accent.R, accent.G, accent.B));
        BoxShadow = amount > .001 && !ReviewTheme.HighContrast
            ? new BoxShadows(new BoxShadow { OffsetY = 3 * amount, Blur = 12 * amount, Color = Color.FromArgb((byte)(30 * amount), 0, 0, 0) }) : default;
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null) { owner.Deactivated += Clear; owner.PropertyChanged += OwnerChanged; }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        motion.Clear(); if (owner is not null) { owner.Deactivated -= Clear; owner.PropertyChanged -= OwnerChanged; owner = null; }
        base.OnDetachedFromVisualTree(e);
    }
    private void Clear(object? sender, EventArgs e) => motion.Clear();
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == IsVisibleProperty && owner?.IsVisible != true) motion.Clear(); }
}
