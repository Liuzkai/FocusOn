using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace FocusOn;

/// <summary>Follow appended rows only while the user is at the log's end.</summary>
public sealed class LogEndFollower : IDisposable
{
    private readonly DataGrid grid;
    private readonly Func<object?> lastRow;
    private ScrollBar? scrollbar;
    private bool following, initialized, updatePending, callbackPending, disposed;

    public LogEndFollower(DataGrid grid, Func<object?> lastRow)
    {
        this.grid = grid; this.lastRow = lastRow;
        grid.TemplateApplied += TemplateApplied;
        grid.AddHandler(InputElement.PointerWheelChangedEvent, WheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        grid.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
    private bool AtEnd => scrollbar is not null && scrollbar.Maximum - scrollbar.Value <= 1;
    private void TemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        DetachScrollbar(); scrollbar = e.NameScope.Find<ScrollBar>("PART_VerticalScrollbar");
        if (scrollbar is not null) { scrollbar.PropertyChanged += ScrollbarChanged; scrollbar.Scroll += Scrolled; }
    }
    private void ScrollbarChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    { if (!updatePending && e.Property == RangeBase.ValueProperty) following = AtEnd; }
    private void Scrolled(object? sender, ScrollEventArgs e) => following = scrollbar is not null && scrollbar.Maximum - e.NewValue <= 1;
    private void WheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0) return;
        following = false;
        Dispatcher.UIThread.Post(() => { if (!disposed) following = AtEnd; }, DispatcherPriority.Loaded);
    }
    private void KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)) return;
        following = false;
        Dispatcher.UIThread.Post(() =>
        {
            if (!disposed) following = AtEnd && (grid.SelectedItem is null || grid.SelectedItem == lastRow());
        }, DispatcherPriority.Loaded);
    }
    public void Pause() => following = false;
    public void Reset() { following = false; initialized = false; }
    public void BeginUpdate() => updatePending = true;
    public void EndUpdate()
    {
        if (callbackPending || disposed) return;
        callbackPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (disposed) return;
                grid.UpdateLayout();
                if (!initialized && lastRow() is not null) { following = AtEnd; initialized = true; }
                if (following && lastRow() is { } row) { grid.ScrollIntoView(row, null); grid.UpdateLayout(); }
            }
            finally { updatePending = false; callbackPending = false; }
        }, DispatcherPriority.Loaded);
    }
    private void DetachScrollbar()
    { if (scrollbar is not null) { scrollbar.PropertyChanged -= ScrollbarChanged; scrollbar.Scroll -= Scrolled; scrollbar = null; } }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; DetachScrollbar(); grid.TemplateApplied -= TemplateApplied;
        grid.RemoveHandler(InputElement.PointerWheelChangedEvent, WheelChanged);
        grid.RemoveHandler(InputElement.KeyDownEvent, KeyDown);
    }
}
