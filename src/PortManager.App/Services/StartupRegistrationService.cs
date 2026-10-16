using Microsoft.Win32;

namespace PortManager.Services;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "PortManager";
    // User data is deliberately separate from Velopack's installation and uninstall keys.
    internal const string PreferencesPath = @"Software\0xMalaz\PortManager";

    private readonly Func<string> _executablePathProvider;
    private readonly string _registryPath;
    private readonly string _preferencesPath;

    public StartupRegistrationService()
        : this(() => Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable."))
    {
    }

    internal StartupRegistrationService(Func<string> executablePathProvider,
        string registryPath = RegistryPath, string preferencesPath = PreferencesPath)
    {
        _executablePathProvider = executablePathProvider;
        _registryPath = registryPath;
        _preferencesPath = preferencesPath;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var preferences = Registry.CurrentUser.CreateSubKey(_preferencesPath, writable: true);
        preferences.SetValue("StartWithWindows", enabled ? 1 : 0, RegistryValueKind.DWord);
        using var key = Registry.CurrentUser.CreateSubKey(_registryPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, BuildCommand(_executablePathProvider()), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public void EnsureCurrentExecutablePath()
    {
        using var preferences = Registry.CurrentUser.OpenSubKey(_preferencesPath, writable: false);
        var enabled = preferences?.GetValue("StartWithWindows") is int saved ? saved != 0 : IsEnabled;
        // Migrate the portable app's preference and restore it after reinstall.
        if (!enabled)
        {
            return;
        }

        SetEnabled(true);
    }

    internal static string BuildCommand(string executablePath) => $"\"{executablePath}\" --startup";

    internal static void RemoveRegistration(string registryPath = RegistryPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(registryPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
        // Preserve PreferencesPath so reinstall restores the user's choice.
    }
}

