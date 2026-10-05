using PortManager.Models;
using PortManager.Native;

namespace PortManager.Services;

public sealed class WindowsPortSnapshotProvider : IPortSnapshotProvider
{
    private readonly ITcpTableReader _tableReader;
    private readonly int _currentProcessId;
    private readonly IProcessMetadataReader _metadataReader;
    private readonly IProcessCommandLineReader _commandLineReader;
    private readonly IPortServiceClassifier _classifier;
    private readonly PortActivityTracker _activityTracker;
    private readonly TimeProvider _timeProvider;
    private readonly object _stateLock = new();
    private readonly Dictionary<int, CachedProcess> _processCache = [];
    private readonly Dictionary<ProcessIdentity, string?> _commandLineCache = [];
    private readonly Dictionary<ProcessIdentity, (bool IsProtected, string? Reason)> _protectionCache = [];
    private readonly Dictionary<(ProcessIdentity Process, int Port), PortClassification> _classificationCache = [];
    private Dictionary<(int Port, int ProcessId), PortListener> _previousListeners = [];

    public WindowsPortSnapshotProvider()
        : this(
            new NativeTcpTableReader(),
            Environment.ProcessId,
            new WindowsProcessCommandLineReader(),
            new PortServiceClassifier(),
            new PortActivityTracker(),
            TimeProvider.System,
            new WindowsProcessMetadataReader())
    {
    }

    internal WindowsPortSnapshotProvider(
        ITcpTableReader tableReader,
        int currentProcessId,
        IProcessCommandLineReader? commandLineReader = null,
        IPortServiceClassifier? classifier = null,
        PortActivityTracker? activityTracker = null,
        TimeProvider? timeProvider = null,
        IProcessMetadataReader? metadataReader = null)
    {
        _tableReader = tableReader;
        _currentProcessId = currentProcessId;
        _metadataReader = metadataReader ?? new WindowsProcessMetadataReader();
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
        var listenerGroups = endpoints
            .Where(endpoint => endpoint.State == TcpEndpointState.Listen)
            .GroupBy(endpoint => (endpoint.Port, endpoint.ProcessId))
            .OrderBy(group => group.Key.Port)
            .ThenBy(group => group.Key.ProcessId)
            .ToArray();

        RefreshProcessCache(listenerGroups);

        var contexts = new List<ListenerContext>(listenerGroups.Length);
        foreach (var group in listenerGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = _processCache[group.Key.ProcessId].Metadata;
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
                group.Select(endpoint => endpoint.Address ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                metadata,
                identity,
                activityKey));
        }

        PopulateCommandLineCache(contexts);
        PruneIdentityCaches(contexts);

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
        var nextListeners = new Dictionary<(int Port, int ProcessId), PortListener>(contexts.Count);
        foreach (var context in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_protectionCache.TryGetValue(context.Identity, out var protection))
            {
                protection = ProcessSafetyPolicy.Evaluate(
                    context.ProcessId,
                    context.Metadata.ProcessName,
                    context.Metadata.ExecutablePath,
                    _currentProcessId);
                _protectionCache[context.Identity] = protection;
            }

            var classificationKey = (context.Identity, context.Port);
            if (!_classificationCache.TryGetValue(classificationKey, out var classification))
            {
                _commandLineCache.TryGetValue(context.Identity, out var commandLine);
                classification = _classifier.Classify(
                    context.Metadata with { CommandLine = commandLine },
                    context.Port);
                _classificationCache[classificationKey] = classification;
            }

            var listenerKey = (context.Port, context.ProcessId);
            _previousListeners.TryGetValue(listenerKey, out var previous);
            var addresses = previous is not null && previous.Addresses.SequenceEqual(context.Addresses)
                ? previous.Addresses
                : context.Addresses;

            var portActivity = activity[context.ActivityKey];
            var listener = new PortListener(
                context.Port,
                context.ProcessId,
                context.Metadata.ProcessName,
                context.Metadata.ExecutablePath,
                context.Metadata.StartTimeUtc,
                addresses,
                protection.IsProtected,
                protection.Reason,
                classification.ServiceName,
                classification.FrameworkName,
                classification.Category,
                portActivity.TimestampUtc,
                portActivity.Source);

            // Hand back the previous instance when nothing changed so the UI can skip unchanged rows.
            if (previous is not null && previous.Equals(listener))
            {
                listener = previous;
            }

            nextListeners[listenerKey] = listener;
            result.Add(listener);
        }

        _previousListeners = nextListeners;
        return result;
    }

    private void RefreshProcessCache(IReadOnlyCollection<IGrouping<(int Port, int ProcessId), RawTcpEndpoint>> listenerGroups)
    {
        var portsByProcess = listenerGroups
            .GroupBy(group => group.Key.ProcessId, group => group.Key.Port)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var processId in _processCache.Keys.Where(processId => !portsByProcess.ContainsKey(processId)).ToArray())
        {
            _processCache.Remove(processId);
        }

        foreach (var (processId, ports) in portsByProcess)
        {
            // Process details are only read when a PID first appears or its listening ports change.
            // A reused PID therefore gets fresh details as soon as its new owner's ports differ.
            if (_processCache.TryGetValue(processId, out var cached) && cached.Ports.SequenceEqual(ports))
            {
                continue;
            }

            _processCache[processId] = new CachedProcess(_metadataReader.Read(processId), ports);
        }
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

    private void PruneIdentityCaches(IReadOnlyCollection<ListenerContext> contexts)
    {
        var activeProcesses = contexts.Select(context => context.Identity).ToHashSet();
        foreach (var identity in _commandLineCache.Keys.Where(identity => !activeProcesses.Contains(identity)).ToArray())
        {
            _commandLineCache.Remove(identity);
        }

        foreach (var identity in _protectionCache.Keys.Where(identity => !activeProcesses.Contains(identity)).ToArray())
        {
            _protectionCache.Remove(identity);
        }

        var activeListeners = contexts.Select(context => (context.Identity, context.Port)).ToHashSet();
        foreach (var key in _classificationCache.Keys.Where(key => !activeListeners.Contains(key)).ToArray())
        {
            _classificationCache.Remove(key);
        }
    }

    private sealed record CachedProcess(ProcessMetadata Metadata, int[] Ports);

    private sealed record ListenerContext(
        int Port,
        int ProcessId,
        IReadOnlyList<string> Addresses,
        ProcessMetadata Metadata,
        ProcessIdentity Identity,
        PortActivityKey ActivityKey);
}
