using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using NetLanes.Core.Interfaces;

namespace NetLanes.NetworkEngine.DriverManagement;

public class DriverManager : IDriverManager
{
    private readonly ILogger<DriverManager>? _logger;

    public DriverManager(ILogger<DriverManager>? logger = null)
    {
        _logger = logger;
    }

    public bool IsAdministrator
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to verify administrative role");
                return false;
            }
        }
    }

    public bool IsDriverInstalled
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;
            string dll = Path.Combine(baseDir, "WinDivert.dll");
            string sys = Path.Combine(baseDir, "WinDivert64.sys");

            bool exists = File.Exists(dll) || File.Exists(Path.Combine(baseDir, "runtimes", "win-x64", "native", "WinDivert.dll"));
            return exists;
        }
    }

    public bool CanIntercept => IsAdministrator && IsDriverInstalled;

    public string DriverStatusDescription
    {
        get
        {
            if (!IsAdministrator)
            {
                return "Elevation Required: Running in standard user mode. Traffic control disabled.";
            }
            if (!IsDriverInstalled)
            {
                return "WinDivert driver binaries (WinDivert.dll / WinDivert64.sys) not found.";
            }
            return "Active Traffic Control Ready (Administrator privileges confirmed).";
        }
    }

    public bool RestartAsAdministrator()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to elevate process via UAC");
            return false;
        }
    }
}
