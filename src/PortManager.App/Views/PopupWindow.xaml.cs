using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PortManager.Views;

public partial class PopupWindow : Window
{
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosShowWindow = 0x0040;
    private bool _isModalDialogOpen;
    private bool _allowClose;

    public PopupWindow()
    {
        InitializeComponent();
    }

    internal bool DisableAutoHide { get; set; }

    public void ToggleNearTray()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowNearTray();
        }
    }

    public void ShowNearTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        UpdateLayout();
        PositionNearNotificationArea();
        Activate();
        Dispatcher.BeginInvoke(() => SearchTextBox.Focus(), DispatcherPriority.Input);
    }

    public void SetModalState(bool isOpen) => _isModalDialogOpen = isOpen;

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnDeactivated(object? sender, EventArgs eventArgs)
    {
        if (!DisableAutoHide && !_isModalDialogOpen && IsVisible)
        {
            Hide();
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == System.Windows.Input.Key.Escape)
        {
            Hide();
            eventArgs.Handled = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (_allowClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        Hide();
    }

    private void PositionNearNotificationArea()
    {
        var cursorPosition = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursorPosition);
        var workingArea = screen.WorkingArea;
        var handle = new WindowInteropHelper(this).Handle;

        if (!GetWindowRect(handle, out var currentBounds))
        {
            return;
        }

        const int margin = 10;
        var width = currentBounds.Right - currentBounds.Left;
        var height = currentBounds.Bottom - currentBounds.Top;
        var left = Math.Max(workingArea.Left + margin, workingArea.Right - width - margin);
        var top = Math.Max(workingArea.Top + margin, workingArea.Bottom - height - margin);

        SetWindowPos(
            handle,
            IntPtr.Zero,
            left,
            top,
            0,
            0,
            SetWindowPosNoSize | SetWindowPosNoZOrder | SetWindowPosShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
