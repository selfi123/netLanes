using NetLanes.NetworkEngine.Interop;
using NetLanes.NetworkEngine.TrafficShaping;
using Xunit;

namespace NetLanes.Networking.Tests;

public class BoundedQueueTests
{
    [Fact]
    public void Queue_RespectsMaxPacketBoundAndSignalsOverflow()
    {
        int maxPackets = 5;
        var queue = new BoundedPacketQueue(maxPackets: maxPackets, maxBytes: 1024 * 1024);

        for (int i = 0; i < maxPackets; i++)
        {
            var packet = new QueuedPacket
            {
                Data = new byte[100],
                Address = default,
                EnqueuedAtUtc = DateTime.UtcNow,
                CanonicalAppPath = "test.exe",
                IsOutbound = true
            };

            bool ok = queue.TryEnqueue(packet, out bool isOverflow);
            Assert.True(ok);
            Assert.False(isOverflow);
        }

        Assert.Equal(5, queue.Count);
        Assert.Equal(500, queue.CurrentBytes);

        // Next packet should trigger overflow
        var overflowPacket = new QueuedPacket
        {
            Data = new byte[100],
            Address = default,
            EnqueuedAtUtc = DateTime.UtcNow,
            CanonicalAppPath = "test.exe",
            IsOutbound = true
        };

        bool accepted = queue.TryEnqueue(overflowPacket, out bool overflowFlag);
        Assert.False(accepted);
        Assert.True(overflowFlag);
    }

    [Fact]
    public void Queue_RespectsMaxByteBound()
    {
        long maxBytes = 1000;
        var queue = new BoundedPacketQueue(maxPackets: 100, maxBytes: maxBytes);

        var p1 = new QueuedPacket
        {
            Data = new byte[800],
            Address = default,
            EnqueuedAtUtc = DateTime.UtcNow,
            CanonicalAppPath = "test.exe",
            IsOutbound = true
        };

        Assert.True(queue.TryEnqueue(p1, out _));

        var p2 = new QueuedPacket
        {
            Data = new byte[300], // Would exceed 1000 bytes
            Address = default,
            EnqueuedAtUtc = DateTime.UtcNow,
            CanonicalAppPath = "test.exe",
            IsOutbound = true
        };

        bool ok = queue.TryEnqueue(p2, out bool overflow);
        Assert.False(ok);
        Assert.True(overflow);
    }

    [Fact]
    public void FlushAll_EmptiesQueueAndResetsByteCount()
    {
        var queue = new BoundedPacketQueue(maxPackets: 10, maxBytes: 10000);
        for (int i = 0; i < 4; i++)
        {
            queue.TryEnqueue(new QueuedPacket
            {
                Data = new byte[50],
                Address = default,
                EnqueuedAtUtc = DateTime.UtcNow,
                CanonicalAppPath = "test.exe",
                IsOutbound = true
            }, out _);
        }

        Assert.Equal(4, queue.Count);
        Assert.Equal(200, queue.CurrentBytes);

        var flushed = queue.FlushAll();
        Assert.Equal(4, flushed.Count);
        Assert.Equal(0, queue.Count);
        Assert.Equal(0, queue.CurrentBytes);
    }
}
