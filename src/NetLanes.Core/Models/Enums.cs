namespace NetLanes.Core.Models;

public enum NetProtocol : byte
{
    Unknown = 0,
    Tcp = 6,
    Udp = 17,
    Icmp = 1,
    IcmpV6 = 58
}

public enum FlowDirection : byte
{
    Unknown = 0,
    Inbound = 1,
    Outbound = 2
}

public enum EnforcementState : byte
{
    Inactive = 0,
    MonitorOnly = 1,
    ActiveShaping = 2,
    Paused = 3,
    SafeOverflowBypass = 4,
    ElevationRequired = 5,
    DriverUnavailable = 6
}

public enum RuleEnforcementStatus : byte
{
    None = 0,
    Configured = 1,
    Active = 2,
    Unsupported = 3
}
