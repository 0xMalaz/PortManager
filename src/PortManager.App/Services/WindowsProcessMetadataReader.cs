using System.ComponentModel;
using System.Diagnostics;
using PortManager.Native;

namespace PortManager.Services;

internal interface IProcessMetadataReader
{
    ProcessMetadata Read(int processId);
}

internal sealed class WindowsProcessMetadataReader : IProcessMetadataReader
{
    public ProcessMetadata Read(int processId)
    {
        if (processId == 0)
        {
            return new ProcessMetadata(processId, "System Idle Process", null, null);
        }

        if (processId == 4)
        {
            return new ProcessMetadata(processId, "System", null, null);
        }

        using var handle = NativeProcessInfo.OpenForQuery(processId);
        var executablePath = handle is null ? null : NativeProcessInfo.QueryImagePath(handle);
        var startTime = handle is null ? null : NativeProcessInfo.QueryStartTimeUtc(handle);
        var processName = executablePath is null
            ? ReadProcessNameFallback(processId)
            : NativeProcessInfo.GetProcessName(executablePath);

        return processName is null
            ? new ProcessMetadata(processId, $"PID {processId} (exited)", null, null)
            : new ProcessMetadata(processId, processName, executablePath, startTime);
    }

    // Processes that refuse even limited query access still expose their image name through the
    // system process list. This is slower, but only runs once per newly seen process.
    private static string? ReadProcessNameFallback(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                return process.ProcessName;
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                return $"PID {processId}";
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
