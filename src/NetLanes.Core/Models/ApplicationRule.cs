namespace NetLanes.Core.Models;

public class ApplicationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CanonicalExecutablePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Guid? LaneId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long? CustomDownloadLimitBps { get; set; }
    public long? CustomUploadLimitBps { get; set; }
    public bool IsCustomDownloadUnlimited { get; set; } = true;
    public bool IsCustomUploadUnlimited { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public long? GetEffectiveDownloadLimitBps(Lane? assignedLane)
    {
        if (!IsEnabled) return null; // Unlimited when disabled
        if (CustomDownloadLimitBps.HasValue)
        {
            return IsCustomDownloadUnlimited ? null : CustomDownloadLimitBps.Value;
        }
        if (assignedLane != null)
        {
            return assignedLane.IsUnlimitedDownload ? null : assignedLane.DownloadLimitBps;
        }
        return null;
    }

    public long? GetEffectiveUploadLimitBps(Lane? assignedLane)
    {
        if (!IsEnabled) return null; // Unlimited when disabled
        if (CustomUploadLimitBps.HasValue)
        {
            return IsCustomUploadUnlimited ? null : CustomUploadLimitBps.Value;
        }
        if (assignedLane != null)
        {
            return assignedLane.IsUnlimitedUpload ? null : assignedLane.UploadLimitBps;
        }
        return null;
    }
}
