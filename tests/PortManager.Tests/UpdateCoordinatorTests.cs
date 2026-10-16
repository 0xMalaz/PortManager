using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class UpdateCoordinatorTests
{
    [TestMethod]
    public async Task Construction_does_nothing_and_unavailable_build_does_not_check()
    {
        var fake = new FakeUpdater { CanUpdate = false };
        using var coordinator = new UpdateCoordinator(fake, () => Assert.Fail("Must not exit"));
        Assert.IsEmpty(fake.Calls);
        await coordinator.RunAsync();
        Assert.AreEqual(UpdateStage.Unavailable, coordinator.Status.Stage);
        Assert.IsEmpty(fake.Calls);
    }

    [TestMethod]
    public async Task No_update_does_not_download_or_exit()
    {
        var fake = new FakeUpdater { Available = null };
        using var coordinator = new UpdateCoordinator(fake, () => Assert.Fail("Must not exit"));
        await coordinator.RunAsync();
        CollectionAssert.AreEqual(new[] { "check" }, fake.Calls);
        Assert.AreEqual(UpdateStage.NoUpdate, coordinator.Status.Stage);
    }

    [TestMethod]
    public async Task Download_and_apply_precede_graceful_shutdown_and_only_run_once()
    {
        var fake = new FakeUpdater();
        var statuses = new List<UpdateStatus>();
        using var coordinator = new UpdateCoordinator(fake, () => fake.Calls.Add("shutdown"));
        coordinator.StatusChanged += statuses.Add;
        await coordinator.RunAsync();
        await coordinator.RunAsync();
        CollectionAssert.AreEqual(new[] { "check", "download", "apply", "shutdown" }, fake.Calls);
        Assert.AreEqual(UpdateStage.Restarting, coordinator.Status.Stage);
        Assert.AreEqual(UpdateStage.Downloading, statuses[^2].Stage);
        Assert.AreEqual(100, statuses[^2].Progress);
    }

    [TestMethod]
    public async Task Pending_download_applies_without_network()
    {
        var fake = new FakeUpdater { PendingVersion = "2.0.0" };
        using var coordinator = new UpdateCoordinator(fake, () => fake.Calls.Add("shutdown"));
        await coordinator.RunAsync();
        CollectionAssert.AreEqual(new[] { "apply", "shutdown" }, fake.Calls);
    }

    [TestMethod]
    [DataRow("check")]
    [DataRow("download")]
    [DataRow("apply")]
    public async Task Failure_keeps_app_open_and_allows_retry(string failAt)
    {
        var fake = new FakeUpdater { FailAt = failAt };
        var exits = 0;
        using var coordinator = new UpdateCoordinator(fake, () => exits++);
        await coordinator.RunAsync();
        Assert.AreEqual(UpdateStage.Failed, coordinator.Status.Stage);
        Assert.AreEqual(0, exits);
        fake.FailAt = null;
        await coordinator.RunAsync();
        Assert.AreEqual(1, exits);
        if (failAt == "apply") Assert.AreEqual(1, fake.Calls.Count(call => call == "download"));
    }

    [TestMethod]
    public async Task Repeated_clicks_during_check_are_ignored()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeUpdater { CheckGate = gate.Task };
        using var coordinator = new UpdateCoordinator(fake, () => fake.Calls.Add("shutdown"));
        var first = coordinator.RunAsync();
        await coordinator.RunAsync();
        CollectionAssert.AreEqual(new[] { "check" }, fake.Calls);
        gate.SetResult();
        await first;
        Assert.AreEqual(1, fake.Calls.Count(call => call == "apply"));
    }

    [TestMethod]
    public async Task Exit_during_non_cancelable_check_prevents_download_and_restart()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeUpdater { CheckGate = gate.Task };
        using var coordinator = new UpdateCoordinator(fake, () => Assert.Fail("Must not restart"));
        var operation = coordinator.RunAsync();
        coordinator.Dispose();
        gate.SetResult();
        await operation;
        CollectionAssert.AreEqual(new[] { "check" }, fake.Calls);
    }

    private sealed class FakeUpdater : IAppUpdater
    {
        public bool CanUpdate { get; set; } = true;
        public string? PendingVersion { get; set; }
        public string? Available { get; set; } = "2.0.0";
        public string? FailAt { get; set; }
        public Task CheckGate { get; set; } = Task.CompletedTask;
        public List<string> Calls { get; } = [];
        public async Task<string?> CheckAsync(CancellationToken cancellationToken)
        {
            Call("check");
            await CheckGate;
            return Available;
        }
        public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            Call("download");
            PendingVersion = Available;
            return Task.CompletedTask;
        }
        public void ApplyAfterExit() => Call("apply");
        private void Call(string name)
        {
            Calls.Add(name);
            if (FailAt == name) throw new IOException("Simulated failure");
        }
    }
}
