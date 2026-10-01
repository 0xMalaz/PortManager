using Microsoft.Win32;

namespace PortManager.Services;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "PortManager";

    private readonly Func<string> _executablePathProvider;

    public StartupRegistrationService()
        : this(() => Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable."))
    {
    }

    internal StartupRegistrationService(Func<string> executablePathProvider)
    {
        _executablePathProvider = executablePathProvider;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);

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
        if (!IsEnabled)
        {
            return;
        }

        var expected = BuildCommand(_executablePathProvider());
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true);
        var current = key.GetValue(ValueName) as string;

        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
            key.SetValue(ValueName, expected, RegistryValueKind.String);
        }
    }

    internal static string BuildCommand(string executablePath) => $"\"{executablePath}\" --startup";
}

