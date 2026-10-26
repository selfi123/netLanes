using System.Diagnostics;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.Infrastructure.Windows;

public class ProcessDiscoveryService : IProcessDiscoveryService
{
    private readonly ProcessImageResolver _imageResolver;

    public ProcessDiscoveryService(ProcessImageResolver imageResolver)
    {
        _imageResolver = imageResolver;
    }

    public Task<IReadOnlyList<ApplicationIdentity>> GetRunningApplicationsAsync(CancellationToken ct = default)
    {
        var list = new List<ApplicationIdentity>();
        var processes = Process.GetProcesses();

        foreach (var proc in processes)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var identity = _imageResolver.Resolve(proc.Id);
                if (identity != null && !string.IsNullOrWhiteSpace(identity.ExecutablePath))
                {
                    list.Add(identity);
                }
            }
            catch
            {
                // Process may have exited during enumeration
            }
            finally
            {
                proc.Dispose();
            }
        }

        return Task.FromResult<IReadOnlyList<ApplicationIdentity>>(list);
    }

    public ApplicationIdentity? ResolveProcess(int processId) => _imageResolver.Resolve(processId);

    public string? GetProcessImagePath(int processId) => _imageResolver.Resolve(processId)?.ExecutablePath;

    public void InvalidateCache() => _imageResolver.ClearCache();
}
