using NetLanes.Core.Models;

namespace NetLanes.Core.Interfaces;

public interface ILaneRepository
{
    Task<IReadOnlyList<Lane>> GetAllAsync(CancellationToken ct = default);
    Task<Lane?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Lane lane, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IApplicationRuleRepository
{
    Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken ct = default);
    Task<ApplicationRule?> GetByPathAsync(string canonicalPath, CancellationToken ct = default);
    Task<ApplicationRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(ApplicationRule rule, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<ApplicationSettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(ApplicationSettings settings, CancellationToken ct = default);
}
