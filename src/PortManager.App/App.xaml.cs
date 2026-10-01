using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using PortManager.Services;
using PortManager.ViewModels;
using PortManager.Views;

namespace PortManager;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ThemeService? _themeService;
    private TrayIconService? _trayIcon;
    private StartupRegistrationService? _startupRegistration;
    private PopupWindow? _popupWindow;
    private PopupWindowViewModel? _viewModel;
    private DispatcherTimer? _refreshTimer;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        WaitForPreviousInstanceIfRequested(eventArgs.Args);

        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            Shutdown();
            return;
        }

        _themeService = new ThemeService(this);
        _startupRegistration = new StartupRegistrationService();
        _startupRegistration.EnsureCurrentExecutablePath();

        var snapshotProvider = new WindowsPortSnapshotProvider();
        var terminationService = new ProcessTerminationService(snapshotProvider);
        var elevationService = new ElevationService();
        var dialogService = new WpfUserDialogService(
            () => _popupWindow,
            isOpen => _popupWindow?.SetModalState(isOpen));

        _viewModel = new PopupWindowViewModel(
            snapshotProvider,
            terminationService,
            dialogService,
            elevationService,
            RequestShutdown);

        _popupWindow = new PopupWindow
        {
            DataContext = _viewModel
        };

#if DEBUG
        var executableName = System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        if (executableName?.EndsWith(".UiTest", StringComparison.OrdinalIgnoreCase) == true)
        {
            _popupWindow.ShowInTaskbar = true;
            _popupWindow.DisableAutoHide = true;
        }
#endif

        _trayIcon = new TrayIconService(_startupRegistration.IsEnabled);
        _trayIcon.ToggleRequested += (_, _) => Dispatcher.Invoke(_popupWindow.ToggleNearTray);
        _trayIcon.OpenRequested += (_, _) => Dispatcher.Invoke(_popupWindow.ShowNearTray);
        _trayIcon.RefreshRequested += (_, _) => Dispatcher.Invoke(() => _ = _viewModel.RefreshAsync());
        _trayIcon.StartupToggleRequested += OnStartupToggleRequested;
        _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(RequestShutdown);

        _singleInstance.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(_popupWindow.ShowNearTray);
        _viewModel.PropertyChanged += (_, propertyChanged) =>
        {
            if (propertyChanged.PropertyName == nameof(PopupWindowViewModel.ListenerCount))
            {
                _trayIcon.SetListenerCount(_viewModel.ListenerCount);
            }
        };

        _popupWindow.IsVisibleChanged += (_, _) => UpdateRefreshInterval();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshInBackgroundAsync();
        _refreshTimer.Start();

        await _viewModel.RefreshAsync();

        if (!eventArgs.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            _popupWindow.ShowNearTray();
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _refreshTimer?.Stop();
        _trayIcon?.Dispose();
        _popupWindow?.CloseForExit();
        _themeService?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }

    private void OnStartupToggleRequested(bool shouldEnable)
    {
        if (_startupRegistration is null || _trayIcon is null)
        {
            return;
        }

        try
        {
            _startupRegistration.SetEnabled(shouldEnable);
            _trayIcon.SetStartupEnabled(_startupRegistration.IsEnabled);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                _popupWindow,
                $"Could not update Windows startup: {exception.Message}",
                "Port Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateRefreshInterval()
    {
        if (_refreshTimer is null || _popupWindow is null || _viewModel is null)
        {
            return;
        }

        _refreshTimer.Interval = _popupWindow.IsVisible
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(5);

        if (_popupWindow.IsVisible)
        {
            _ = _viewModel.RefreshInBackgroundAsync();
        }
    }

    private void RequestShutdown()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Shutdown();
    }

    private static void WaitForPreviousInstanceIfRequested(string[] arguments)
    {
        var waitArgumentIndex = Array.FindIndex(
            arguments,
            argument => string.Equals(argument, "--wait-for-pid", StringComparison.OrdinalIgnoreCase));

        if (waitArgumentIndex < 0 ||
            waitArgumentIndex + 1 >= arguments.Length ||
            !int.TryParse(arguments[waitArgumentIndex + 1], out var processId))
        {
            return;
        }

        try
        {
            using var previousProcess = Process.GetProcessById(processId);
            previousProcess.WaitForExit(milliseconds: 10_000);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // The previous instance has already exited or cannot be queried.
        }
    }
}
