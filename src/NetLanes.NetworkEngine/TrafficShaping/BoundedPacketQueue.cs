using System.Collections.Concurrent;
using NetLanes.NetworkEngine.Interop;

namespace NetLanes.NetworkEngine.TrafficShaping;

public class QueuedPacket
{
    public required byte[] Data { get; init; }
    public required WinDivertNative.WINDIVERT_ADDRESS Address { get; init; }
    public required DateTime EnqueuedAtUtc { get; init; }
    public required string CanonicalAppPath { get; init; }
    public required bool IsOutbound { get; init; }
    public int Length => Data.Length;
}

public class BoundedPacketQueue
{
    private readonly ConcurrentQueue<QueuedPacket> _queue = new();
    private readonly int _maxPackets;
    private readonly long _maxBytes;
    private long _currentBytes;

    public int Count => _queue.Count;
    public long CurrentBytes => Interlocked.Read(ref _currentBytes);

    public BoundedPacketQueue(int maxPackets = 2000, long maxBytes = 4 * 1024 * 1024)
    {
        _maxPackets = Math.Max(1, maxPackets);
        _maxBytes = Math.Max(1, maxBytes);
    }

    public bool TryEnqueue(QueuedPacket packet, out bool isOverflow)
    {
        if (_queue.Count >= _maxPackets || (CurrentBytes + packet.Length) > _maxBytes)
        {
            isOverflow = true;
            return false;
        }

        _queue.Enqueue(packet);
        Interlocked.Add(ref _currentBytes, packet.Length);
        isOverflow = false;
        return true;
    }

    public bool TryDequeue(out QueuedPacket? packet)
    {
        if (_queue.TryDequeue(out packet))
        {
            Interlocked.Add(ref _currentBytes, -packet.Length);
            return true;
        }

        packet = null;
        return false;
    }

    public List<QueuedPacket> FlushAll()
    {
        var flushed = new List<QueuedPacket>(_queue.Count);
        while (_queue.TryDequeue(out var packet))
        {
            Interlocked.Add(ref _currentBytes, -packet.Length);
            flushed.Add(packet);
        }
        return flushed;
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _currentBytes, 0);
    }
}
