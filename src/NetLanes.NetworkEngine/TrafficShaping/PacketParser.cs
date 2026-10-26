using System.Net;
using NetLanes.Core.Models;

namespace NetLanes.NetworkEngine.TrafficShaping;

public ref struct ParsedPacketHeader
{
    public bool IsValid;
    public NetProtocol Protocol;
    public string SourceAddress;
    public int SourcePort;
    public string DestinationAddress;
    public int DestinationPort;
    public int L4HeaderOffset;
    public int PayloadLength;
}

public static class PacketParser
{
    public static bool TryParse(ReadOnlySpan<byte> packet, out ParsedPacketHeader header)
    {
        header = default;
        if (packet.Length < 20) return false;

        byte version = (byte)(packet[0] >> 4);
        if (version == 4)
        {
            return TryParseIPv4(packet, out header);
        }
        if (version == 6)
        {
            return TryParseIPv6(packet, out header);
        }

        return false;
    }

    private static bool TryParseIPv4(ReadOnlySpan<byte> packet, out ParsedPacketHeader header)
    {
        header = default;
        int ihl = (packet[0] & 0x0F) * 4;
        if (ihl < 20 || packet.Length < ihl) return false;

        byte protocolByte = packet[9];
        NetProtocol protocol = protocolByte switch
        {
            6 => NetProtocol.Tcp,
            17 => NetProtocol.Udp,
            _ => NetProtocol.Unknown
        };

        if (protocol == NetProtocol.Unknown) return false;

        var srcIp = new IPAddress(packet.Slice(12, 4));
        var dstIp = new IPAddress(packet.Slice(16, 4));

        int l4Offset = ihl;
        if (packet.Length < l4Offset + 4) return false;

        int srcPort = (packet[l4Offset] << 8) | packet[l4Offset + 1];
        int dstPort = (packet[l4Offset + 2] << 8) | packet[l4Offset + 3];

        header = new ParsedPacketHeader
        {
            IsValid = true,
            Protocol = protocol,
            SourceAddress = srcIp.ToString(),
            SourcePort = srcPort,
            DestinationAddress = dstIp.ToString(),
            DestinationPort = dstPort,
            L4HeaderOffset = l4Offset,
            PayloadLength = packet.Length - l4Offset
        };

        return true;
    }

    private static bool TryParseIPv6(ReadOnlySpan<byte> packet, out ParsedPacketHeader header)
    {
        header = default;
        if (packet.Length < 40) return false;

        byte nextHeader = packet[6];
        NetProtocol protocol = nextHeader switch
        {
            6 => NetProtocol.Tcp,
            17 => NetProtocol.Udp,
            _ => NetProtocol.Unknown
        };

        if (protocol == NetProtocol.Unknown) return false;

        var srcIp = new IPAddress(packet.Slice(8, 16));
        var dstIp = new IPAddress(packet.Slice(24, 16));

        int l4Offset = 40;
        if (packet.Length < l4Offset + 4) return false;

        int srcPort = (packet[l4Offset] << 8) | packet[l4Offset + 1];
        int dstPort = (packet[l4Offset + 2] << 8) | packet[l4Offset + 3];

        header = new ParsedPacketHeader
        {
            IsValid = true,
            Protocol = protocol,
            SourceAddress = srcIp.ToString(),
            SourcePort = srcPort,
            DestinationAddress = dstIp.ToString(),
            DestinationPort = dstPort,
            L4HeaderOffset = l4Offset,
            PayloadLength = packet.Length - l4Offset
        };

        return true;
    }
}
