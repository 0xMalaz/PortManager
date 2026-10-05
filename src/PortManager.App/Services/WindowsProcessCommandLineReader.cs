using PortManager.Native;

namespace PortManager.Services;

internal interface IProcessCommandLineReader
{
    IReadOnlyDictionary<int, string?> ReadCommandLines(IReadOnlyCollection<int> processIds);
}

internal sealed class WindowsProcessCommandLineReader : IProcessCommandLineReader
{
    public IReadOnlyDictionary<int, string?> ReadCommandLines(IReadOnlyCollection<int> processIds)
    {
        var commandLines = new Dictionary<int, string?>();

        foreach (var processId in processIds.Where(processId => processId > 0).Distinct())
        {
            using var handle = NativeProcessInfo.OpenForQuery(processId);
            commandLines[processId] = handle is null ? null : NativeProcessInfo.QueryCommandLine(handle);
        }

        return commandLines;
    }
}
