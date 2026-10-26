namespace NetLanes.Core.Models;

public class ApplicationSettings
{
    public int RefreshIntervalMs { get; set; } = 1000;
    public bool SafeOverflowEnabled { get; set; } = true;
    public int BoundedQueueMaxPackets { get; set; } = 2000;
    public long BoundedQueueMaxBytes { get; set; } = 4 * 1024 * 1024; // 4 MiB
    public string RateUnit { get; set; } = "MiB/s";
    public double SmoothingFactor { get; set; } = 0.3; // EMA alpha
    public bool DarkTheme { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public Guid? DefaultLaneId { get; set; }
}
