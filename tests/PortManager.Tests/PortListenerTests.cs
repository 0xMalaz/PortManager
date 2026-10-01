namespace PortManager.Tests;

[TestClass]
public sealed class PortListenerTests
{
    [TestMethod]
    public void AddressDisplay_collapses_loopback_bindings()
    {
        var listener = TestData.Listener(addresses: ["127.0.0.1", "::1"]);

        Assert.AreEqual("localhost", listener.AddressDisplay);
    }

    [TestMethod]
    public void AddressDisplay_collapses_wildcard_bindings()
    {
        var listener = TestData.Listener(addresses: ["0.0.0.0", "::"]);

        Assert.AreEqual("All interfaces", listener.AddressDisplay);
    }
}

