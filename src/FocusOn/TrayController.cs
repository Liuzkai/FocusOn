using Forms = System.Windows.Forms;

namespace FocusOn;

public sealed class TrayController : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem status = new("正在启动") { Enabled = false };
    private readonly Forms.ToolStripMenuItem pause;
    private readonly Forms.ToolStripMenuItem resume;
    private bool disposed;

    public TrayController(Action open, Action pause, Action resume, Action exit)
    {
        this.pause = new Forms.ToolStripMenuItem("暂停记录(&P)", null, (_, _) => pause());
        this.resume = new Forms.ToolStripMenuItem("继续记录(&R)", null, (_, _) => resume()) { Enabled = false };
        menu.Items.Add(status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("打开时间线(&O)", null, (_, _) => open()));
        menu.Items.Add(this.pause);
        menu.Items.Add(this.resume);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("退出 FocusOn(&X)", null, (_, _) => exit()));
        icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "FocusOn · 正在启动",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => open();
    }

    public void Update(string actualStatus, bool manualPaused)
    {
        if (disposed) return;
        status.Text = actualStatus;
        var text = "FocusOn · " + actualStatus;
        icon.Text = text.Length > 63 ? text[..63] : text;
        pause.Enabled = !manualPaused;
        resume.Enabled = manualPaused;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }
}
