using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;
using NetLanes.Infrastructure.Windows;
using NetLanes.NetworkEngine.Interop;
using NetLanes.NetworkEngine.TrafficShaping;

namespace NetLanes.NetworkEngine.FlowTracking;

public class NetworkFlowMonitor : IFlowMonitor
{
    private static readonly IntPtr InvalidHandle = new(-1);

    private readonly IProcessDiscoveryService _processDiscovery;
    private readonly FlowTable _flowTable;
    private readonly IDriverManager _driverManager;
    private readonly ILogger<NetworkFlowMonitor>? _logger;

    private readonly ConcurrentDictionary<string, FlowSessionTracker> _activeFlows = new();
    private readonly ConcurrentDictionary<string, AppTransferTracker> _appTrackers = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private Task? _sniffTask;
    private IntPtr _sniffHandle = InvalidHandle;

    private bool _isMonitoring;
    private bool _isElevationRequired;
    private volatile bool _isSniffingPaused;

    private double _globalDownRate;
    private double _globalUpRate;

    // Interface fallback tracking
    private long _prevAdapterBytesRecv;
    private long _prevAdapterBytesSent;
    private DateTime _prevAdapterTime = DateTime.UtcNow;
    private readonly RateSmoother _globalDownSmoother = new(0.35);
    private readonly RateSmoother _globalUpSmoother = new(0.35);

    public event Action<IReadOnlyList<NetworkFlow>>? FlowsUpdated;
    public bool IsMonitoring => _isMonitoring;
    public bool IsElevationRequired => _isElevationRequired;
    public double GlobalDownloadRateBps => _globalDownRate;
    public double GlobalUploadRateBps => _globalUpRate;

    private class FlowSessionTracker
    {
        public required NetworkFlow Flow { get; set; }
        public long FlowBytesDown;
        public long FlowBytesUp;
        public RateSmoother DownloadSmoother { get; } = new(0.35);
        public RateSmoother UploadSmoother { get; } = new(0.35);
        public long PreviousBytesDown { get; set; }
        public long PreviousBytesUp { get; set; }
        public DateTime LastSampleTime { get; set; } = DateTime.UtcNow;
    }

    private class AppTransferTracker
    {
        public long TotalBytesDownloaded;
        public long TotalBytesUploaded;
        public long PreviousBytesDown;
        public long PreviousBytesUp;
        public double CurrentDownRate;
        public double CurrentUpRate;
        public RateSmoother DownSmoother { get; } = new(0.35);
        public RateSmoother UpSmoother { get; } = new(0.35);
        public DateTime LastSampleTime { get; set; } = DateTime.UtcNow;
        public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;
    }

    public NetworkFlowMonitor(
        IProcessDiscoveryService processDiscovery,
        FlowTable flowTable,
        IDriverManager driverManager,
        ILogger<NetworkFlowMonitor>? logger = null)
    {
        _processDiscovery = processDiscovery;
        _flowTable = flowTable;
        _driverManager = driverManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_isMonitoring) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            _isMonitoring = true;
            _isElevationRequired = !_driverManager.IsAdministrator;

            // Start polling sockets & calculating rates
            _monitorTask = Task.Run(() => MonitorLoopAsync(_cts.Token));

            // If elevated, start packet sniffing to capture live packet byte counts
            if (_driverManager.IsAdministrator)
            {
                StartSniffingInternal();
            }
            else
            {
                _logger?.LogInformation("Running NetworkFlowMonitor in unprivileged mode. WinDivert packet sniffing requires Administrator.");
            }

            _logger?.LogInformation("Network flow monitor started");
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Task? monitorTaskToWait = null;
        Task? sniffTaskToWait = null;

        lock (_lock)
        {
            if (!_isMonitoring) return;
            _isMonitoring = false;
            _cts?.Cancel();

            StopSniffingInternal();

            monitorTaskToWait = _monitorTask;
            sniffTaskToWait = _sniffTask;
        }

        if (monitorTaskToWait != null)
        {
            try { await monitorTaskToWait.WaitAsync(TimeSpan.FromSeconds(2), ct); } catch { }
        }
        if (sniffTaskToWait != null)
        {
            try { await sniffTaskToWait.WaitAsync(TimeSpan.FromSeconds(2), ct); } catch { }
        }

        _logger?.LogInformation("Network flow monitor stopped");
    }

    public void PauseSniffing()
    {
        _isSniffingPaused = true;
        StopSniffingInternal();
        _logger?.LogDebug("Packet sniffing paused (traffic shaping active)");
    }

    public void ResumeSniffing()
    {
        _isSniffingPaused = false;
        if (_driverManager.IsAdministrator && _isMonitoring)
        {
            StartSniffingInternal();
            _logger?.LogDebug("Packet sniffing resumed");
        }
    }

    public IReadOnlyList<NetworkFlow> GetActiveFlows()
    {
        return _activeFlows.Values.Select(v => v.Flow).ToList();
    }

    public (double downRate, double upRate, long totalDown, long totalUp) GetAppTransferStats(string canonicalPath)
    {
        if (!string.IsNullOrEmpty(canonicalPath) && _appTrackers.TryGetValue(canonicalPath, out var trk))
        {
            return (
                trk.CurrentDownRate,
                trk.CurrentUpRate,
                Interlocked.Read(ref trk.TotalBytesDownloaded),
                Interlocked.Read(ref trk.TotalBytesUploaded)
            );
        }
        return (0, 0, 0, 0);
    }

    public void RecordAppTransfer(string canonicalPath, long byteCount, bool isOutbound)
    {
        if (string.IsNullOrEmpty(canonicalPath) || byteCount <= 0) return;

        var tracker = _appTrackers.GetOrAdd(canonicalPath, _ => new AppTransferTracker());
        if (isOutbound)
        {
            Interlocked.Add(ref tracker.TotalBytesUploaded, byteCount);
        }
        else
        {
            Interlocked.Add(ref tracker.TotalBytesDownloaded, byteCount);
        }
        tracker.LastActiveUtc = DateTime.UtcNow;
    }

    public void RecordPacketTransfer(string flowId, long byteCount, bool isOutbound)
    {
        if (string.IsNullOrEmpty(flowId) || byteCount <= 0) return;

        if (_activeFlows.TryGetValue(flowId, out var tracker))
        {
            if (isOutbound)
            {
                long val = Interlocked.Add(ref tracker.FlowBytesUp, byteCount);
                tracker.Flow.TotalBytesUploaded = val;
            }
            else
            {
                long val = Interlocked.Add(ref tracker.FlowBytesDown, byteCount);
                tracker.Flow.TotalBytesDownloaded = val;
            }
            tracker.Flow.LastActiveUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(tracker.Flow.CanonicalPath))
            {
                RecordAppTransfer(tracker.Flow.CanonicalPath, byteCount, isOutbound);
            }
        }
    }

    public bool TryRecordPacketTransfer(string flowId, long byteCount, bool isOutbound)
    {
        if (string.IsNullOrEmpty(flowId) || byteCount <= 0) return false;

        if (_activeFlows.TryGetValue(flowId, out var tracker))
        {
            if (isOutbound)
            {
                long val = Interlocked.Add(ref tracker.FlowBytesUp, byteCount);
                tracker.Flow.TotalBytesUploaded = val;
            }
            else
            {
                long val = Interlocked.Add(ref tracker.FlowBytesDown, byteCount);
                tracker.Flow.TotalBytesDownloaded = val;
            }
            tracker.Flow.LastActiveUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(tracker.Flow.CanonicalPath))
            {
                RecordAppTransfer(tracker.Flow.CanonicalPath, byteCount, isOutbound);
            }
            return true;
        }

        return false;
    }

    private void StartSniffingInternal()
    {
        if (_isSniffingPaused) return;

        try
        {
            // WinDivert Sniff mode: copies packets without diverting, delaying, or altering them
            string filter = "!loopback and (ip or ipv6) and (tcp or udp)";
            _sniffHandle = WinDivertNative.WinDivertOpen(
                filter,
                WinDivertNative.WINDIVERT_LAYER_NETWORK,
                priority: -1000, // low priority sniff
                flags: WinDivertNative.WINDIVERT_FLAG_SNIFF | WinDivertNative.WINDIVERT_FLAG_RECV_ONLY);

            if (_sniffHandle != InvalidHandle && _sniffHandle != IntPtr.Zero)
            {
                WinDivertNative.WinDivertSetParam(_sniffHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_LEN, 16384);
                WinDivertNative.WinDivertSetParam(_sniffHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_TIME, 4000);
                WinDivertNative.WinDivertSetParam(_sniffHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_SIZE, 33554432); // 32MB

                _sniffTask = Task.Run(() => SniffLoopAsync(_cts!.Token));
                _logger?.LogInformation("WinDivert packet sniffing active (16K queue / 32MB buffer)");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                _sniffHandle = InvalidHandle;
                _logger?.LogWarning("Could not open WinDivert sniff handle (Win32 Error: {Error})", err);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start WinDivert packet sniffer");
        }
    }

    private void StopSniffingInternal()
    {
        if (_sniffHandle != InvalidHandle && _sniffHandle != IntPtr.Zero)
        {
            WinDivertNative.WinDivertClose(_sniffHandle);
            _sniffHandle = InvalidHandle;
        }
    }

    private unsafe void SniffLoopAsync(CancellationToken ct)
    {
        const int BufferSize = 65535;
        byte[] buffer = new byte[BufferSize];

        fixed (byte* pBuffer = buffer)
        {
            while (!ct.IsCancellationRequested && _isMonitoring && _sniffHandle != InvalidHandle)
            {
                if (_isSniffingPaused)
                {
                    Thread.Sleep(50);
                    continue;
                }

                var addr = new WinDivertNative.WINDIVERT_ADDRESS();
                if (!WinDivertNative.WinDivertRecv(_sniffHandle, pBuffer, (uint)BufferSize, out uint recvLen, ref addr))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 995 /* ERROR_OPERATION_ABORTED */ || err == 6 /* ERROR_INVALID_HANDLE */)
                    {
                        break;
                    }
                    continue;
                }

                if (recvLen == 0) continue;

                // Parse packet header
                var packetSpan = new ReadOnlySpan<byte>(pBuffer, (int)recvLen);
                if (PacketParser.TryParse(packetSpan, out var header))
                {
                    bool isOutbound = addr.IsOutbound;
                    string localAddr = isOutbound ? header.SourceAddress : header.DestinationAddress;
                    int localPort = isOutbound ? header.SourcePort : header.DestinationPort;
                    string remoteAddr = isOutbound ? header.DestinationAddress : header.SourceAddress;
                    int remotePort = isOutbound ? header.DestinationPort : header.SourcePort;

                    string flowId = NetworkFlow.BuildFlowId(header.Protocol, localAddr, localPort, remoteAddr, remotePort);
                    bool matched = TryRecordPacketTransfer(flowId, (long)recvLen, isOutbound);

                    if (!matched)
                    {
                        var flowOwner = _flowTable.Lookup(
                            header.Protocol,
                            header.SourceAddress,
                            header.SourcePort,
                            header.DestinationAddress,
                            header.DestinationPort,
                            isOutbound);

                        if (flowOwner != null && !string.IsNullOrEmpty(flowOwner.CanonicalPath))
                        {
                            RecordAppTransfer(flowOwner.CanonicalPath, (long)recvLen, isOutbound);
                        }
                    }
                }
            }
        }
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                PollSockets();
                await Task.Delay(250, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error in network flow monitor cycle");
                await Task.Delay(1500, ct);
            }
        }
    }

    private void PollSockets()
    {
        var sockets = IpHlpApi.GetAllActiveSockets();
        _flowTable.UpdateFromActiveSockets(sockets, _processDiscovery);

        var now = DateTime.UtcNow;
        var currentKeys = new HashSet<string>(sockets.Count);

        // 1. Calculate per-flow rates
        foreach (var tracker in _activeFlows.Values)
        {
            double flowElapsed = (now - tracker.LastSampleTime).TotalSeconds;
            if (flowElapsed >= 0.5)
            {
                long flowDown = Interlocked.Read(ref tracker.FlowBytesDown);
                long flowUp = Interlocked.Read(ref tracker.FlowBytesUp);
                tracker.Flow.TotalBytesDownloaded = flowDown;
                tracker.Flow.TotalBytesUploaded = flowUp;

                long downDelta = flowDown - tracker.PreviousBytesDown;
                long upDelta = flowUp - tracker.PreviousBytesUp;

                double instDown = Math.Max(0, downDelta / flowElapsed);
                double instUp = Math.Max(0, upDelta / flowElapsed);

                tracker.Flow.DownloadRateBps = tracker.DownloadSmoother.Sample(instDown);
                tracker.Flow.UploadRateBps = tracker.UploadSmoother.Sample(instUp);

                tracker.PreviousBytesDown = flowDown;
                tracker.PreviousBytesUp = flowUp;
                tracker.LastSampleTime = now;
            }
        }

        // 2. Update app tracker rates
        double totalAppDown = 0;
        double totalAppUp = 0;
        foreach (var (appPath, tracker) in _appTrackers)
        {
            double elapsed = (now - tracker.LastSampleTime).TotalSeconds;
            if (elapsed >= 0.5)
            {
                long downDelta = Interlocked.Read(ref tracker.TotalBytesDownloaded) - tracker.PreviousBytesDown;
                long upDelta = Interlocked.Read(ref tracker.TotalBytesUploaded) - tracker.PreviousBytesUp;

                double instDown = Math.Max(0, downDelta / elapsed);
                double instUp = Math.Max(0, upDelta / elapsed);

                tracker.CurrentDownRate = tracker.DownSmoother.Sample(instDown);
                tracker.CurrentUpRate = tracker.UpSmoother.Sample(instUp);

                tracker.PreviousBytesDown = Interlocked.Read(ref tracker.TotalBytesDownloaded);
                tracker.PreviousBytesUp = Interlocked.Read(ref tracker.TotalBytesUploaded);
                tracker.LastSampleTime = now;
            }

            totalAppDown += tracker.CurrentDownRate;
            totalAppUp += tracker.CurrentUpRate;
        }

        // 3. Update global rates: always sample interface stats to ensure full throughput capture
        SampleGlobalInterfaceStats(now);
        _globalDownRate = Math.Max(totalAppDown, _globalDownSmoother.CurrentRate);
        _globalUpRate = Math.Max(totalAppUp, _globalUpSmoother.CurrentRate);

        // 4. Update socket flow table
        foreach (var s in sockets)
        {
            string flowId = NetworkFlow.BuildFlowId(
                s.Protocol,
                s.LocalAddress.ToString(),
                s.LocalPort,
                s.RemoteAddress.ToString(),
                s.RemotePort);

            currentKeys.Add(flowId);

            _activeFlows.AddOrUpdate(
                flowId,
                id =>
                {
                    var identity = _processDiscovery.ResolveProcess(s.OwningPid);
                    string? canonical = identity?.CanonicalPath;

                    return new FlowSessionTracker
                    {
                        Flow = new NetworkFlow
                        {
                            FlowId = id,
                            Protocol = s.Protocol,
                            LocalAddress = s.LocalAddress.ToString(),
                            LocalPort = s.LocalPort,
                            RemoteAddress = s.RemoteAddress.ToString(),
                            RemotePort = s.RemotePort,
                            ProcessId = s.OwningPid,
                            ProcessName = identity?.ProcessName ?? (s.OwningPid > 0 ? $"PID {s.OwningPid}" : "Unknown"),
                            ExecutablePath = identity?.ExecutablePath,
                            CanonicalPath = canonical,
                            State = s.State,
                            DownloadRateBps = 0,
                            UploadRateBps = 0,
                            TotalBytesDownloaded = 0,
                            TotalBytesUploaded = 0,
                            FirstSeenUtc = now,
                            LastActiveUtc = now
                        },
                        LastSampleTime = now
                    };
                },
                (id, tracker) =>
                {
                    tracker.Flow.State = s.State;
                    tracker.Flow.LastActiveUtc = now;
                    return tracker;
                });
        }

        // Evict flows inactive for over 15 seconds
        var purgeCutoff = now.AddSeconds(-15);
        foreach (var kvp in _activeFlows)
        {
            if (kvp.Value.Flow.LastActiveUtc < purgeCutoff && !currentKeys.Contains(kvp.Key))
            {
                _activeFlows.TryRemove(kvp.Key, out _);
            }
        }

        var snapshot = _activeFlows.Values.Select(v => v.Flow).ToList();
        FlowUpdated(snapshot);
    }

    private void SampleGlobalInterfaceStats(DateTime now)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            long totalRecv = 0;
            long totalSent = 0;
            foreach (var nic in interfaces)
            {
                var stats = nic.GetIPv4Statistics();
                totalRecv += stats.BytesReceived;
                totalSent += stats.BytesSent;
            }

            double elapsed = (now - _prevAdapterTime).TotalSeconds;
            if (elapsed >= 0.5 && _prevAdapterBytesRecv > 0)
            {
                double instDown = Math.Max(0, (totalRecv - _prevAdapterBytesRecv) / elapsed);
                double instUp = Math.Max(0, (totalSent - _prevAdapterBytesSent) / elapsed);

                _globalDownSmoother.Sample(instDown);
                _globalUpSmoother.Sample(instUp);
            }

            _prevAdapterBytesRecv = totalRecv;
            _prevAdapterBytesSent = totalSent;
            _prevAdapterTime = now;
        }
        catch
        {
            // Ignore stats sampling issues
        }
    }

    private void FlowUpdated(IReadOnlyList<NetworkFlow> flows)
    {
        try
        {
            FlowsUpdated?.Invoke(flows);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error dispatching FlowsUpdated event");
        }
    }
}
