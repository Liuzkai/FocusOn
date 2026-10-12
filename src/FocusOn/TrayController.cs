using Avalonia.Controls;
using Avalonia.Platform;

namespace FocusOn;

public sealed class TrayController : IDisposable
{
    private readonly TrayIcon icon;
    private readonly NativeMenuItem status = new("正在启动") { IsEnabled = false };
    private readonly NativeMenuItem pause, resume;
    public TrayController(Action open, Action pause, Action resume, Action exit)
    {
        this.pause = Item("暂停记录", pause);
        this.resume = Item("继续记录", resume);
        this.resume.IsEnabled = false;
        var menu = new NativeMenu();
        menu.Items.Add(status); menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item("打开时间线", open)); menu.Items.Add(this.pause); menu.Items.Add(this.resume);
        menu.Items.Add(new NativeMenuItemSeparator()); menu.Items.Add(Item("退出 FocusOn", exit));
        using var resource = AssetLoader.Open(new Uri("avares://FocusOn/Assets/focuson.png"));
        icon = new TrayIcon { Icon = new WindowIcon(resource), Menu = menu, ToolTipText = "FocusOn", IsVisible = true };
        icon.Clicked += (_, _) => open();
    }
    private static NativeMenuItem Item(string text, Action action)
    {
        var item = new NativeMenuItem(text); item.Click += (_, _) => action(); return item;
    }
    public void Update(string state, bool paused)
    {
        status.Header = state; icon.ToolTipText = "FocusOn · " + state; pause.IsEnabled = !paused; resume.IsEnabled = paused;
    }
    public void Dispose() { icon.IsVisible = false; icon.Dispose(); }
}
