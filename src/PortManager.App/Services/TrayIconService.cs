using System.Drawing;
using System.Windows;
using PortManager.Infrastructure;
using Forms = System.Windows.Forms;

namespace PortManager.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly Forms.ToolStripMenuItem _updateItem;
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
        // Request the tray size so Windows uses the pixel-tuned 16/20/24 px frame instead of downscaling 32 px.
        using (var sourceIcon = new Icon(resource.Stream, Forms.SystemInformation.SmallIconSize))
        {
            _icon = (Icon)sourceIcon.Clone();
        }

        _contextMenu = new Forms.ContextMenuStrip();
        _contextMenu.Opening += (_, _) => MenuOpening?.Invoke(this, EventArgs.Empty);
        _updateItem = new Forms.ToolStripMenuItem(Strings.CheckForUpdates);
        _updateItem.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _startWithWindowsItem = new Forms.ToolStripMenuItem(Strings.StartWithWindows)
        {
            Checked = startupEnabled,
            CheckOnClick = false
        };
        var exitItem = new Forms.ToolStripMenuItem(Strings.Exit);

        _startWithWindowsItem.Click += (_, _) =>
            StartupToggleRequested?.Invoke(!_startWithWindowsItem.Checked);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(_updateItem);
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

    public event EventHandler? UpdateRequested;

    public event EventHandler? MenuOpening;

    public event Action<bool>? StartupToggleRequested;

    public event EventHandler? ExitRequested;

    public void SetStartupEnabled(bool enabled) => _startWithWindowsItem.Checked = enabled;

    public void SetAvailableUpdate(string? version) =>
        _updateItem.Text = version is null ? Strings.CheckForUpdates : Strings.InstallUpdate(version);

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
