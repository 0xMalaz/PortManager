using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PortManager.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _startWithWindowsItem;
    private readonly Icon _icon;
    private bool _disposed;

    public TrayIconService(bool startupEnabled)
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("Assets/PortManager.ico", UriKind.Relative));
        if (resource is null)
        {
            throw new InvalidOperationException("The Port Manager tray icon resource is missing.");
        }

        using (resource.Stream)
        using (var sourceIcon = new Icon(resource.Stream))
        {
            _icon = (Icon)sourceIcon.Clone();
        }

        _contextMenu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Open Port Manager");
        var refreshItem = new Forms.ToolStripMenuItem("Refresh");
        _startWithWindowsItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            Checked = startupEnabled,
            CheckOnClick = false
        };
        var exitItem = new Forms.ToolStripMenuItem("Exit");

        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        refreshItem.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _startWithWindowsItem.Click += (_, _) =>
            StartupToggleRequested?.Invoke(!_startWithWindowsItem.Checked);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(refreshItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(_startWithWindowsItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "Port Manager",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                ToggleRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? ToggleRequested;

    public event EventHandler? OpenRequested;

    public event EventHandler? RefreshRequested;

    public event Action<bool>? StartupToggleRequested;

    public event EventHandler? ExitRequested;

    public void SetStartupEnabled(bool enabled) => _startWithWindowsItem.Checked = enabled;

    public void SetListenerCount(int count)
    {
        _notifyIcon.Text = count == 1
            ? "Port Manager — 1 TCP listener"
            : $"Port Manager — {count} TCP listeners";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _icon.Dispose();
    }
}
