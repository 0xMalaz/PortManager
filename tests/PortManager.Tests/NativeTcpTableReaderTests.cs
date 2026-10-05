using System.Net;
using System.Runtime.InteropServices;
using PortManager.Native;

namespace PortManager.Tests;

[TestClass]
public sealed class NativeTcpTableReaderTests
{
    [TestMethod]
    [DataRow(80)]
    [DataRow(3000)]
    [DataRow(5001)]
    [DataRow(65535)]
    public void DecodePort_converts_network_byte_order(int port)
    {
        var encoded = unchecked((uint)(ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));

        Assert.AreEqual(port, NativeTcpTableReader.DecodePort(encoded));
    }

    [TestMethod]
    public void ParseIpv4Row_reads_connected_state_and_owner()
    {
        var row = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.WriteInt32(row, 0, (int)TcpEndpointState.Established);
            Marshal.WriteInt32(row, 4, BitConverter.ToInt32(IPAddress.Loopback.GetAddressBytes()));
            Marshal.WriteInt32(row, 8, unchecked((int)EncodePort(5173)));
            Marshal.WriteInt32(row, 20, 8214);

            var endpoint = NativeTcpTableReader.ParseIpv4Row(row);

            Assert.AreEqual(5173, endpoint.Port);
            Assert.AreEqual(8214, endpoint.ProcessId);
            Assert.IsNull(endpoint.Address, "Connection rows skip address formatting.");
            Assert.AreEqual(TcpEndpointState.Established, endpoint.State);
        }
        finally
        {
            Marshal.FreeHGlobal(row);
        }
    }

    [TestMethod]
    public void ParseIpv4Row_formats_listener_address_in_network_order()
    {
        var row = Marshal.AllocHGlobal(24);
        try
        {
            Marshal.WriteInt32(row, 0, (int)TcpEndpointState.Listen);
            Marshal.WriteInt32(row, 4, BitConverter.ToInt32(IPAddress.Parse("192.168.1.20").GetAddressBytes()));
            Marshal.WriteInt32(row, 8, unchecked((int)EncodePort(3000)));
            Marshal.WriteInt32(row, 20, 8214);

            var endpoint = NativeTcpTableReader.ParseIpv4Row(row);

            Assert.AreEqual("192.168.1.20", endpoint.Address);
            Assert.AreEqual(TcpEndpointState.Listen, endpoint.State);
        }
        finally
        {
            Marshal.FreeHGlobal(row);
        }
    }

    [TestMethod]
    public void ParseIpv6Row_reads_listener_state_and_owner()
    {
        var row = Marshal.AllocHGlobal(56);
        try
        {
            var address = IPAddress.IPv6Loopback.GetAddressBytes();
            Marshal.Copy(address, 0, row, address.Length);
            Marshal.WriteInt32(row, 16, 0);
            Marshal.WriteInt32(row, 20, unchecked((int)EncodePort(8000)));
            Marshal.WriteInt32(row, 48, (int)TcpEndpointState.Listen);
            Marshal.WriteInt32(row, 52, 4567);

            var endpoint = NativeTcpTableReader.ParseIpv6Row(row);

            Assert.AreEqual(8000, endpoint.Port);
            Assert.AreEqual(4567, endpoint.ProcessId);
            Assert.AreEqual("::1", endpoint.Address);
            Assert.AreEqual(TcpEndpointState.Listen, endpoint.State);
        }
        finally
        {
            Marshal.FreeHGlobal(row);
        }
    }

    private static uint EncodePort(int port) =>
        unchecked((uint)(ushort)IPAddress.HostToNetworkOrder(unchecked((short)port)));
}
