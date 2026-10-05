using PortManager.Native;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class WindowsPortSnapshotProviderTests
{
    [TestMethod]
    public async Task GetListeners_groups_addresses_and_sorts_ports()
    {
        var processId = Environment.ProcessId;
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(8000, processId, "::1", TcpEndpointState.Listen),
            new RawTcpEndpoint(3000, processId, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(8000, processId, "127.0.0.1", TcpEndpointState.Listen)
        ]);
        var provider = new WindowsPortSnapshotProvider(reader, currentProcessId: -1);

        var listeners = await provider.GetListenersAsync();

        Assert.HasCount(2, listeners);
        Assert.AreEqual(3000, listeners[0].Port);
        Assert.AreEqual(8000, listeners[1].Port);
        Assert.AreEqual("localhost", listeners[1].AddressDisplay);
        Assert.HasCount(2, listeners[1].Addresses);
    }

    [TestMethod]
    public async Task GetListeners_tracks_connections_and_caches_process_classification()
    {
        var processId = Environment.ProcessId;
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(5173, processId, "127.0.0.1", TcpEndpointState.Listen)
        ]);
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var commandLines = new CountingCommandLineReader("node vite");
        var classifier = new CountingClassifier();
        var provider = new WindowsPortSnapshotProvider(
            reader,
            currentProcessId: -1,
            commandLines,
            classifier,
            new PortActivityTracker(),
            timeProvider);

        var initial = await provider.GetListenersAsync();
        timeProvider.UtcNow = now.AddMinutes(5);
        reader.Endpoints =
        [
            new RawTcpEndpoint(5173, processId, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(5173, processId, "127.0.0.1", TcpEndpointState.Established)
        ];
        var connected = await provider.GetListenersAsync();
        timeProvider.UtcNow = now.AddMinutes(7);
        reader.Endpoints =
        [
            new RawTcpEndpoint(5173, processId, "127.0.0.1", TcpEndpointState.Listen)
        ];
        var afterConnection = await provider.GetListenersAsync();

        Assert.AreEqual(PortActivitySource.ProcessStart, initial[0].LastActivitySource);
        Assert.AreEqual(PortActivitySource.ObservedConnection, connected[0].LastActivitySource);
        Assert.AreEqual(now.AddMinutes(5), connected[0].LastActiveUtc);
        Assert.AreEqual(now.AddMinutes(5), afterConnection[0].LastActiveUtc);
        Assert.AreEqual(1, commandLines.ReadCount);
        Assert.AreEqual(1, classifier.ClassificationCount);
    }

    [TestMethod]
    public async Task GetListeners_does_not_display_connection_only_endpoints()
    {
        var processId = Environment.ProcessId;
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(49152, processId, "127.0.0.1", TcpEndpointState.Established)
        ]);
        var provider = new WindowsPortSnapshotProvider(reader, currentProcessId: -1);

        var listeners = await provider.GetListenersAsync();

        Assert.IsEmpty(listeners);
    }

    [TestMethod]
    public async Task GetListeners_uses_first_seen_when_process_start_cannot_be_read()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(139, 4, "0.0.0.0", TcpEndpointState.Listen)
        ]);
        var provider = new WindowsPortSnapshotProvider(
            reader,
            currentProcessId: -1,
            timeProvider: new MutableTimeProvider(now));

        var listeners = await provider.GetListenersAsync();

        Assert.AreEqual(now, listeners[0].LastActiveUtc);
        Assert.AreEqual(PortActivitySource.FirstSeen, listeners[0].LastActivitySource);
    }

    [TestMethod]
    public async Task GetListeners_reads_process_metadata_only_when_its_listening_ports_change()
    {
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Established)
        ]);
        var metadata = new CountingMetadataReader();
        var provider = new WindowsPortSnapshotProvider(
            reader,
            currentProcessId: -1,
            new CountingCommandLineReader("node server.js"),
            metadataReader: metadata);

        await provider.GetListenersAsync();
        await provider.GetListenersAsync();
        Assert.AreEqual(1, metadata.ReadCount, "An unchanged listener set must not re-read process details.");

        reader.Endpoints =
        [
            new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(3001, 1234, "127.0.0.1", TcpEndpointState.Listen)
        ];
        await provider.GetListenersAsync();
        Assert.AreEqual(2, metadata.ReadCount, "A process whose ports changed is read again.");

        reader.Endpoints = [];
        await provider.GetListenersAsync();
        reader.Endpoints = [new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Listen)];
        await provider.GetListenersAsync();
        Assert.AreEqual(3, metadata.ReadCount, "A PID that disappeared is read again when it returns.");
    }

    [TestMethod]
    public async Task GetListeners_returns_the_same_instances_until_a_listener_changes()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var reader = new FakeTcpTableReader(
        [
            new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(5000, 1234, "127.0.0.1", TcpEndpointState.Listen)
        ]);
        var provider = new WindowsPortSnapshotProvider(
            reader,
            currentProcessId: -1,
            new CountingCommandLineReader("node server.js"),
            timeProvider: timeProvider,
            metadataReader: new CountingMetadataReader());

        var first = await provider.GetListenersAsync();
        timeProvider.UtcNow = now.AddMinutes(1);
        var unchanged = await provider.GetListenersAsync();
        reader.Endpoints =
        [
            new RawTcpEndpoint(3000, 1234, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(5000, 1234, "127.0.0.1", TcpEndpointState.Listen),
            new RawTcpEndpoint(5000, 1234, "127.0.0.1", TcpEndpointState.Established)
        ];
        var connected = await provider.GetListenersAsync();

        Assert.AreSame(first[0], unchanged[0]);
        Assert.AreSame(first[1], unchanged[1]);
        Assert.AreSame(first[0], connected[0]);
        Assert.AreNotSame(first[1], connected[1]);
        Assert.AreEqual(PortActivitySource.ObservedConnection, connected[1].LastActivitySource);
    }

    private sealed class FakeTcpTableReader(IReadOnlyList<RawTcpEndpoint> endpoints) : ITcpTableReader
    {
        public IReadOnlyList<RawTcpEndpoint> Endpoints { get; set; } = endpoints;

        public IReadOnlyList<RawTcpEndpoint> ReadEndpoints() => Endpoints;
    }

    private sealed class CountingCommandLineReader(string commandLine) : IProcessCommandLineReader
    {
        public int ReadCount { get; private set; }

        public IReadOnlyDictionary<int, string?> ReadCommandLines(IReadOnlyCollection<int> processIds)
        {
            ReadCount++;
            return processIds.ToDictionary(processId => processId, _ => (string?)commandLine);
        }
    }

    private sealed class CountingMetadataReader : IProcessMetadataReader
    {
        private static readonly DateTimeOffset StartTime = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

        public int ReadCount { get; private set; }

        public ProcessMetadata Read(int processId)
        {
            ReadCount++;
            return new ProcessMetadata(processId, "node", @"C:\Tools\node.exe", StartTime);
        }
    }

    private sealed class CountingClassifier : IPortServiceClassifier
    {
        public int ClassificationCount { get; private set; }

        public PortClassification Classify(ProcessMetadata process, int port)
        {
            ClassificationCount++;
            return new PortClassification("Vite Dev Server", "Vite", PortCategory.Dev);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
