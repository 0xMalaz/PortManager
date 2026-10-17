using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using PortManager.Infrastructure;
using PortManager.Native;
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
    private DispatcherTimer? _updatePollTimer;
    private bool _isExiting;
    private UpdateCoordinator? _updates;
    private UpdateWindow? _updateWindow;

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
        _trayIcon.ToggleRequested += (_, _) => Dispatcher.Invoke(TogglePopup);
        _trayIcon.StartupToggleRequested += OnStartupToggleRequested;
        _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(RequestShutdown);
        _updates = new UpdateCoordinator(new VelopackAppUpdater(), RequestShutdown);
        _updates.StatusChanged += status =>
        {
            _updateWindow?.Render(status);
            _trayIcon?.SetAvailableUpdate(status.Stage == UpdateStage.Available ? status.Version : null);
            UpdateEfficiencyMode();
        };
        _trayIcon.UpdateRequested += (_, _) => Dispatcher.Invoke(CheckForUpdates);
        _updatePollTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(8) };
        _updatePollTimer.Tick += async (_, _) => await _updates.CheckInBackgroundAsync();
        _updatePollTimer.Start();
        _trayIcon.MenuOpening += (_, _) =>
        {
            if (_updates.Status.Stage != UpdateStage.Available)
                _ = _updates.CheckInBackgroundAsync(TimeSpan.FromMinutes(15));
        };

        _singleInstance.ActivationRequested += (_, _) => Dispatcher.BeginInvoke(ShowPopup);
        _viewModel.IsViewVisible = false;
        _popupWindow.IsVisibleChanged += (_, _) => OnPopupVisibilityChanged();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (_, _) => await _viewModel.RefreshInBackgroundAsync();
        _refreshTimer.Start();

        await _viewModel.RefreshAsync();
        _ = _updates.CheckInBackgroundAsync();

        if (!eventArgs.Args.Contains("--startup", StringComparer.OrdinalIgnoreCase))
        {
            ShowPopup();
        }
        else
        {
            UpdateEfficiencyMode();
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        _updates?.Dispose();
        _updateWindow?.Close();
        _refreshTimer?.Stop();
        _updatePollTimer?.Stop();
        _trayIcon?.Dispose();
        _popupWindow?.CloseForExit();
        _themeService?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }

    private async void CheckForUpdates()
    {
        if (_isExiting || _updates is null) return;
        if (_updateWindow is null)
        {
            _updateWindow = new UpdateWindow();
            _updateWindow.Closed += (_, _) => _updateWindow = null;
        }
        _updateWindow.Render(_updates.Status);
        _updateWindow.Show();
        _updateWindow.Activate();
        await _updates.RunAsync();
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
                Strings.CouldNotUpdateStartup(exception.Message),
                "Port Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowPopup()
    {
        // Leave efficiency mode before the window renders so opening the popup stays instant.
        ProcessPowerThrottling.SetEfficiencyMode(false);
        _popupWindow?.ShowNearTray();
    }

    private void TogglePopup()
    {
        if (_popupWindow is null)
        {
            return;
        }

        if (_popupWindow.IsVisible)
        {
            _popupWindow.Hide();
        }
        else
        {
            ShowPopup();
        }
    }

    private void OnPopupVisibilityChanged()
    {
        if (_refreshTimer is null || _popupWindow is null || _viewModel is null)
        {
            return;
        }

        var isVisible = _popupWindow.IsVisible;
        _viewModel.IsViewVisible = isVisible;
        UpdateEfficiencyMode();

        _refreshTimer.Interval = isVisible
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(5);

        if (isVisible)
        {
            _ = _viewModel.RefreshInBackgroundAsync();
        }
    }

    private void UpdateEfficiencyMode()
    {
        var updating = _updates?.Status.Stage is UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Restarting;
        ProcessPowerThrottling.SetEfficiencyMode(_popupWindow?.IsVisible != true && !updating);
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
