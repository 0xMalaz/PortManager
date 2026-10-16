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

    [TestMethod]
    public void AppendTables_preserves_every_row_and_family_order_in_one_destination()
    {
        var ipv4 = Marshal.AllocHGlobal(sizeof(uint) + 2 * 24);
        var ipv6 = Marshal.AllocHGlobal(sizeof(uint) + 2 * 56);
        try
        {
            Marshal.WriteInt32(ipv4, 2);
            Marshal.WriteInt32(ipv6, 2);
            for (var index = 0; index < 2; index++)
            {
                var row4 = IntPtr.Add(ipv4, sizeof(uint) + index * 24);
                Marshal.WriteInt32(row4, 0, (int)(index == 0 ? TcpEndpointState.Listen : TcpEndpointState.TimeWait));
                Marshal.WriteInt32(row4, 4, BitConverter.ToInt32(IPAddress.Loopback.GetAddressBytes()));
                Marshal.WriteInt32(row4, 8, unchecked((int)EncodePort(3000 + index)));
                Marshal.WriteInt32(row4, 20, 100 + index);

                var row6 = IntPtr.Add(ipv6, sizeof(uint) + index * 56);
                Marshal.Copy(IPAddress.Parse("fe80::1234").GetAddressBytes(), 0, row6, 16);
                Marshal.WriteInt32(row6, 16, 7);
                Marshal.WriteInt32(row6, 20, unchecked((int)EncodePort(5000 + index)));
                Marshal.WriteInt32(row6, 48, (int)(index == 0 ? TcpEndpointState.Listen : TcpEndpointState.Established));
                Marshal.WriteInt32(row6, 52, 200 + index);
            }

            var sentinel = new RawTcpEndpoint(80, 1, "0.0.0.0", TcpEndpointState.Listen);
            var rows = new List<RawTcpEndpoint> { sentinel };
            NativeTcpTableReader.AppendIpv4Table(ipv4, rows);
            NativeTcpTableReader.AppendIpv6Table(ipv6, rows);

            CollectionAssert.AreEqual(new[]
            {
                sentinel,
                new RawTcpEndpoint(3000, 100, "127.0.0.1", TcpEndpointState.Listen),
                new RawTcpEndpoint(3001, 101, null, TcpEndpointState.TimeWait),
                new RawTcpEndpoint(5000, 200, "fe80::1234%7", TcpEndpointState.Listen),
                new RawTcpEndpoint(5001, 201, null, TcpEndpointState.Established)
            }, rows);

            Marshal.WriteInt32(ipv4, 0);
            Marshal.WriteInt32(ipv6, 0);
            NativeTcpTableReader.AppendIpv4Table(ipv4, rows);
            NativeTcpTableReader.AppendIpv6Table(ipv6, rows);
            Assert.HasCount(5, rows, "Empty tables must preserve rows already read from the other family.");
        }
        finally
        {
            Marshal.FreeHGlobal(ipv4);
            Marshal.FreeHGlobal(ipv6);
        }
    }
}
