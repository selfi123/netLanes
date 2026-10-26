using System.Net;
using System.Runtime.InteropServices;
using NetLanes.Core.Models;

namespace NetLanes.Infrastructure.Windows;

public static class IpHlpApi
{
    private const int AF_INET = 2;
    private const int AF_INET6 = 23;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int NO_ERROR = 0;

    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    public record SocketEndpointInfo(
        NetProtocol Protocol,
        IPAddress LocalAddress,
        int LocalPort,
        IPAddress RemoteAddress,
        int RemotePort,
        int OwningPid,
        string State);

    public static List<SocketEndpointInfo> GetAllActiveSockets()
    {
        var sockets = new List<SocketEndpointInfo>(256);
        GetTcpV4Sockets(sockets);
        GetTcpV6Sockets(sockets);
        GetUdpV4Sockets(sockets);
        GetUdpV6Sockets(sockets);
        return sockets;
    }

    private static void GetTcpV4Sockets(List<SocketEndpointInfo> list)
    {
        int buffSize = 0;
        uint ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (buffSize <= 0) return;

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedTcpTable(tcpTablePtr, ref buffSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (ret != NO_ERROR) return;

            int numEntries = Marshal.ReadInt32(tcpTablePtr);
            IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);

            // MIB_TCPROW_OWNER_PID is 24 bytes:
            // DWORD dwState (4)
            // DWORD dwLocalAddr (4)
            // DWORD dwLocalPort (4)
            // DWORD dwRemoteAddr (4)
            // DWORD dwRemotePort (4)
            // DWORD dwOwningPid (4)
            for (int i = 0; i < numEntries; i++)
            {
                int stateCode = Marshal.ReadInt32(rowPtr, 0);
                uint localAddrRaw = (uint)Marshal.ReadInt32(rowPtr, 4);
                uint localPortRaw = (uint)Marshal.ReadInt32(rowPtr, 8);
                uint remoteAddrRaw = (uint)Marshal.ReadInt32(rowPtr, 12);
                uint remotePortRaw = (uint)Marshal.ReadInt32(rowPtr, 16);
                int owningPid = Marshal.ReadInt32(rowPtr, 20);

                var localIp = new IPAddress(localAddrRaw);
                var remoteIp = new IPAddress(remoteAddrRaw);
                int localPort = ConvertPort(localPortRaw);
                int remotePort = ConvertPort(remotePortRaw);

                list.Add(new SocketEndpointInfo(
                    NetProtocol.Tcp,
                    localIp,
                    localPort,
                    remoteIp,
                    remotePort,
                    owningPid,
                    GetTcpStateString(stateCode)));

                rowPtr = IntPtr.Add(rowPtr, 24);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
    }

    private static void GetTcpV6Sockets(List<SocketEndpointInfo> list)
    {
        int buffSize = 0;
        uint ret = GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
        if (buffSize <= 0) return;

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedTcpTable(tcpTablePtr, ref buffSize, true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, 0);
            if (ret != NO_ERROR) return;

            int numEntries = Marshal.ReadInt32(tcpTablePtr);
            IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);

            // MIB_TCP6ROW_OWNER_PID is 56 bytes:
            // UCHAR ucLocalAddr[16] (16)
            // DWORD dwLocalScopeId (4)
            // DWORD dwLocalPort (4)
            // UCHAR ucRemoteAddr[16] (16)
            // DWORD dwRemoteScopeId (4)
            // DWORD dwRemotePort (4)
            // DWORD dwState (4)
            // DWORD dwOwningPid (4)
            byte[] addrBuf = new byte[16];
            for (int i = 0; i < numEntries; i++)
            {
                Marshal.Copy(rowPtr, addrBuf, 0, 16);
                var localIp = new IPAddress(addrBuf);

                uint localPortRaw = (uint)Marshal.ReadInt32(rowPtr, 20);

                Marshal.Copy(IntPtr.Add(rowPtr, 24), addrBuf, 0, 16);
                var remoteIp = new IPAddress(addrBuf);

                uint remotePortRaw = (uint)Marshal.ReadInt32(rowPtr, 44);
                int stateCode = Marshal.ReadInt32(rowPtr, 48);
                int owningPid = Marshal.ReadInt32(rowPtr, 52);

                list.Add(new SocketEndpointInfo(
                    NetProtocol.Tcp,
                    localIp,
                    ConvertPort(localPortRaw),
                    remoteIp,
                    ConvertPort(remotePortRaw),
                    owningPid,
                    GetTcpStateString(stateCode)));

                rowPtr = IntPtr.Add(rowPtr, 56);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
    }

    private static void GetUdpV4Sockets(List<SocketEndpointInfo> list)
    {
        int buffSize = 0;
        uint ret = GetExtendedUdpTable(IntPtr.Zero, ref buffSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
        if (buffSize <= 0) return;

        IntPtr udpTablePtr = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedUdpTable(udpTablePtr, ref buffSize, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
            if (ret != NO_ERROR) return;

            int numEntries = Marshal.ReadInt32(udpTablePtr);
            IntPtr rowPtr = IntPtr.Add(udpTablePtr, 4);

            // MIB_UDPROW_OWNER_PID is 12 bytes:
            // DWORD dwLocalAddr (4)
            // DWORD dwLocalPort (4)
            // DWORD dwOwningPid (4)
            for (int i = 0; i < numEntries; i++)
            {
                uint localAddrRaw = (uint)Marshal.ReadInt32(rowPtr, 0);
                uint localPortRaw = (uint)Marshal.ReadInt32(rowPtr, 4);
                int owningPid = Marshal.ReadInt32(rowPtr, 8);

                list.Add(new SocketEndpointInfo(
                    NetProtocol.Udp,
                    new IPAddress(localAddrRaw),
                    ConvertPort(localPortRaw),
                    IPAddress.Any,
                    0,
                    owningPid,
                    "Listening"));

                rowPtr = IntPtr.Add(rowPtr, 12);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(udpTablePtr);
        }
    }

    private static void GetUdpV6Sockets(List<SocketEndpointInfo> list)
    {
        int buffSize = 0;
        uint ret = GetExtendedUdpTable(IntPtr.Zero, ref buffSize, true, AF_INET6, UDP_TABLE_OWNER_PID, 0);
        if (buffSize <= 0) return;

        IntPtr udpTablePtr = Marshal.AllocHGlobal(buffSize);
        try
        {
            ret = GetExtendedUdpTable(udpTablePtr, ref buffSize, true, AF_INET6, UDP_TABLE_OWNER_PID, 0);
            if (ret != NO_ERROR) return;

            int numEntries = Marshal.ReadInt32(udpTablePtr);
            IntPtr rowPtr = IntPtr.Add(udpTablePtr, 4);

            // MIB_UDP6ROW_OWNER_PID is 28 bytes:
            // UCHAR ucLocalAddr[16] (16)
            // DWORD dwLocalScopeId (4)
            // DWORD dwLocalPort (4)
            // DWORD dwOwningPid (4)
            byte[] addrBuf = new byte[16];
            for (int i = 0; i < numEntries; i++)
            {
                Marshal.Copy(rowPtr, addrBuf, 0, 16);
                uint localPortRaw = (uint)Marshal.ReadInt32(rowPtr, 20);
                int owningPid = Marshal.ReadInt32(rowPtr, 24);

                list.Add(new SocketEndpointInfo(
                    NetProtocol.Udp,
                    new IPAddress(addrBuf),
                    ConvertPort(localPortRaw),
                    IPAddress.IPv6Any,
                    0,
                    owningPid,
                    "Listening"));

                rowPtr = IntPtr.Add(rowPtr, 28);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(udpTablePtr);
        }
    }

    private static int ConvertPort(uint dwPort)
    {
        // Port numbers from IP Helper are stored in network byte order (big-endian)
        return (int)(((dwPort & 0xFF) << 8) | ((dwPort >> 8) & 0xFF));
    }

    private static string GetTcpStateString(int state) => state switch
    {
        1 => "Closed",
        2 => "Listen",
        3 => "SynSent",
        4 => "SynRcvd",
        5 => "Established",
        6 => "FinWait1",
        7 => "FinWait2",
        8 => "CloseWait",
        9 => "Closing",
        10 => "LastAck",
        11 => "TimeWait",
        12 => "DeleteTCB",
        _ => "Unknown"
    };
}
