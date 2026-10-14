using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Media.Imaging;

namespace FocusOn;

public sealed class TrayController : IDisposable
{
    private readonly TrayIcon icon;
    private readonly NativeMenuItem timerItem;
    private TitleTimerState? timerState;
    private readonly WindowIcon defaultIcon;
    private int lastIconStep = -1;
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
        timerItem = Item("计时组件", open); timerItem.IsEnabled = false;
        menu.Items.Add(status); menu.Items.Add(timerItem); menu.Items.Add(new NativeMenuItemSeparator());
        openItem = Item("打开时间线", open); exitItem = Item("退出 FocusOn", exit);
        menu.Items.Add(openItem); menu.Items.Add(this.pause); menu.Items.Add(this.resume);
        menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(exitItem);
        var image = OperatingSystem.IsMacOS() ? "focuson-template.png" : "focuson-tray.png";
        using var resource = AssetLoader.Open(new Uri("avares://FocusOn/Assets/" + image));
        defaultIcon = new WindowIcon(resource);
        icon = new TrayIcon { Icon = defaultIcon, Menu = menu, ToolTipText = "FocusOn", IsVisible = true };
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
        status.Header = Localizer.Translate(state); RefreshTimerText(); pause.IsEnabled = !paused; resume.IsEnabled = paused;
    }
    private void RefreshTimerText()
    {
        timerItem.IsEnabled = timerState is not null;
        timerItem.Header = timerState is { } state ? TimerPresentation.Summary(state) + " · " + state.Title : Localizer.Get("计时组件");
        icon.ToolTipText = timerState is { } timer ? "FocusOn · " + TimerPresentation.Summary(timer) + "\n" + timer.Title[..Math.Min(timer.Title.Length, 70)] : "FocusOn · " + Localizer.Translate(lastState);
    }
    public void UpdateTimer(TitleTimerState? state)
    {
        if (disposed) return;
        timerState = state; RefreshTimerText();
        var step = state is null ? -1 : state.IsCountdown ? (int)(state.Progress * 60) : (int)(state.Duration.TotalMinutes % 60) + 100;
        if (state is { IsRunning: false }) step += 1000;
        if (state is { IsCompleted: true }) step += 2000;
        if (step == lastIconStep) return;
        lastIconStep = step;
        if (state is null) { icon.Icon = defaultIcon; return; }
        var dial = new TimerDial { Width = 32, Height = 32, State = state, Template = OperatingSystem.IsMacOS() };
        dial.Measure(new Size(32, 32)); dial.Arrange(new Rect(0, 0, 32, 32));
        using var bitmap = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(96, 96)); bitmap.Render(dial);
        using var png = new MemoryStream(); bitmap.Save(png); png.Position = 0; icon.Icon = new WindowIcon(png);
    }
    public void RefreshLanguage()
    {
        openItem.Header = Localizer.Get("打开时间线"); exitItem.Header = Localizer.Get("退出 FocusOn");
        pause.Header = Localizer.Get("暂停记录"); resume.Header = Localizer.Get("继续记录"); Update(lastState, isPaused);
    }
    public void Dispose() { if (disposed) return; disposed = true; availabilityTimer.Stop(); icon.IsVisible = false; icon.Dispose(); IsAvailable = false; }
}
