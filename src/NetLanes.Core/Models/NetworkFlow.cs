namespace NetLanes.Core.Models;

public class NetworkFlow
{
    public string FlowId { get; set; } = string.Empty;
    public NetProtocol Protocol { get; set; }
    public string LocalAddress { get; set; } = string.Empty;
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = string.Empty;
    public int RemotePort { get; set; }
    public int ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public string? ExecutablePath { get; set; }
    public string? CanonicalPath { get; set; }
    public bool IsAttributed => ProcessId > 0 && !string.IsNullOrEmpty(ExecutablePath);
    public double DownloadRateBps { get; set; }
    public double UploadRateBps { get; set; }
    public long TotalBytesDownloaded { get; set; }
    public long TotalBytesUploaded { get; set; }
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;
    public string State { get; set; } = "Active";

    public static string BuildFlowId(NetProtocol protocol, string localAddress, int localPort, string remoteAddress, int remotePort)
    {
        return $"{protocol}_{localAddress}:{localPort}_{remoteAddress}:{remotePort}";
    }
}
