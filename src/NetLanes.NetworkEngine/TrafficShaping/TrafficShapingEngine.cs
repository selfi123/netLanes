using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.RateLimiting;
using NetLanes.Core.Utilities;
using NetLanes.NetworkEngine.DriverManagement;
using NetLanes.NetworkEngine.FlowTracking;
using NetLanes.NetworkEngine.Interop;

namespace NetLanes.NetworkEngine.TrafficShaping;

public class TrafficShapingEngine : ITrafficShaper
{
    private static readonly IntPtr InvalidHandle = new(-1);

    private readonly FlowTable _flowTable;
    private readonly IDriverManager _driverManager;
    private readonly IFlowMonitor? _flowMonitor;
    private readonly ILogger<TrafficShapingEngine>? _logger;

    private readonly object _syncLock = new();
    private IntPtr _divertHandle = InvalidHandle;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private Task? _pacingTask;

    private volatile bool _isShaping;
    private volatile bool _isPaused;
    private volatile bool _isEmergencyStopped;

    private readonly BoundedPacketQueue _inboundQueue;
    private readonly BoundedPacketQueue _outboundQueue;

    // Rate limiters per canonical executable path
    private readonly ConcurrentDictionary<string, ApplicationPacer> _applicationPacers = new(StringComparer.OrdinalIgnoreCase);

    // Active rules cache
    private readonly ConcurrentDictionary<string, ApplicationRule> _rulesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, Lane> _lanesById = new();

    private long _totalPacketsCaptured;
    private long _totalPacketsReinjected;
    private long _totalPacketsDropped;
    private long _totalPacketsBypassed;
    private EnforcementState _currentState = EnforcementState.Inactive;
    private string? _lastStatusMessage = "Ready";

    public EnforcementStatus Status => GetCurrentStatus();
    public event Action<EnforcementStatus>? StatusChanged;

    private class ApplicationPacer
    {
        public required TokenBucketRateLimiter InboundLimiter { get; set; }
        public required TokenBucketRateLimiter OutboundLimiter { get; set; }
    }

    public TrafficShapingEngine(
        FlowTable flowTable,
        IDriverManager driverManager,
        IFlowMonitor? flowMonitor = null,
        int maxQueuePackets = 2000,
        long maxQueueBytes = 4 * 1024 * 1024,
        ILogger<TrafficShapingEngine>? logger = null)
    {
        _flowTable = flowTable;
        _driverManager = driverManager;
        _flowMonitor = flowMonitor;
        _logger = logger;
        _inboundQueue = new BoundedPacketQueue(maxQueuePackets, maxQueueBytes);
        _outboundQueue = new BoundedPacketQueue(maxQueuePackets, maxQueueBytes);
    }

    public void UpdateRules(IReadOnlyList<ApplicationRule> rules, IReadOnlyList<Lane> lanes)
    {
        _rulesByPath.Clear();
        foreach (var rule in rules)
        {
            if (!string.IsNullOrEmpty(rule.CanonicalExecutablePath))
            {
                _rulesByPath[rule.CanonicalExecutablePath] = rule;
            }
        }

        _lanesById.Clear();
        foreach (var lane in lanes)
        {
            _lanesById[lane.Id] = lane;
        }

        // Refresh existing pacers with newly configured rates
        foreach (var (path, pacer) in _applicationPacers)
        {
            var (downLimit, upLimit) = ResolveLimitsForApp(path);
            pacer.InboundLimiter.UpdateRate(downLimit ?? 0);
            pacer.OutboundLimiter.UpdateRate(upLimit ?? 0);
        }

        _logger?.LogInformation("Traffic shaping rules updated ({RuleCount} rules, {LaneCount} lanes)", rules.Count, lanes.Count);
        RaiseStatusChanged();
    }

    public Task StartShapingAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_isShaping) return Task.CompletedTask;

            if (!_driverManager.IsAdministrator)
            {
                _currentState = EnforcementState.ElevationRequired;
                _lastStatusMessage = "Administrator rights required to open packet interception driver.";
                _logger?.LogWarning("Cannot start traffic shaping: Elevation required");
                RaiseStatusChanged();
                return Task.CompletedTask;
            }

            _isEmergencyStopped = false;
            _isPaused = false;

            // Filter: non-loopback IPv4/IPv6 TCP and UDP packets
            string filter = "!loopback and (ip or ipv6) and (tcp or udp)";
            _divertHandle = WinDivertNative.WinDivertOpen(
                filter,
                WinDivertNative.WINDIVERT_LAYER_NETWORK,
                priority: 0,
                flags: 0);

            if (_divertHandle == InvalidHandle || _divertHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                _divertHandle = InvalidHandle;
                _currentState = error == 5 ? EnforcementState.ElevationRequired : EnforcementState.DriverUnavailable;
                _lastStatusMessage = $"WinDivertOpen failed (Error {error}): {GetWin32ErrorMessage(error)}";
                _logger?.LogError("Failed to open WinDivert handle. Error code: {ErrorCode}", error);
                RaiseStatusChanged();
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _isShaping = true;
            _flowMonitor?.PauseSniffing();

            // Configure maximum queue buffer to prevent packet dropping at gigabit speeds
            WinDivertNative.WinDivertSetParam(_divertHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_LEN, 16384);
            WinDivertNative.WinDivertSetParam(_divertHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_TIME, 4000);
            WinDivertNative.WinDivertSetParam(_divertHandle, WinDivertNative.WINDIVERT_PARAM_QUEUE_SIZE, 33554432); // 32MB

            _currentState = EnforcementState.ActiveShaping;
            _lastStatusMessage = "Active Traffic Control running";

            _captureTask = Task.Run(() => CaptureLoopAsync(_cts.Token));
            _pacingTask = Task.Run(() => PacingLoopAsync(_cts.Token));

            _logger?.LogInformation("Traffic shaping engine started successfully");
            RaiseStatusChanged();
        }

        return Task.CompletedTask;
    }

    public async Task StopShapingAsync(CancellationToken ct = default)
    {
        Task? captureTaskToWait = null;
        Task? pacingTaskToWait = null;

        lock (_syncLock)
        {
            if (!_isShaping) return;

            _isShaping = false;
            _cts?.Cancel();
            captureTaskToWait = _captureTask;
            pacingTaskToWait = _pacingTask;

            CloseHandleInternal();
            FlushQueuesImmediately();

            _currentState = EnforcementState.Inactive;
            _lastStatusMessage = "Traffic control stopped. Normal traffic restored.";
            _flowMonitor?.ResumeSniffing();
        }

        if (captureTaskToWait != null)
        {
            try { await captureTaskToWait.WaitAsync(TimeSpan.FromSeconds(2), ct); } catch { }
        }
        if (pacingTaskToWait != null)
        {
            try { await pacingTaskToWait.WaitAsync(TimeSpan.FromSeconds(2), ct); } catch { }
        }

        _logger?.LogInformation("Traffic shaping engine stopped cleanly");
        RaiseStatusChanged();
    }

    public Task PauseShapingAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (!_isShaping) return Task.CompletedTask;
            _isPaused = true;
            _currentState = EnforcementState.Paused;
            _lastStatusMessage = "Traffic control paused (Packets passing unshaped)";
            FlushQueuesImmediately();
        }

        _logger?.LogInformation("Traffic control paused");
        RaiseStatusChanged();
        return Task.CompletedTask;
    }

    public Task ResumeShapingAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (!_isShaping) return Task.CompletedTask;
            _isPaused = false;
            _currentState = EnforcementState.ActiveShaping;
            _lastStatusMessage = "Active Traffic Control running";
        }

        _logger?.LogInformation("Traffic control resumed");
        RaiseStatusChanged();
        return Task.CompletedTask;
    }

    public void TriggerEmergencyStop()
    {
        lock (_syncLock)
        {
            _isEmergencyStopped = true;
            _isShaping = false;
            _cts?.Cancel();

            _logger?.LogWarning("EMERGENCY STOP TRIGGERED: Flushing all queues and closing divert handle immediately");
            CloseHandleInternal();
            FlushQueuesImmediately();

            _currentState = EnforcementState.Inactive;
            _lastStatusMessage = "Emergency stop executed. All packets released.";
            _flowMonitor?.ResumeSniffing();
        }

        RaiseStatusChanged();
    }

    private unsafe void CaptureLoopAsync(CancellationToken ct)
    {
        const int BufferSize = 65535;
        byte[] packetBuffer = new byte[BufferSize];

        fixed (byte* pBuffer = packetBuffer)
        {
            while (!ct.IsCancellationRequested && _isShaping && _divertHandle != InvalidHandle)
            {
                var addr = new WinDivertNative.WINDIVERT_ADDRESS();
                if (!WinDivertNative.WinDivertRecv(_divertHandle, pBuffer, (uint)BufferSize, out uint recvLen, ref addr))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 995 /* ERROR_OPERATION_ABORTED */ || err == 6 /* ERROR_INVALID_HANDLE */)
                    {
                        break;
                    }
                    continue;
                }

                Interlocked.Increment(ref _totalPacketsCaptured);

                if (_isEmergencyStopped || _isPaused)
                {
                    // Pass immediately without shaping
                    SendPacketDirect(pBuffer, recvLen, in addr);
                    continue;
                }

                // Parse packet header
                var packetSpan = new ReadOnlySpan<byte>(pBuffer, (int)recvLen);
                if (!PacketParser.TryParse(packetSpan, out var header))
                {
                    // Unparseable packet, pass through unaltered
                    SendPacketDirect(pBuffer, recvLen, in addr);
                    continue;
                }

                bool isOutbound = addr.IsOutbound;
                var flowOwner = _flowTable.Lookup(
                    header.Protocol,
                    header.SourceAddress,
                    header.SourcePort,
                    header.DestinationAddress,
                    header.DestinationPort,
                    isOutbound);

                if (flowOwner == null || string.IsNullOrEmpty(flowOwner.CanonicalPath))
                {
                    // Unattributed or system traffic: pass immediately
                    SendPacketDirect(pBuffer, recvLen, in addr);
                    continue;
                }

                // Record transfer bytes in flow monitor for live rate reporting
                string localAddr = isOutbound ? header.SourceAddress : header.DestinationAddress;
                int localPort = isOutbound ? header.SourcePort : header.DestinationPort;
                string remoteAddr = isOutbound ? header.DestinationAddress : header.SourceAddress;
                int remotePort = isOutbound ? header.DestinationPort : header.SourcePort;
                string flowId = NetworkFlow.BuildFlowId(header.Protocol, localAddr, localPort, remoteAddr, remotePort);

                _flowMonitor?.RecordPacketTransfer(flowId, (long)recvLen, isOutbound);
                _flowMonitor?.RecordAppTransfer(flowOwner.CanonicalPath, (long)recvLen, isOutbound);

                // Check rate limits for this application
                var (downLimit, upLimit) = ResolveLimitsForApp(flowOwner.CanonicalPath);
                long? applicableLimit = isOutbound ? upLimit : downLimit;

                if (!applicableLimit.HasValue || applicableLimit.Value <= 0)
                {
                    // Unlimited: pass immediately
                    SendPacketDirect(pBuffer, recvLen, in addr);
                    continue;
                }

                // Enforce Token Bucket
                var pacer = GetOrCreatePacer(flowOwner.CanonicalPath, downLimit, upLimit);
                var limiter = isOutbound ? pacer.OutboundLimiter : pacer.InboundLimiter;

                if (limiter.TryConsume(recvLen, out _))
                {
                    // Tokens available: send immediately
                    SendPacketDirect(pBuffer, recvLen, in addr);
                }
                else
                {
                    // Queue packet for delayed pacing
                    byte[] packetCopy = new byte[recvLen];
                    Marshal.Copy((IntPtr)pBuffer, packetCopy, 0, (int)recvLen);

                    var queued = new QueuedPacket
                    {
                        Data = packetCopy,
                        Address = addr,
                        EnqueuedAtUtc = DateTime.UtcNow,
                        CanonicalAppPath = flowOwner.CanonicalPath,
                        IsOutbound = isOutbound
                    };

                    var targetQueue = isOutbound ? _outboundQueue : _inboundQueue;
                    if (!targetQueue.TryEnqueue(queued, out bool isOverflow))
                    {
                        if (isOverflow)
                        {
                            // Safe Overflow: Pass immediately rather than losing user connectivity
                            Interlocked.Increment(ref _totalPacketsBypassed);
                            SendPacketDirect(pBuffer, recvLen, in addr);
                        }
                        else
                        {
                            Interlocked.Increment(ref _totalPacketsDropped);
                        }
                    }
                }
            }
        }
    }

    private async Task PacingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isShaping)
        {
            bool drainedAny = DrainPacedPackets(_outboundQueue, isOutbound: true);
            drainedAny |= DrainPacedPackets(_inboundQueue, isOutbound: false);

            if (!drainedAny)
            {
                try
                {
                    await Task.Delay(2, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private unsafe bool DrainPacedPackets(BoundedPacketQueue queue, bool isOutbound)
    {
        if (queue.Count == 0) return false;

        bool processedAny = false;
        int maxBatch = Math.Min(32, queue.Count);

        for (int i = 0; i < maxBatch; i++)
        {
            if (!queue.TryDequeue(out var packet) || packet == null) break;

            if (_isEmergencyStopped || _isPaused)
            {
                fixed (byte* p = packet.Data)
                {
                    var addr = packet.Address;
                    SendPacketDirect(p, (uint)packet.Length, in addr);
                }
                processedAny = true;
                continue;
            }

            var pacer = _applicationPacers.GetValueOrDefault(packet.CanonicalAppPath);
            var limiter = isOutbound ? pacer?.OutboundLimiter : pacer?.InboundLimiter;

            if (limiter == null || limiter.TryConsume(packet.Length, out _))
            {
                fixed (byte* p = packet.Data)
                {
                    var addr = packet.Address;
                    SendPacketDirect(p, (uint)packet.Length, in addr);
                }
                processedAny = true;
            }
            else
            {
                // Put back if still constrained and not expired (> 500ms old packets are passed to prevent timeouts)
                if ((DateTime.UtcNow - packet.EnqueuedAtUtc).TotalMilliseconds > 500)
                {
                    fixed (byte* p = packet.Data)
                    {
                        var addr = packet.Address;
                        SendPacketDirect(p, (uint)packet.Length, in addr);
                    }
                    Interlocked.Increment(ref _totalPacketsBypassed);
                }
                else
                {
                    queue.TryEnqueue(packet, out _);
                    break;
                }
            }
        }

        return processedAny;
    }

    private unsafe void SendPacketDirect(byte* pPacket, uint packetLen, in WinDivertNative.WINDIVERT_ADDRESS addr)
    {
        if (_divertHandle == InvalidHandle || _divertHandle == IntPtr.Zero) return;

        WinDivertNative.WinDivertSend(_divertHandle, pPacket, packetLen, out _, in addr);
        Interlocked.Increment(ref _totalPacketsReinjected);
    }

    private unsafe void FlushQueuesImmediately()
    {
        var allPackets = _outboundQueue.FlushAll();
        allPackets.AddRange(_inboundQueue.FlushAll());

        if (_divertHandle != InvalidHandle && _divertHandle != IntPtr.Zero)
        {
            foreach (var packet in allPackets)
            {
                fixed (byte* p = packet.Data)
                {
                    var addr = packet.Address;
                    WinDivertNative.WinDivertSend(_divertHandle, p, (uint)packet.Length, out _, in addr);
                    Interlocked.Increment(ref _totalPacketsReinjected);
                }
            }
        }
    }

    private (long? DownLimit, long? UpLimit) ResolveLimitsForApp(string canonicalPath)
    {
        if (_rulesByPath.TryGetValue(canonicalPath, out var rule))
        {
            if (!rule.IsEnabled) return (null, null);

            Lane? lane = null;
            if (rule.LaneId.HasValue)
            {
                _lanesById.TryGetValue(rule.LaneId.Value, out lane);
            }

            long? down = rule.GetEffectiveDownloadLimitBps(lane);
            long? up = rule.GetEffectiveUploadLimitBps(lane);
            return (down, up);
        }

        return (null, null);
    }

    private ApplicationPacer GetOrCreatePacer(string canonicalPath, long? downLimit, long? upLimit)
    {
        var pacer = _applicationPacers.GetOrAdd(canonicalPath, path => new ApplicationPacer
        {
            InboundLimiter = downLimit.HasValue && downLimit.Value > 0
                ? new TokenBucketRateLimiter(downLimit.Value)
                : TokenBucketRateLimiter.CreateUnlimited(),
            OutboundLimiter = upLimit.HasValue && upLimit.Value > 0
                ? new TokenBucketRateLimiter(upLimit.Value)
                : TokenBucketRateLimiter.CreateUnlimited()
        });

        pacer.InboundLimiter.UpdateRate(downLimit ?? 0);
        pacer.OutboundLimiter.UpdateRate(upLimit ?? 0);
        return pacer;
    }

    private void CloseHandleInternal()
    {
        if (_divertHandle != InvalidHandle && _divertHandle != IntPtr.Zero)
        {
            WinDivertNative.WinDivertClose(_divertHandle);
            _divertHandle = InvalidHandle;
        }
    }

    private EnforcementStatus GetCurrentStatus() => new()
    {
        State = _currentState,
        ActiveFlowCount = _flowTable.Count,
        ControlledFlowCount = _applicationPacers.Count,
        QueuedPackets = _inboundQueue.Count + _outboundQueue.Count,
        QueuedBytes = _inboundQueue.CurrentBytes + _outboundQueue.CurrentBytes,
        DroppedPackets = Interlocked.Read(ref _totalPacketsDropped),
        BypassedPackets = Interlocked.Read(ref _totalPacketsBypassed),
        StatusMessage = _lastStatusMessage,
        TimestampUtc = DateTime.UtcNow
    };

    private void RaiseStatusChanged()
    {
        try
        {
            StatusChanged?.Invoke(GetCurrentStatus());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error raising StatusChanged event");
        }
    }

    private static string GetWin32ErrorMessage(int errorCode) => errorCode switch
    {
        5 => "Access Denied. Process must be elevated as Administrator.",
        2 => "Driver file not found. Ensure WinDivert64.sys exists.",
        577 => "Windows driver signature verification failed.",
        _ => $"Windows System Error {errorCode}"
    };

    public async ValueTask DisposeAsync()
    {
        await StopShapingAsync();
        GC.SuppressFinalize(this);
    }
}
