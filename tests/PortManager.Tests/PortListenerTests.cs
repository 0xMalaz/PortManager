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

    [TestMethod]
    public void AddressDisplay_preserves_special_cases_and_general_address_order()
    {
        Assert.AreEqual("Unknown address", (TestData.Listener() with { Addresses = [] }).AddressDisplay);
        Assert.AreEqual("localhost", TestData.Listener(addresses: ["::1", "127.0.0.1", "::1"]).AddressDisplay);
        Assert.AreEqual("All interfaces", TestData.Listener(addresses: ["192.168.1.20", "::", "127.0.0.1"]).AddressDisplay);
        Assert.AreEqual("All interfaces", TestData.Listener(addresses: ["0.0.0.0", "192.168.1.20"]).AddressDisplay);
        Assert.AreEqual("127.0.0.1, 192.168.1.20", TestData.Listener(addresses: ["127.0.0.1", "192.168.1.20", "127.0.0.1"]).AddressDisplay);
        Assert.AreEqual("FE80::ABCD, 192.168.1.20", TestData.Listener(addresses: ["FE80::ABCD", "fe80::abcd", "192.168.1.20"]).AddressDisplay);
    }

    [TestMethod]
    public void AddressDisplay_stays_current_after_record_copy_or_address_list_change()
    {
        var addresses = new List<string> { "127.0.0.1" };
        var original = TestData.Listener() with { Addresses = addresses };
        var equalCopy = original with { };
        Assert.AreEqual("localhost", original.AddressDisplay);
        Assert.AreEqual(equalCopy, original, "Reading a display property must not change record equality.");

        var replacement = original with { Addresses = ["0.0.0.0"] };
        Assert.AreEqual("All interfaces", replacement.AddressDisplay);
        addresses.Add("192.168.1.20");
        Assert.AreEqual("127.0.0.1, 192.168.1.20", original.AddressDisplay);
    }
}

