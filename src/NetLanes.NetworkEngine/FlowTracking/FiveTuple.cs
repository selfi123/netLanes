using System.Net;
using NetLanes.Core.Models;

namespace NetLanes.NetworkEngine.FlowTracking;

public readonly struct FiveTuple : IEquatable<FiveTuple>
{
    public NetProtocol Protocol { get; }
    public string LocalAddress { get; }
    public int LocalPort { get; }
    public string RemoteAddress { get; }
    public int RemotePort { get; }

    public FiveTuple(NetProtocol protocol, string localAddress, int localPort, string remoteAddress, int remotePort)
    {
        Protocol = protocol;
        LocalAddress = NormalizeAddress(localAddress);
        LocalPort = localPort;
        RemoteAddress = NormalizeAddress(remoteAddress);
        RemotePort = remotePort;
    }

    public FiveTuple(NetProtocol protocol, IPAddress localAddress, int localPort, IPAddress remoteAddress, int remotePort)
        : this(protocol, localAddress.ToString(), localPort, remoteAddress.ToString(), remotePort)
    {
    }

    public FiveTuple Reverse()
    {
        return new FiveTuple(Protocol, RemoteAddress, RemotePort, LocalAddress, LocalPort);
    }

    public static string NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "0.0.0.0";
        if (IPAddress.TryParse(address, out var ip))
        {
            // Normalize IPv4-mapped IPv6 (::ffff:192.168.1.1) to standard IPv4
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }
            return ip.ToString();
        }
        return address.Trim().ToLowerInvariant();
    }

    public bool Equals(FiveTuple other)
    {
        return Protocol == other.Protocol &&
               LocalPort == other.LocalPort &&
               RemotePort == other.RemotePort &&
               string.Equals(LocalAddress, other.LocalAddress, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(RemoteAddress, other.RemoteAddress, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is FiveTuple other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            (byte)Protocol,
            StringComparer.OrdinalIgnoreCase.GetHashCode(LocalAddress),
            LocalPort,
            StringComparer.OrdinalIgnoreCase.GetHashCode(RemoteAddress),
            RemotePort);
    }

    public override string ToString() => $"{Protocol} {LocalAddress}:{LocalPort} -> {RemoteAddress}:{RemotePort}";
}
