using System.Drawing;
using System.Windows.Forms;
using WinLive.Core;

namespace WinLive.Windows;

public sealed class TrayCommandService : ITrayCommandService
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _visibilityItem;

    public TrayCommandService()
    {
        _visibilityItem = new ToolStripMenuItem("Island hidden")
        {
            Enabled = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_visibilityItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Reset position", null, (_, _) => ResetPositionRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WinLive",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
            {
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? OpenSettingsRequested;

    public event EventHandler? ResetPositionRequested;

    public event EventHandler? ExitRequested;

    public void PublishIslandVisibility(bool isVisible)
    {
        _visibilityItem.Text = isVisible ? "Island visible" : "Island hidden";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
