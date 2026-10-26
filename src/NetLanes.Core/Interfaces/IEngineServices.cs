using NetLanes.Core.Models;

namespace NetLanes.Core.Interfaces;

public interface IProcessDiscoveryService
{
    Task<IReadOnlyList<ApplicationIdentity>> GetRunningApplicationsAsync(CancellationToken ct = default);
    ApplicationIdentity? ResolveProcess(int processId);
    string? GetProcessImagePath(int processId);
    void InvalidateCache();
}

public interface IFlowMonitor
{
    event Action<IReadOnlyList<NetworkFlow>>? FlowsUpdated;
    bool IsMonitoring { get; }
    bool IsElevationRequired { get; }
    double GlobalDownloadRateBps { get; }
    double GlobalUploadRateBps { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    void PauseSniffing();
    void ResumeSniffing();
    IReadOnlyList<NetworkFlow> GetActiveFlows();
    void RecordAppTransfer(string canonicalPath, long byteCount, bool isOutbound);
    void RecordPacketTransfer(string flowId, long byteCount, bool isOutbound);
    (double downRate, double upRate, long totalDown, long totalUp) GetAppTransferStats(string canonicalPath);
}

public interface ITrafficShaper : IAsyncDisposable
{
    EnforcementStatus Status { get; }
    event Action<EnforcementStatus>? StatusChanged;
    Task StartShapingAsync(CancellationToken ct = default);
    Task StopShapingAsync(CancellationToken ct = default);
    Task PauseShapingAsync(CancellationToken ct = default);
    Task ResumeShapingAsync(CancellationToken ct = default);
    void TriggerEmergencyStop();
    void UpdateRules(IReadOnlyList<ApplicationRule> rules, IReadOnlyList<Lane> lanes);
}

public interface IDriverManager
{
    bool IsAdministrator { get; }
    bool IsDriverInstalled { get; }
    bool CanIntercept { get; }
    string DriverStatusDescription { get; }
    bool RestartAsAdministrator();
}
