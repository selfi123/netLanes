namespace NetLanes.Core.Models;

public class EnforcementStatus
{
    public EnforcementState State { get; set; } = EnforcementState.Inactive;
    public int ActiveFlowCount { get; set; }
    public int ControlledFlowCount { get; set; }
    public long QueuedPackets { get; set; }
    public long QueuedBytes { get; set; }
    public long DroppedPackets { get; set; }
    public long BypassedPackets { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static EnforcementStatus InactiveStatus => new()
    {
        State = EnforcementState.Inactive,
        StatusMessage = "Ready - Traffic monitoring stopped"
    };

    public static EnforcementStatus MonitorOnlyStatus(int activeFlows) => new()
    {
        State = EnforcementState.MonitorOnly,
        ActiveFlowCount = activeFlows,
        StatusMessage = "Monitor Mode - Passively observing flows"
    };
}
