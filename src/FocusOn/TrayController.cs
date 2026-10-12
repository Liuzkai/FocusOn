using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace FocusOn;

public sealed class TrayController : IDisposable
{
    private readonly TrayIcon icon;
    private readonly NativeMenuItem status = new("正在启动") { IsEnabled = false };
    private readonly NativeMenuItem pause, resume, openItem, exitItem;
    private string lastState = "正在启动";
    private bool isPaused;
    private bool disposed, checking;
    private readonly DispatcherTimer availabilityTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    public bool IsAvailable { get; private set; }
    public event Action<bool>? AvailabilityChanged;
    public TrayController(Action open, Action pause, Action resume, Action exit)
    {
        this.pause = Item("暂停记录", pause);
        this.resume = Item("继续记录", resume);
        this.resume.IsEnabled = false;
        var menu = new NativeMenu();
        menu.Items.Add(status); menu.Items.Add(new NativeMenuItemSeparator());
        openItem = Item("打开时间线", open); exitItem = Item("退出 FocusOn", exit);
        menu.Items.Add(openItem); menu.Items.Add(this.pause); menu.Items.Add(this.resume);
        menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exitItem);
        var image = OperatingSystem.IsMacOS() ? "focuson-template.png" : "focuson-tray.png";
        using var resource = AssetLoader.Open(new Uri("avares://FocusOn/Assets/" + image));
        icon = new TrayIcon { Icon = new WindowIcon(resource), Menu = menu, ToolTipText = "FocusOn", IsVisible = true };
        if (OperatingSystem.IsMacOS()) MacOSProperties.SetIsTemplateIcon(icon, true);
        icon.Clicked += (_, _) => open();
        RefreshLanguage();
        availabilityTimer.Tick += async (_, _) => await CheckAvailabilityAsync();
        availabilityTimer.Start();
    }
    public async Task CheckAvailabilityAsync()
    {
        if (disposed || checking) return;
        checking = true;
        try
        {
            var available = icon.NativeMenuExporter is not null && (!OperatingSystem.IsLinux() || await Task.Run(() => LinuxTraySupport.IsAvailable(Environment.ProcessId)));
            if (!disposed && available != IsAvailable) { IsAvailable = available; AvailabilityChanged?.Invoke(available); }
        }
        finally { checking = false; }
    }
    private static NativeMenuItem Item(string text, Action action)
    {
        var item = new NativeMenuItem(Localizer.Get(text)); item.Click += (_, _) => action(); return item;
    }
    public void Update(string state, bool paused)
    {
        lastState = state; isPaused = paused;
        status.Header = Localizer.Translate(state); icon.ToolTipText = "FocusOn · " + Localizer.Translate(state); pause.IsEnabled = !paused; resume.IsEnabled = paused;
    }
    public void RefreshLanguage()
    {
        openItem.Header = Localizer.Get("打开时间线"); exitItem.Header = Localizer.Get("退出 FocusOn");
        pause.Header = Localizer.Get("暂停记录"); resume.Header = Localizer.Get("继续记录"); Update(lastState, isPaused);
    }
    public void Dispose() { if (disposed) return; disposed = true; availabilityTimer.Stop(); icon.IsVisible = false; icon.Dispose(); IsAvailable = false; }
}
