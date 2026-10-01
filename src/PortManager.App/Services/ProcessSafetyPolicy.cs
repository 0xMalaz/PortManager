namespace PortManager.Services;

internal static class ProcessSafetyPolicy
{
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Registry",
        "smss",
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "svchost",
        "dwm",
        "fontdrvhost",
        "sihost",
        "taskhostw",
        "explorer"
    };

    internal static (bool IsProtected, string? Reason) Evaluate(
        int processId,
        string processName,
        string? executablePath,
        int currentProcessId)
    {
        if (processId is 0 or 4)
        {
            return (true, "Windows owns this system listener.");
        }

        if (processId == currentProcessId)
        {
            return (true, "Port Manager cannot terminate itself.");
        }

        if (CriticalProcessNames.Contains(processName))
        {
            return (true, "This is a protected Windows process.");
        }

        if (IsInsideWindowsDirectory(executablePath))
        {
            return (true, "Processes inside the Windows system directory are read-only.");
        }

        return (false, null);
    }

    private static bool IsInsideWindowsDirectory(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var normalizedWindowsDirectory = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(windowsDirectory));
            var normalizedExecutablePath = System.IO.Path.GetFullPath(executablePath);

            return normalizedExecutablePath.StartsWith(
                normalizedWindowsDirectory + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or System.IO.PathTooLongException)
        {
            return false;
        }
    }
}
