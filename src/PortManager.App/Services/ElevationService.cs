using System.ComponentModel;
using System.Diagnostics;
using PortManager.Infrastructure;

namespace PortManager.Services;

public interface IElevationService
{
    bool TryRestartAsAdministrator(out string? errorMessage);
}

public sealed class ElevationService : IElevationService
{
    public bool TryRestartAsAdministrator(out string? errorMessage)
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            errorMessage = Strings.ExecutablePathUnavailable;
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--elevated-restart --wait-for-pid {Environment.ProcessId}",
                UseShellExecute = true,
                Verb = "runas"
            });

            errorMessage = null;
            return true;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            errorMessage = null;
            return false;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            errorMessage = Strings.CouldNotRestartAsAdministrator(exception.Message);
            return false;
        }
    }
}

