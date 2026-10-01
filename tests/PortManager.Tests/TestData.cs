using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

internal static class TestData
{
    internal static PortListener Listener(
        int port = 3000,
        int processId = 1234,
        string processName = "node",
        DateTimeOffset? startTime = null,
        bool isProtected = false,
        string? protectionReason = null,
        string serviceName = "Node Server",
        string? frameworkName = "Node",
        PortCategory category = PortCategory.Dev,
        DateTimeOffset? lastActive = null,
        PortActivitySource activitySource = PortActivitySource.ProcessStart,
        params string[] addresses) =>
        new(
            port,
            processId,
            processName,
            @"C:\Tools\node.exe",
            startTime ?? new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero),
            addresses.Length == 0 ? ["127.0.0.1"] : addresses,
            isProtected,
            protectionReason,
            serviceName,
            frameworkName,
            category,
            lastActive ?? startTime ?? new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero),
            activitySource);
}

internal sealed class MutableSnapshotProvider(IReadOnlyList<PortListener> snapshot) : IPortSnapshotProvider
{
    public IReadOnlyList<PortListener> Snapshot { get; set; } = snapshot;

    public Task<IReadOnlyList<PortListener>> GetListenersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Snapshot);
}
