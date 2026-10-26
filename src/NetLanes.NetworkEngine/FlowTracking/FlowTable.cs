using System.Collections.Concurrent;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Infrastructure.Windows;

namespace NetLanes.NetworkEngine.FlowTracking;

public record FlowOwnerInfo(
    int ProcessId,
    string? ExecutablePath,
    string? CanonicalPath,
    string ProcessName,
    DateTime LastSeenUtc);

public class FlowTable
{
    private readonly ConcurrentDictionary<FiveTuple, FlowOwnerInfo> _exactFlows = new();
    private readonly ConcurrentDictionary<(NetProtocol Protocol, int LocalPort), FlowOwnerInfo> _localPortFlows = new();

    public int Count => _exactFlows.Count;

    public void UpdateFromActiveSockets(IEnumerable<IpHlpApi.SocketEndpointInfo> sockets, IProcessDiscoveryService processDiscovery)
    {
        var now = DateTime.UtcNow;

        foreach (var socket in sockets)
        {
            if (socket.OwningPid <= 0) continue;

            var identity = processDiscovery.ResolveProcess(socket.OwningPid);
            string procName = identity?.ProcessName ?? $"PID {socket.OwningPid}";
            string? exePath = identity?.ExecutablePath;
            string? canonical = identity?.CanonicalPath;

            var owner = new FlowOwnerInfo(
                socket.OwningPid,
                exePath,
                canonical,
                procName,
                now);

            // Record exact 5-tuple
            var tuple = new FiveTuple(
                socket.Protocol,
                socket.LocalAddress,
                socket.LocalPort,
                socket.RemoteAddress,
                socket.RemotePort);

            _exactFlows[tuple] = owner;

            // Record local port mapping (for UDP or non-connected TCP sockets)
            if (socket.LocalPort > 0)
            {
                _localPortFlows[(socket.Protocol, socket.LocalPort)] = owner;
            }
        }

        // Clean up stale flows older than 30 seconds
        CleanupStaleEntries(now);
    }

    public FlowOwnerInfo? Lookup(NetProtocol protocol, string srcIp, int srcPort, string dstIp, int dstPort, bool isOutbound)
    {
        FiveTuple primaryTuple;
        int localPort;

        if (isOutbound)
        {
            // Outbound: src is local, dst is remote
            primaryTuple = new FiveTuple(protocol, srcIp, srcPort, dstIp, dstPort);
            localPort = srcPort;
        }
        else
        {
            // Inbound: dst is local host, src is remote sender
            primaryTuple = new FiveTuple(protocol, dstIp, dstPort, srcIp, srcPort);
            localPort = dstPort;
        }

        if (_exactFlows.TryGetValue(primaryTuple, out var owner))
        {
            return owner;
        }

        // Fallback: match by local protocol & port
        if (_localPortFlows.TryGetValue((protocol, localPort), out var portOwner))
        {
            return portOwner;
        }

        return null;
    }

    public void RegisterFlow(FiveTuple tuple, FlowOwnerInfo owner)
    {
        _exactFlows[tuple] = owner;
        if (tuple.LocalPort > 0)
        {
            _localPortFlows[(tuple.Protocol, tuple.LocalPort)] = owner;
        }
    }

    private void CleanupStaleEntries(DateTime now)
    {
        var cutoff = now.AddSeconds(-30);

        foreach (var kvp in _exactFlows)
        {
            if (kvp.Value.LastSeenUtc < cutoff)
            {
                _exactFlows.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _localPortFlows)
        {
            if (kvp.Value.LastSeenUtc < cutoff)
            {
                _localPortFlows.TryRemove(kvp.Key, out _);
            }
        }
    }

    public void Clear()
    {
        _exactFlows.Clear();
        _localPortFlows.Clear();
    }
}
