using System.Runtime.InteropServices;

namespace NetLanes.NetworkEngine.Interop;

public static unsafe class WinDivertNative
{
    private const string DllName = "WinDivert.dll";

    public const int WINDIVERT_LAYER_NETWORK = 0;
    public const int WINDIVERT_LAYER_NETWORK_FORWARD = 1;
    public const int WINDIVERT_LAYER_FLOW = 2;
    public const int WINDIVERT_LAYER_SOCKET = 3;
    public const int WINDIVERT_LAYER_REFLECT = 4;

    public const ulong WINDIVERT_FLAG_SNIFF = 0x0001;
    public const ulong WINDIVERT_FLAG_DROP = 0x0002;
    public const ulong WINDIVERT_FLAG_RECV_ONLY = 0x0004;
    public const ulong WINDIVERT_FLAG_READ_ONLY = WINDIVERT_FLAG_RECV_ONLY;
    public const ulong WINDIVERT_FLAG_SEND_ONLY = 0x0008;
    public const ulong WINDIVERT_FLAG_WRITE_ONLY = WINDIVERT_FLAG_SEND_ONLY;
    public const ulong WINDIVERT_FLAG_NO_CHECKSUM = 0x0010;

    public const byte WINDIVERT_FLAG_OUTBOUND = 0x02;
    public const byte WINDIVERT_FLAG_LOOPBACK = 0x04;
    public const byte WINDIVERT_FLAG_IMPOSTOR = 0x08;
    public const byte WINDIVERT_FLAG_IPV6 = 0x10;

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct WINDIVERT_ADDRESS
    {
        [FieldOffset(0)] public long Timestamp;
        [FieldOffset(8)] public byte Layer;
        [FieldOffset(9)] public byte Event;
        [FieldOffset(10)] public byte Flags;
        [FieldOffset(11)] public byte Reserved1;
        [FieldOffset(12)] public uint Reserved2;

        // Network layer data
        [FieldOffset(16)] public uint IfIdx;
        [FieldOffset(20)] public uint SubIfIdx;

        // Flow layer data (when Layer == WINDIVERT_LAYER_FLOW)
        [FieldOffset(16)] public ulong EndpointId;
        [FieldOffset(24)] public ulong ParentEndpointId;
        [FieldOffset(32)] public uint ProcessId;
        [FieldOffset(36)] public fixed uint LocalAddr[4];
        [FieldOffset(52)] public fixed uint RemoteAddr[4];
        [FieldOffset(68)] public ushort LocalPort;
        [FieldOffset(70)] public ushort RemotePort;
        [FieldOffset(72)] public byte Protocol;

        public bool IsOutbound => (Flags & WINDIVERT_FLAG_OUTBOUND) != 0;
        public bool IsInbound => !IsOutbound;
        public bool IsLoopback => (Flags & WINDIVERT_FLAG_LOOPBACK) != 0;
        public bool IsIPv6 => (Flags & WINDIVERT_FLAG_IPV6) != 0;
    }

    [DllImport(DllName, SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "WinDivertOpen")]
    public static extern IntPtr WinDivertOpen(
        [MarshalAs(UnmanagedType.LPStr)] string filter,
        int layer,
        short priority,
        ulong flags);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertRecv")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertRecv(
        IntPtr handle,
        byte* pPacket,
        uint packetLen,
        out uint pRecvLen,
        ref WINDIVERT_ADDRESS pAddr);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertRecvEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertRecvEx(
        IntPtr handle,
        byte* pPacket,
        uint packetLen,
        out uint pRecvLen,
        ulong flags,
        ref WINDIVERT_ADDRESS pAddr,
        ref uint pAddrLen,
        NativeOverlapped* lpOverlapped);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertSend")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertSend(
        IntPtr handle,
        byte* pPacket,
        uint packetLen,
        out uint pSendLen,
        in WINDIVERT_ADDRESS pAddr);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertSendEx")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertSendEx(
        IntPtr handle,
        byte* pPacket,
        uint packetLen,
        out uint pSendLen,
        ulong flags,
        in WINDIVERT_ADDRESS pAddr,
        uint addrLen,
        NativeOverlapped* lpOverlapped);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertClose")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertClose(IntPtr handle);

    public const int WINDIVERT_PARAM_QUEUE_LEN = 0;
    public const int WINDIVERT_PARAM_QUEUE_TIME = 1;
    public const int WINDIVERT_PARAM_QUEUE_SIZE = 2;

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertSetParam")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertSetParam(
        IntPtr handle,
        int param,
        ulong value);

    [DllImport(DllName, SetLastError = true, EntryPoint = "WinDivertHelperCalcChecksums")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertHelperCalcChecksums(
        byte* pPacket,
        uint packetLen,
        ref WINDIVERT_ADDRESS pAddr,
        ulong flags);
}
