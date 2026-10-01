using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class PortListenerFilterTests
{
    private readonly PortManager.Models.PortListener _listener = TestData.Listener(
        port: 5173,
        processId: 8214,
        processName: "node",
        addresses: ["127.0.0.1"]);

    [TestMethod]
    [DataRow("")]
    [DataRow("5173")]
    [DataRow("8214")]
    [DataRow("NODE")]
    [DataRow("localhost")]
    [DataRow("Node Server")]
    [DataRow("node")]
    public void Matches_searches_port_pid_process_and_address(string query)
    {
        Assert.IsTrue(PortListenerFilter.Matches(_listener, query));
    }

    [TestMethod]
    public void Matches_searches_friendly_service_and_framework()
    {
        var listener = TestData.Listener(
            serviceName: "Vite Dev Server",
            frameworkName: "Vite");

        Assert.IsTrue(PortListenerFilter.Matches(listener, "dev server"));
        Assert.IsTrue(PortListenerFilter.Matches(listener, "VITE"));
    }

    [TestMethod]
    public void Matches_rejects_unrelated_query()
    {
        Assert.IsFalse(PortListenerFilter.Matches(_listener, "postgres"));
    }
}
