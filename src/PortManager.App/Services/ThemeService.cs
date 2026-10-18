using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PortManager.Infrastructure;

namespace PortManager.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed class ThemeService : IDisposable
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    internal const string ThemeValueName = "Theme";
    private const string MoonGlyph = "";
    private const string SunGlyph = "";
    private readonly ResourceDictionary _resources;
    private readonly Dispatcher _dispatcher;
    private readonly string _preferencesPath;
    private readonly Func<bool> _systemIsLight;
    private ThemePreference _preference;
    private bool? _isLight;
    private bool _disposed;

    public ThemeService(System.Windows.Application application)
        : this(application.Resources, application.Dispatcher, StartupRegistrationService.PreferencesPath, IsSystemLightTheme)
    {
    }

    internal ThemeService(ResourceDictionary resources, Dispatcher dispatcher, string preferencesPath, Func<bool> systemIsLight)
    {
        _resources = resources;
        _dispatcher = dispatcher;
        _preferencesPath = preferencesPath;
        _systemIsLight = systemIsLight;
        _preference = LoadPreference(preferencesPath);
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

    /// <summary>Switches to the opposite of the visible theme and remembers the choice.</summary>
    public void Toggle()
    {
        _preference = _isLight == false ? ThemePreference.Light : ThemePreference.Dark;
        try
        {
            SavePreference(_preferencesPath, _preference);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Still switch for this session if the preference cannot be saved.
        }

        ApplyCurrentTheme();
    }

    internal static ThemePreference LoadPreference(string preferencesPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(preferencesPath, writable: false);
        return key?.GetValue(ThemeValueName) is string value &&
               Enum.TryParse<ThemePreference>(value, ignoreCase: true, out var preference) &&
               Enum.IsDefined(preference)
            ? preference
            : ThemePreference.System;
    }

    internal static void SavePreference(string preferencesPath, ThemePreference preference)
    {
        using var key = Registry.CurrentUser.CreateSubKey(preferencesPath, writable: true);
        key.SetValue(ThemeValueName, preference.ToString(), RegistryValueKind.String);
    }

    internal static bool ResolveIsLight(ThemePreference preference, Func<bool> systemIsLight) => preference switch
    {
        ThemePreference.Light => true,
        ThemePreference.Dark => false,
        _ => systemIsLight()
    };

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs)
    {
        _dispatcher.BeginInvoke(ApplyCurrentTheme);
    }

    private void ApplyCurrentTheme()
    {
        // UserPreferenceChanged fires for many unrelated settings; replacing the brushes forces a full redraw.
        var isLight = ResolveIsLight(_preference, _systemIsLight);
        if (_isLight == isLight)
        {
            return;
        }

        _isLight = isLight;

        _resources["ThemeToggleGlyph"] = isLight ? MoonGlyph : SunGlyph;
        _resources["ThemeToggleName"] = isLight ? Strings.SwitchToDarkTheme : Strings.SwitchToLightTheme;

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

    private static bool IsSystemLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private void SetBrush(string resourceKey, string colorValue)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorValue);
        _resources[resourceKey] = new SolidColorBrush(color);
    }
}
