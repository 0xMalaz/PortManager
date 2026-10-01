using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace PortManager.Services;

public sealed class ThemeService : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private readonly System.Windows.Application _application;
    private bool _disposed;

    public ThemeService(System.Windows.Application application)
    {
        _application = application;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        ApplyCurrentTheme();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs)
    {
        _application.Dispatcher.BeginInvoke(ApplyCurrentTheme);
    }

    private void ApplyCurrentTheme()
    {
        var isLight = IsLightTheme();

        SetBrush("WindowBackgroundBrush", isLight ? "#F5F7FB" : "#111318");
        SetBrush("SurfaceBrush", isLight ? "#FFFFFF" : "#1B1E25");
        SetBrush("SurfaceHoverBrush", isLight ? "#F1F4F9" : "#252933");
        SetBrush("TextPrimaryBrush", isLight ? "#172033" : "#F2F4F7");
        SetBrush("TextSecondaryBrush", isLight ? "#667085" : "#A7AFBE");
        SetBrush("BorderBrush", isLight ? "#E3E8F0" : "#303641");
        SetBrush("AccentBrush", isLight ? "#635BFF" : "#7C74FF");
        SetBrush("AccentHoverBrush", isLight ? "#5148E5" : "#918BFF");
        SetBrush("AccentSoftBrush", isLight ? "#EDEBFF" : "#242044");
        SetBrush("DangerBrush", isLight ? "#D92D20" : "#F04438");
        SetBrush("DangerHoverBrush", isLight ? "#B42318" : "#F97066");
        SetBrush("DangerSoftBrush", isLight ? "#FFF1F0" : "#211A22");
        SetBrush("MutedBrush", isLight ? "#98A2B3" : "#7A8494");
        SetBrush("SuccessBrush", isLight ? "#039855" : "#32D583");
    }

    private static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private void SetBrush(string resourceKey, string colorValue)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorValue);
        _application.Resources[resourceKey] = new SolidColorBrush(color);
    }
}
