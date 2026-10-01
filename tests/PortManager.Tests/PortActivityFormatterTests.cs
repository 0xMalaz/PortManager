using PortManager.Models;

namespace PortManager.Tests;

[TestClass]
public sealed class PortActivityFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(30, "just now")]
    [DataRow(360, "6m ago")]
    [DataRow(10_800, "3h ago")]
    public void Format_uses_compact_relative_times(int secondsAgo, string expected)
    {
        Assert.AreEqual(expected, PortActivityFormatter.Format(Now.AddSeconds(-secondsAgo), Now));
    }

    [TestMethod]
    public void Format_uses_local_date_after_twenty_four_hours()
    {
        var timestamp = Now.AddDays(-2);

        Assert.AreEqual(timestamp.ToLocalTime().ToString("g"), PortActivityFormatter.Format(timestamp, Now));
    }
}
