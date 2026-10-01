using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class PortActivityTrackerTests
{
    private static readonly DateTimeOffset ProcessStart = new(2026, 8, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstScan = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Update_uses_process_start_until_a_connection_is_observed()
    {
        var tracker = new PortActivityTracker();
        var key = Key(port: 3000, processId: 100);

        var initial = tracker.Update([(key, ProcessStart)], new HashSet<(int, int)>(), FirstScan);
        var connectedAt = FirstScan.AddMinutes(5);
        var connected = tracker.Update([(key, ProcessStart)], new HashSet<(int, int)> { (3000, 100) }, connectedAt);
        var later = tracker.Update([(key, ProcessStart)], new HashSet<(int, int)>(), connectedAt.AddMinutes(2));

        Assert.AreEqual(ProcessStart, initial[key].TimestampUtc);
        Assert.AreEqual(PortActivitySource.ProcessStart, initial[key].Source);
        Assert.AreEqual(connectedAt, connected[key].TimestampUtc);
        Assert.AreEqual(PortActivitySource.ObservedConnection, connected[key].Source);
        Assert.AreEqual(connectedAt, later[key].TimestampUtc);
    }

    [TestMethod]
    public void Update_tracks_each_port_independently_for_one_process()
    {
        var tracker = new PortActivityTracker();
        var first = Key(port: 3000, processId: 100);
        var second = Key(port: 5000, processId: 100);

        var activity = tracker.Update(
            [(first, ProcessStart), (second, ProcessStart)],
            new HashSet<(int, int)> { (5000, 100) },
            FirstScan);

        Assert.AreEqual(PortActivitySource.ProcessStart, activity[first].Source);
        Assert.AreEqual(PortActivitySource.ObservedConnection, activity[second].Source);
    }

    [TestMethod]
    public void Update_resets_activity_when_the_process_identity_changes()
    {
        var tracker = new PortActivityTracker();
        var original = Key(port: 3000, processId: 100, startTime: ProcessStart);
        var replacementStart = FirstScan.AddMinutes(10);
        var replacement = Key(port: 3000, processId: 100, startTime: replacementStart);

        tracker.Update([(original, ProcessStart)], new HashSet<(int, int)> { (3000, 100) }, FirstScan);
        var activity = tracker.Update([(replacement, replacementStart)], new HashSet<(int, int)>(), replacementStart);

        Assert.AreEqual(replacementStart, activity[replacement].TimestampUtc);
        Assert.AreEqual(PortActivitySource.ProcessStart, activity[replacement].Source);
        Assert.IsFalse(activity.ContainsKey(original));
    }

    [TestMethod]
    public void Update_uses_first_seen_when_process_start_is_unavailable()
    {
        var tracker = new PortActivityTracker();
        var key = new PortActivityKey(3000, 100, null, "node");

        var activity = tracker.Update([(key, null)], new HashSet<(int, int)>(), FirstScan);

        Assert.AreEqual(FirstScan, activity[key].TimestampUtc);
        Assert.AreEqual(PortActivitySource.FirstSeen, activity[key].Source);
    }

    private static PortActivityKey Key(int port, int processId, DateTimeOffset? startTime = null) =>
        new(port, processId, startTime ?? ProcessStart, "node");
}
