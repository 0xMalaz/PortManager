using System.ComponentModel;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class ProcessTerminationServiceTests
{
    [TestMethod]
    public async Task Prepare_lists_every_port_owned_by_the_process()
    {
        var first = TestData.Listener(port: 3000);
        var second = TestData.Listener(port: 5000);
        var provider = new MutableSnapshotProvider([second, first]);
        var service = new ProcessTerminationService(provider);

        var preview = await service.PrepareAsync(first);

        CollectionAssert.AreEqual(new[] { 3000, 5000 }, preview.OwnedPorts.ToArray());
    }

    [TestMethod]
    public async Task Prepare_rejects_stale_process_identity()
    {
        var selected = TestData.Listener(startTime: DateTimeOffset.UtcNow.AddMinutes(-2));
        var replacement = selected with { ProcessStartTimeUtc = DateTimeOffset.UtcNow };
        var provider = new MutableSnapshotProvider([replacement]);
        var service = new ProcessTerminationService(provider);

        await Assert.ThrowsExactlyAsync<ListenerStaleException>(() => service.PrepareAsync(selected));
    }

    [TestMethod]
    public async Task Terminate_returns_access_denied_when_windows_rejects_kill()
    {
        var listener = TestData.Listener();
        var provider = new MutableSnapshotProvider([listener]);
        var service = CreateService(
            provider,
            (_, _) => throw new Win32Exception(5));

        var result = await service.TerminateAsync(new TerminationPreview(listener, [listener.Port]));

        Assert.AreEqual(TerminationOutcome.AccessDenied, result.Outcome);
    }

    [TestMethod]
    public async Task Terminate_returns_success_only_after_port_disappears()
    {
        var listener = TestData.Listener();
        var provider = new MutableSnapshotProvider([listener]);
        var service = CreateService(
            provider,
            (_, _) =>
            {
                provider.Snapshot = [];
                return Task.CompletedTask;
            });

        var result = await service.TerminateAsync(new TerminationPreview(listener, [listener.Port]));

        Assert.AreEqual(TerminationOutcome.Success, result.Outcome);
    }

    [TestMethod]
    public async Task Terminate_reports_a_port_that_immediately_reopens()
    {
        var listener = TestData.Listener();
        var replacement = TestData.Listener(
            port: listener.Port,
            processId: 9876,
            processName: "watcher",
            startTime: DateTimeOffset.UtcNow);
        var provider = new MutableSnapshotProvider([listener]);
        var service = CreateService(
            provider,
            (_, _) =>
            {
                provider.Snapshot = [replacement];
                return Task.CompletedTask;
            });

        var result = await service.TerminateAsync(new TerminationPreview(listener, [listener.Port]));

        Assert.AreEqual(TerminationOutcome.StillListening, result.Outcome);
        Assert.AreEqual("watcher", result.CurrentOwner?.ProcessName);
    }

    private static ProcessTerminationService CreateService(
        IPortSnapshotProvider provider,
        Func<PortListener, CancellationToken, Task> terminate) =>
        new(
            provider,
            terminate,
            portReleaseWait: TimeSpan.FromMilliseconds(10),
            pollInterval: TimeSpan.FromMilliseconds(1));
}

