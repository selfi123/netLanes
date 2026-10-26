using NetLanes.Core.Models;
using NetLanes.NetworkEngine.FlowTracking;
using Xunit;

namespace NetLanes.Networking.Tests;

public class FlowCorrelationTests
{
    [Fact]
    public void FiveTuple_ReverseMatchesInboundFlow()
    {
        var outbound = new FiveTuple(NetProtocol.Tcp, "192.168.1.100", 54321, "93.184.216.34", 443);
        var reversed = outbound.Reverse();

        Assert.Equal(NetProtocol.Tcp, reversed.Protocol);
        Assert.Equal("93.184.216.34", reversed.LocalAddress);
        Assert.Equal(443, reversed.LocalPort);
        Assert.Equal("192.168.1.100", reversed.RemoteAddress);
        Assert.Equal(54321, reversed.RemotePort);
    }

    [Fact]
    public void FiveTuple_EqualityIsCaseInsensitive()
    {
        var tuple1 = new FiveTuple(NetProtocol.Tcp, "2001:DB8::1", 8080, "2001:DB8::2", 9090);
        var tuple2 = new FiveTuple(NetProtocol.Tcp, "2001:db8::1", 8080, "2001:db8::2", 9090);

        Assert.Equal(tuple1, tuple2);
        Assert.Equal(tuple1.GetHashCode(), tuple2.GetHashCode());
    }

    [Fact]
    public void FiveTuple_NormalizesIPv4MappedIPv6Address()
    {
        string mapped = "::ffff:192.168.1.50";
        string normalized = FiveTuple.NormalizeAddress(mapped);

        Assert.Equal("192.168.1.50", normalized);
    }

    [Fact]
    public void FlowTable_Lookup_MatchesInboundAndOutboundFlows()
    {
        var flowTable = new FlowTable();
        var tuple = new FiveTuple(NetProtocol.Tcp, "192.168.1.100", 49152, "142.250.190.46", 443);
        var owner = new FlowOwnerInfo(
            ProcessId: 1234,
            ExecutablePath: @"C:\Program Files\App\app.exe",
            CanonicalPath: @"c:\program files\app\app.exe",
            ProcessName: "app.exe",
            LastSeenUtc: DateTime.UtcNow);

        flowTable.RegisterFlow(tuple, owner);

        // Outbound packet: src is local 192.168.1.100:49152, dst is remote 142.250.190.46:443
        var matchOut = flowTable.Lookup(NetProtocol.Tcp, "192.168.1.100", 49152, "142.250.190.46", 443, isOutbound: true);
        Assert.NotNull(matchOut);
        Assert.Equal(1234, matchOut.ProcessId);

        // Inbound packet: src is remote 142.250.190.46:443, dst is local 192.168.1.100:49152
        var matchIn = flowTable.Lookup(NetProtocol.Tcp, "142.250.190.46", 443, "192.168.1.100", 49152, isOutbound: false);
        Assert.NotNull(matchIn);
        Assert.Equal(1234, matchIn.ProcessId);
    }
}
