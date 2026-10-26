using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.Infrastructure.Windows;

public class ProcessImageResolver
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(
        IntPtr hProcess,
        uint dwFlags,
        StringBuilder lpExeName,
        ref uint lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(
        IntPtr hProcess,
        out long lpCreationTime,
        out long lpExitTime,
        out long lpKernelTime,
        out long lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "registry",
        "smss.exe",
        "csrss.exe",
        "wininit.exe",
        "services.exe",
        "lsass.exe",
        "svchost.exe",
        "dwm.exe",
        "fontdrvhost.exe",
        "winlogon.exe",
        "spoolsv.exe"
    };

    private record CachedProcessInfo(
        int ProcessId,
        string ProcessName,
        string? ExecutablePath,
        string? CanonicalPath,
        long CreationTime,
        bool IsSystemProcess,
        DateTime CachedAtUtc);

    private readonly ConcurrentDictionary<int, CachedProcessInfo> _cache = new();

    public ApplicationIdentity? Resolve(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        if (processId == 4)
        {
            return new ApplicationIdentity
            {
                ProcessId = 4,
                ProcessName = "System",
                ExecutablePath = "System",
                CanonicalPath = "system",
                IsSystemProcess = true,
                IsControllable = false
            };
        }

        IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            // If already cached, check if cached entry is still somewhat recent
            if (_cache.TryGetValue(processId, out var stale) && (DateTime.UtcNow - stale.CachedAtUtc).TotalSeconds < 10)
            {
                return ToIdentity(stale);
            }
            return null;
        }

        try
        {
            long creationTime = 0;
            if (GetProcessTimes(hProcess, out long ct, out _, out _, out _))
            {
                creationTime = ct;
            }

            if (_cache.TryGetValue(processId, out var cached) && cached.CreationTime == creationTime && creationTime > 0)
            {
                return ToIdentity(cached);
            }

            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            string? exePath = null;

            if (QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
            {
                exePath = sb.ToString();
            }

            string procName = string.Empty;
            if (!string.IsNullOrEmpty(exePath))
            {
                procName = PathMatcher.GetFileName(exePath);
            }
            else
            {
                try
                {
                    using var p = Process.GetProcessById(processId);
                    procName = p.ProcessName;
                }
                catch
                {
                    procName = $"PID {processId}";
                }
            }

            string? canonical = PathMatcher.Normalize(exePath);
            bool isSystem = SystemProcessNames.Contains(procName) || SystemProcessNames.Contains(Path.GetFileName(canonical ?? string.Empty));

            var info = new CachedProcessInfo(
                processId,
                procName,
                exePath,
                canonical,
                creationTime,
                isSystem,
                DateTime.UtcNow);

            _cache[processId] = info;
            return ToIdentity(info);
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    public void ClearCache() => _cache.Clear();

    private static ApplicationIdentity ToIdentity(CachedProcessInfo info) => new()
    {
        ProcessId = info.ProcessId,
        ProcessName = info.ProcessName,
        ExecutablePath = info.ExecutablePath,
        CanonicalPath = info.CanonicalPath,
        IsSystemProcess = info.IsSystemProcess,
        IsControllable = !info.IsSystemProcess && !string.IsNullOrEmpty(info.ExecutablePath)
    };
}
