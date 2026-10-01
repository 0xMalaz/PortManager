using PortManager.Models;

namespace PortManager.Services;

internal readonly record struct PortActivityKey(
    int Port,
    int ProcessId,
    DateTimeOffset? ProcessStartTimeUtc,
    string ProcessName);

internal sealed record PortActivitySnapshot(
    DateTimeOffset TimestampUtc,
    PortActivitySource Source);

internal sealed class PortActivityTracker
{
    private Dictionary<PortActivityKey, PortActivitySnapshot> _activity = [];

    internal IReadOnlyDictionary<PortActivityKey, PortActivitySnapshot> Update(
        IReadOnlyCollection<(PortActivityKey Key, DateTimeOffset? ProcessStartTimeUtc)> listeners,
        IReadOnlySet<(int Port, int ProcessId)> connectedPorts,
        DateTimeOffset observedAtUtc)
    {
        var next = new Dictionary<PortActivityKey, PortActivitySnapshot>(listeners.Count);

        foreach (var listener in listeners)
        {
            var hasConnection = connectedPorts.Contains((listener.Key.Port, listener.Key.ProcessId));
            if (_activity.TryGetValue(listener.Key, out var existing))
            {
                next[listener.Key] = hasConnection
                    ? new PortActivitySnapshot(observedAtUtc, PortActivitySource.ObservedConnection)
                    : existing;
                continue;
            }

            next[listener.Key] = hasConnection
                ? new PortActivitySnapshot(observedAtUtc, PortActivitySource.ObservedConnection)
                : listener.ProcessStartTimeUtc.HasValue
                    ? new PortActivitySnapshot(listener.ProcessStartTimeUtc.Value, PortActivitySource.ProcessStart)
                    : new PortActivitySnapshot(observedAtUtc, PortActivitySource.FirstSeen);
        }

        _activity = next;
        return next;
    }
}
