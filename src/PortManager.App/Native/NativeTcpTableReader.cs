using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace PortManager.Native;

internal enum TcpEndpointState : uint
{
    Closed = 1,
    Listen = 2,
    SynSent = 3,
    SynReceived = 4,
    Established = 5,
    FinWait1 = 6,
    FinWait2 = 7,
    CloseWait = 8,
    Closing = 9,
    LastAck = 10,
    TimeWait = 11,
    DeleteTcb = 12
}

internal sealed record RawTcpEndpoint(
    int Port,
    int ProcessId,
    string? Address,
    TcpEndpointState State);

internal interface ITcpTableReader
{
    IReadOnlyList<RawTcpEndpoint> ReadEndpoints();
}

internal sealed class NativeTcpTableReader : ITcpTableReader
{
    private const int AddressFamilyInet = 2;
    private const int AddressFamilyInet6 = 23;
    private const uint ErrorInsufficientBuffer = 122;
    private const int Ipv4RowSize = 24;
    private const int Ipv6RowSize = 56;

    public IReadOnlyList<RawTcpEndpoint> ReadEndpoints()
    {
        var endpoints = new List<RawTcpEndpoint>();
        endpoints.AddRange(ReadTable(AddressFamilyInet));
        endpoints.AddRange(ReadTable(AddressFamilyInet6));
        return endpoints;
    }

    internal static int DecodePort(uint networkOrderPort)
    {
        var networkValue = unchecked((short)(networkOrderPort & 0xFFFF));
        return unchecked((ushort)IPAddress.NetworkToHostOrder(networkValue));
    }

    private static IReadOnlyList<RawTcpEndpoint> ReadTable(int addressFamily)
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref bufferSize,
            order: false,
            addressFamily,
            TcpTableClass.OwnerPidAll,
            0);

        if (result != ErrorInsufficientBuffer && result != 0)
        {
            throw new Win32Exception(unchecked((int)result));
        }

        if (bufferSize <= 0)
        {
            return [];
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(
                    buffer,
                    ref bufferSize,
                    order: false,
                    addressFamily,
                    TcpTableClass.OwnerPidAll,
                    0);

                if (result == ErrorInsufficientBuffer)
                {
                    continue;
                }

                if (result != 0)
                {
                    throw new Win32Exception(unchecked((int)result));
                }

                return addressFamily == AddressFamilyInet
                    ? ParseIpv4Table(buffer)
                    : ParseIpv6Table(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        throw new Win32Exception(unchecked((int)ErrorInsufficientBuffer));
    }

    private static IReadOnlyList<RawTcpEndpoint> ParseIpv4Table(IntPtr table)
    {
        var count = Marshal.ReadInt32(table);
        var rows = new List<RawTcpEndpoint>(Math.Max(count, 0));
        var rowPointer = IntPtr.Add(table, sizeof(uint));

        for (var index = 0; index < count; index++)
        {
            var pointer = IntPtr.Add(rowPointer, index * Ipv4RowSize);
            rows.Add(ParseIpv4Row(pointer));
        }

        return rows;
    }

    private static IReadOnlyList<RawTcpEndpoint> ParseIpv6Table(IntPtr table)
    {
        var count = Marshal.ReadInt32(table);
        var rows = new List<RawTcpEndpoint>(Math.Max(count, 0));
        var rowPointer = IntPtr.Add(table, sizeof(uint));

        for (var index = 0; index < count; index++)
        {
            var pointer = IntPtr.Add(rowPointer, index * Ipv6RowSize);
            rows.Add(ParseIpv6Row(pointer));
        }

        return rows;
    }

    internal static RawTcpEndpoint ParseIpv4Row(IntPtr pointer)
    {
        var state = (TcpEndpointState)ReadUInt32(pointer, 0);
        var localAddressValue = ReadUInt32(pointer, 4);
        var localPortValue = ReadUInt32(pointer, 8);
        var processId = unchecked((int)ReadUInt32(pointer, 20));

        // Only listeners display their bound address; connection rows are used for activity tracking alone.
        var address = state == TcpEndpointState.Listen
            ? new IPAddress(localAddressValue).ToString()
            : null;

        return new RawTcpEndpoint(DecodePort(localPortValue), processId, address, state);
    }

    internal static RawTcpEndpoint ParseIpv6Row(IntPtr pointer)
    {
        var localPortValue = ReadUInt32(pointer, 20);
        var state = (TcpEndpointState)ReadUInt32(pointer, 48);
        var processId = unchecked((int)ReadUInt32(pointer, 52));

        string? address = null;
        if (state == TcpEndpointState.Listen)
        {
            var addressBytes = new byte[16];
            Marshal.Copy(pointer, addressBytes, 0, addressBytes.Length);
            address = new IPAddress(addressBytes, ReadUInt32(pointer, 16)).ToString();
        }

        return new RawTcpEndpoint(DecodePort(localPortValue), processId, address, state);
    }

    private static uint ReadUInt32(IntPtr pointer, int offset) =>
        unchecked((uint)Marshal.ReadInt32(pointer, offset));

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        int addressFamily,
        TcpTableClass tableClass,
        uint reserved);

    private enum TcpTableClass
    {
        OwnerPidAll = 5
    }
}
