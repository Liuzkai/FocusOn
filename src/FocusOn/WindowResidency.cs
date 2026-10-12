using Avalonia.Controls;

namespace FocusOn;

/// <summary>Keep the same window and recording session alive until explicit application exit.</summary>
public sealed class WindowResidency : IDisposable
{
    private readonly Window window;
    private readonly Action<bool> availabilityChanged;
    private bool available, exiting, disposed;
    public WindowResidency(Window window, Action<bool> availabilityChanged)
    {
        this.window = window; this.availabilityChanged = availabilityChanged;
        window.Closing += OnClosing;
    }
    public void SetTrayAvailable(bool value)
    {
        if (disposed || exiting) return;
        var previouslyAvailable = available;
        available = value; availabilityChanged(value);
        // Shell restart, extension removal or watcher loss must not strand a hidden window.
        if (previouslyAvailable && !value && !window.IsVisible) Restore();
    }
    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (exiting) return;
        args.Cancel = true;
        if (available) window.Hide();
        else Restore();
    }
    public void Restore()
    {
        if (disposed || exiting) return;
        window.Show(); window.WindowState = WindowState.Normal; window.Activate();
    }
    public void BeginExit() => exiting = true;
    public void Dispose() { if (disposed) return; disposed = true; window.Closing -= OnClosing; }
}
