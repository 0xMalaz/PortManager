using System.Management;
using System.Runtime.InteropServices;

namespace PortManager.Services;

internal interface IProcessCommandLineReader
{
    IReadOnlyDictionary<int, string?> ReadCommandLines(IReadOnlyCollection<int> processIds);
}

internal sealed class WindowsProcessCommandLineReader : IProcessCommandLineReader
{
    public IReadOnlyDictionary<int, string?> ReadCommandLines(IReadOnlyCollection<int> processIds)
    {
        var ids = processIds
            .Where(processId => processId > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<int, string?>();
        }

        var whereClause = string.Join(" OR ", ids.Select(processId => $"ProcessId = {processId}"));
        var query = $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE {whereClause}";

        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            using var results = searcher.Get();
            var commandLines = new Dictionary<int, string?>();

            foreach (ManagementObject process in results)
            {
                using (process)
                {
                    var processId = Convert.ToInt32(process["ProcessId"]);
                    commandLines[processId] = process["CommandLine"] as string;
                }
            }

            return commandLines;
        }
        catch (Exception exception) when (
            exception is ManagementException or
            UnauthorizedAccessException or
            COMException or
            InvalidOperationException)
        {
            return new Dictionary<int, string?>();
        }
    }
}
