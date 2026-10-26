namespace NetLanes.Core.Models;

public class Lane
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long DownloadLimitBps { get; set; }
    public long UploadLimitBps { get; set; }
    public bool IsUnlimitedDownload { get; set; } = true;
    public bool IsUnlimitedUpload { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public static Lane CreateDefaultStreaming() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Streaming",
        Description = "High-priority media streaming and video conferencing (Unlimited)",
        IsUnlimitedDownload = true,
        IsUnlimitedUpload = true,
        DownloadLimitBps = 0,
        UploadLimitBps = 0
    };

    public static Lane CreateDefaultGameDownloads() => new()
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Name = "Game Downloads",
        Description = "Bulk game and update downloads (Capped at 35 MiB/s down, 2 MiB/s up)",
        IsUnlimitedDownload = false,
        IsUnlimitedUpload = false,
        DownloadLimitBps = 35L * 1024 * 1024, // 35 MiB/s
        UploadLimitBps = 2L * 1024 * 1024     // 2 MiB/s
    };

    public static Lane CreateDefaultBackground() => new()
    {
        Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Name = "Background",
        Description = "Low-priority cloud backups, sync, and system updates (Capped at 5 MiB/s down, 1 MiB/s up)",
        IsUnlimitedDownload = false,
        IsUnlimitedUpload = false,
        DownloadLimitBps = 5L * 1024 * 1024,  // 5 MiB/s
        UploadLimitBps = 1L * 1024 * 1024     // 1 MiB/s
    };
}
