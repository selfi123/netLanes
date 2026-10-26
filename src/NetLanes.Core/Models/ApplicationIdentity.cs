namespace NetLanes.Core.Models;

public class ApplicationIdentity
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; }
    public string? CanonicalPath { get; set; }
    public DateTime? StartTimeUtc { get; set; }
    public bool IsSystemProcess { get; set; }
    public bool IsControllable { get; set; } = true;
    public string DisplayName => !string.IsNullOrWhiteSpace(ProcessName) ? ProcessName : (ExecutablePath != null ? System.IO.Path.GetFileNameWithoutExtension(ExecutablePath) : $"PID {ProcessId}");
}
