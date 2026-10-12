using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FocusOn;

// The full app strip is a drag target; the small footer indicates position.
public sealed class AppTotalsScrollViewer : ScrollViewer
{
    private readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(1) };
    private IPointer? captured;
    private Point origin;
    private double originOffset;
    private bool dragging, draggingThumb;
    private Cursor? savedCursor;
    private Window? owner;
    private Point? lastMotion;
    private static readonly Cursor DragCursor = new(StandardCursorType.SizeWestEast);
    protected override Type StyleKeyOverride => typeof(ScrollViewer);
    private double MaximumOffset => Math.Max(0, Extent.Width - Viewport.Width);
    public bool IsIndicatorVisible { get; private set; }
    public Rect IndicatorBounds
    {
        get
        {
            var width = Bounds.Width;
            if (width <= 0 || MaximumOffset <= 0) return default;
            var thumb = Math.Clamp(width * Viewport.Width / Extent.Width, Math.Min(24, width), width);
            return new(Math.Clamp(Offset.X / MaximumOffset, 0, 1) * (width - thumb), Math.Max(0, Bounds.Height - 4), thumb, 3);
        }
    }

    public AppTotalsScrollViewer()
    {
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        Focusable = true;
        // Handle both axes before the inner presenter consumes horizontal trackpad input.
        AddHandler(PointerWheelChangedEvent, ScrollWheel, RoutingStrategies.Tunnel);
        idle.Tick += (_, _) => { if (captured is null) HideIndicator(); };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Presenter padding is excluded from its scrollable measurement; margin reserves
        // actual footer space without compressing the app chips or their padding.
        if (e.NameScope.Find<Control>("PART_ContentPresenter") is { } presenter) presenter.Margin = new(0, 0, 0, 6);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        // Keep the whole reserved footer interactive even while the line is hidden.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (IsIndicatorVisible && MaximumOffset > 0)
            context.DrawRectangle(ReviewTheme.Muted, null, IndicatorBounds, 1.5, 1.5);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Pointer.Type != PointerType.Mouse || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || MaximumOffset <= 0) return;
        // Preserve any future interactive chip content rather than stealing its click.
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button or TextBox) return;
        Focus(); origin = e.GetPosition(this); originOffset = Offset.X; savedCursor = Cursor;
        draggingThumb = origin.Y >= Bounds.Height - 6;
        if (draggingThumb && !IndicatorBounds.Contains(origin))
        {
            var bounds = IndicatorBounds;
            ScrollTo((origin.X - bounds.Width / 2) * MaximumOffset / Math.Max(1, Bounds.Width - bounds.Width));
            originOffset = Offset.X;
        }
        captured = e.Pointer; captured.Capture(this); RevealIndicator(); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (MaximumOffset <= 0) return;
        var point = e.GetPosition(this);
        if (point != lastMotion) RevealIndicator();
        lastMotion = point;
        if (captured != e.Pointer) return;
        var delta = point.X - origin.X;
        if (!dragging && Math.Abs(delta) < 4) return;
        dragging = true; Cursor = DragCursor;
        ScrollTo(originOffset + (draggingThumb ? delta * MaximumOffset / Math.Max(1, Bounds.Width - IndicatorBounds.Width) : -delta));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (captured != e.Pointer) return;
        CancelDrag(); RevealIndicator(); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e); CancelDrag(); HideIndicator();
    }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); lastMotion = null; }
    private void ScrollWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || MaximumOffset <= 0) return;
        var delta = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
        if (delta == 0) return;
        var previous = Offset.X; ScrollTo(previous - delta * 48);
        if (captured is not null) originOffset += Offset.X - previous;
        RevealIndicator(); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && captured is not null) { CancelDrag(); HideIndicator(); e.Handled = true; return; }
        if (MaximumOffset > 0 && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown)
        {
            ScrollTo(e.Key switch { Key.Left => Offset.X - 48, Key.Right => Offset.X + 48, Key.Home => 0,
                Key.End => MaximumOffset, Key.PageUp => Offset.X - Viewport.Width, _ => Offset.X + Viewport.Width });
            RevealIndicator(); e.Handled = true; return;
        }
        base.OnKeyDown(e);
        if (e.Handled && MaximumOffset > 0) RevealIndicator();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OffsetProperty || change.Property == ExtentProperty || change.Property == ViewportProperty)
        {
            InvalidateVisual();
            if (MaximumOffset <= 0) { CancelDrag(); HideIndicator(); }
        }
        if (change.Property == IsVisibleProperty && !IsVisible) CancelInteraction();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;
        owner.Deactivated += OwnerDeactivated; owner.PropertyChanged += OwnerChanged;
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelInteraction();
        if (owner is not null) { owner.Deactivated -= OwnerDeactivated; owner.PropertyChanged -= OwnerChanged; owner = null; }
        base.OnDetachedFromVisualTree(e);
    }
    private void OwnerDeactivated(object? sender, EventArgs e) => CancelInteraction();
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    { if (e.Property == IsVisibleProperty && owner?.IsVisible == false) CancelInteraction(); }
    private void ScrollTo(double offset) => Offset = new(Math.Clamp(offset, 0, MaximumOffset), 0);
    private void RevealIndicator()
    {
        if (MaximumOffset <= 0 || !IsVisible || owner?.IsVisible == false) return;
        IsIndicatorVisible = true; InvalidateVisual(); idle.Stop(); idle.Start();
    }
    private void HideIndicator() { idle.Stop(); IsIndicatorVisible = false; InvalidateVisual(); }
    private void CancelInteraction() { CancelDrag(); HideIndicator(); lastMotion = null; }
    private void CancelDrag()
    {
        var pointer = captured; captured = null;
        if (dragging) Cursor = savedCursor;
        dragging = draggingThumb = false;
        if (pointer?.Captured == this) pointer.Capture(null);
    }
}
