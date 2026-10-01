using System.Diagnostics;
using PortManager.Models;
using PortManager.Native;

namespace PortManager.Services;

public sealed class WindowsPortSnapshotProvider : IPortSnapshotProvider
{
    private readonly ITcpTableReader _tableReader;
    private readonly int _currentProcessId;
    private readonly IProcessCommandLineReader _commandLineReader;
    private readonly IPortServiceClassifier _classifier;
    private readonly PortActivityTracker _activityTracker;
    private readonly TimeProvider _timeProvider;
    private readonly object _stateLock = new();
    private readonly Dictionary<ProcessIdentity, string?> _commandLineCache = [];
    private readonly Dictionary<(ProcessIdentity Process, int Port), PortClassification> _classificationCache = [];

    public WindowsPortSnapshotProvider()
        : this(
            new NativeTcpTableReader(),
            Environment.ProcessId,
            new WindowsProcessCommandLineReader(),
            new PortServiceClassifier(),
            new PortActivityTracker(),
            TimeProvider.System)
    {
    }

    internal WindowsPortSnapshotProvider(
        ITcpTableReader tableReader,
        int currentProcessId,
        IProcessCommandLineReader? commandLineReader = null,
        IPortServiceClassifier? classifier = null,
        PortActivityTracker? activityTracker = null,
        TimeProvider? timeProvider = null)
    {
        _tableReader = tableReader;
        _currentProcessId = currentProcessId;
        _commandLineReader = commandLineReader ?? new WindowsProcessCommandLineReader();
        _classifier = classifier ?? new PortServiceClassifier();
        _activityTracker = activityTracker ?? new PortActivityTracker();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<PortListener>> GetListenersAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            lock (_stateLock)
            {
                return BuildSnapshot(cancellationToken);
            }
        }, cancellationToken);

    private IReadOnlyList<PortListener> BuildSnapshot(CancellationToken cancellationToken)
    {
        var endpoints = _tableReader.ReadEndpoints();
        var metadataByProcess = new Dictionary<int, ProcessMetadata>();
        var contexts = new List<ListenerContext>();

        foreach (var group in endpoints
                     .Where(endpoint => endpoint.State == TcpEndpointState.Listen)
                     .GroupBy(endpoint => (endpoint.Port, endpoint.ProcessId))
                     .OrderBy(group => group.Key.Port)
                     .ThenBy(group => group.Key.ProcessId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!metadataByProcess.TryGetValue(group.Key.ProcessId, out var metadata))
            {
                metadata = ReadProcessMetadata(group.Key.ProcessId);
                metadataByProcess[group.Key.ProcessId] = metadata;
            }

            var identity = new ProcessIdentity(
                metadata.ProcessId,
                metadata.StartTimeUtc,
                metadata.ProcessName,
                metadata.ExecutablePath);
            var activityKey = new PortActivityKey(
                group.Key.Port,
                group.Key.ProcessId,
                metadata.StartTimeUtc,
                metadata.ProcessName);

            contexts.Add(new ListenerContext(
                group.Key.Port,
                group.Key.ProcessId,
                group.Select(endpoint => endpoint.Address)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                metadata,
                identity,
                activityKey));
        }

        PopulateCommandLineCache(contexts);
        PruneClassificationCaches(contexts);

        var connectedPorts = endpoints
            .Where(endpoint => endpoint.State is not TcpEndpointState.Listen and
                               not TcpEndpointState.Closed and
                               not TcpEndpointState.DeleteTcb)
            .Select(endpoint => (endpoint.Port, endpoint.ProcessId))
            .ToHashSet();
        var observedAtUtc = _timeProvider.GetUtcNow();
        var activity = _activityTracker.Update(
            contexts.Select(context => (context.ActivityKey, context.Metadata.StartTimeUtc)).ToArray(),
            connectedPorts,
            observedAtUtc);

        var result = new List<PortListener>(contexts.Count);
        foreach (var context in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var protection = ProcessSafetyPolicy.Evaluate(
                context.ProcessId,
                context.Metadata.ProcessName,
                context.Metadata.ExecutablePath,
                _currentProcessId);
            var classificationKey = (context.Identity, context.Port);
            if (!_classificationCache.TryGetValue(classificationKey, out var classification))
            {
                _commandLineCache.TryGetValue(context.Identity, out var commandLine);
                classification = _classifier.Classify(
                    context.Metadata with { CommandLine = commandLine },
                    context.Port);
                _classificationCache[classificationKey] = classification;
            }

            var portActivity = activity[context.ActivityKey];
            result.Add(new PortListener(
                context.Port,
                context.ProcessId,
                context.Metadata.ProcessName,
                context.Metadata.ExecutablePath,
                context.Metadata.StartTimeUtc,
                context.Addresses,
                protection.IsProtected,
                protection.Reason,
                classification.ServiceName,
                classification.FrameworkName,
                classification.Category,
                portActivity.TimestampUtc,
                portActivity.Source));
        }

        return result;
    }

    private void PopulateCommandLineCache(IReadOnlyCollection<ListenerContext> contexts)
    {
        var missingIdentities = contexts
            .Select(context => context.Identity)
            .Distinct()
            .Where(identity => !_commandLineCache.ContainsKey(identity))
            .ToArray();

        if (missingIdentities.Length == 0)
        {
            return;
        }

        var commandLines = _commandLineReader.ReadCommandLines(
            missingIdentities.Select(identity => identity.ProcessId).Distinct().ToArray());

        foreach (var identity in missingIdentities)
        {
            commandLines.TryGetValue(identity.ProcessId, out var commandLine);
            _commandLineCache[identity] = commandLine;
        }
    }

    private void PruneClassificationCaches(IReadOnlyCollection<ListenerContext> contexts)
    {
        var activeProcesses = contexts.Select(context => context.Identity).ToHashSet();
        foreach (var identity in _commandLineCache.Keys.Where(identity => !activeProcesses.Contains(identity)).ToArray())
        {
            _commandLineCache.Remove(identity);
        }

        var activeListeners = contexts.Select(context => (context.Identity, context.Port)).ToHashSet();
        foreach (var key in _classificationCache.Keys.Where(key => !activeListeners.Contains(key)).ToArray())
        {
            _classificationCache.Remove(key);
        }
    }

    private static ProcessMetadata ReadProcessMetadata(int processId)
    {
        if (processId == 0)
        {
            return new ProcessMetadata(processId, "System Idle Process", null, null);
        }

        if (processId == 4)
        {
            return new ProcessMetadata(processId, "System", null, null);
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            var processName = SafeRead(() => process.ProcessName) ?? $"PID {processId}";
            var executablePath = SafeRead(() => process.MainModule?.FileName);
            var startTime = SafeReadProcessStartTime(process);

            return new ProcessMetadata(processId, processName, executablePath, startTime);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new ProcessMetadata(processId, $"PID {processId} (exited)", null, null);
        }
    }

    private static T? SafeRead<T>(Func<T?> reader)
    {
        try
        {
            return reader();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return default;
        }
    }

    private static DateTimeOffset? SafeReadProcessStartTime(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record ListenerContext(
        int Port,
        int ProcessId,
        IReadOnlyList<string> Addresses,
        ProcessMetadata Metadata,
        ProcessIdentity Identity,
        PortActivityKey ActivityKey);
}
