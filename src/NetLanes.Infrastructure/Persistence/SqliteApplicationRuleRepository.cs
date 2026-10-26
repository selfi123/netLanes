using System.Data.Common;
using Microsoft.Data.Sqlite;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.Infrastructure.Persistence;

public class SqliteApplicationRuleRepository : IApplicationRuleRepository
{
    private readonly SqliteDatabaseInitializer _initializer;

    public SqliteApplicationRuleRepository(SqliteDatabaseInitializer initializer)
    {
        _initializer = initializer;
    }

    private SqliteConnection CreateConnection() => new(_initializer.ConnectionString);

    public async Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, CanonicalExecutablePath, DisplayName, LaneId, IsEnabled, CustomDownloadLimitBps, CustomUploadLimitBps, IsCustomDownloadUnlimited, IsCustomUploadUnlimited, Notes, CreatedAtUtc, UpdatedAtUtc FROM ApplicationRules ORDER BY DisplayName ASC;";

        var list = new List<ApplicationRule>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadRule(reader));
        }

        return list;
    }

    public async Task<ApplicationRule?> GetByPathAsync(string canonicalPath, CancellationToken ct = default)
    {
        string normalized = PathMatcher.Normalize(canonicalPath);
        if (string.IsNullOrEmpty(normalized)) return null;

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, CanonicalExecutablePath, DisplayName, LaneId, IsEnabled, CustomDownloadLimitBps, CustomUploadLimitBps, IsCustomDownloadUnlimited, IsCustomUploadUnlimited, Notes, CreatedAtUtc, UpdatedAtUtc FROM ApplicationRules WHERE CanonicalExecutablePath = @Path;";
        cmd.Parameters.AddWithValue("@Path", normalized);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadRule(reader);
        }

        return null;
    }

    public async Task<ApplicationRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, CanonicalExecutablePath, DisplayName, LaneId, IsEnabled, CustomDownloadLimitBps, CustomUploadLimitBps, IsCustomDownloadUnlimited, IsCustomUploadUnlimited, Notes, CreatedAtUtc, UpdatedAtUtc FROM ApplicationRules WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadRule(reader);
        }

        return null;
    }

    public async Task SaveAsync(ApplicationRule rule, CancellationToken ct = default)
    {
        rule.CanonicalExecutablePath = PathMatcher.Normalize(rule.CanonicalExecutablePath);
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ApplicationRules (Id, CanonicalExecutablePath, DisplayName, LaneId, IsEnabled, CustomDownloadLimitBps, CustomUploadLimitBps, IsCustomDownloadUnlimited, IsCustomUploadUnlimited, Notes, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@Id, @Path, @DisplayName, @LaneId, @IsEnabled, @CustomDown, @CustomUp, @IsDownUnlimited, @IsUpUnlimited, @Notes, @CreatedAtUtc, @UpdatedAtUtc)
            ON CONFLICT(Id) DO UPDATE SET
                CanonicalExecutablePath = excluded.CanonicalExecutablePath,
                DisplayName = excluded.DisplayName,
                LaneId = excluded.LaneId,
                IsEnabled = excluded.IsEnabled,
                CustomDownloadLimitBps = excluded.CustomDownloadLimitBps,
                CustomUploadLimitBps = excluded.CustomUploadLimitBps,
                IsCustomDownloadUnlimited = excluded.IsCustomDownloadUnlimited,
                IsCustomUploadUnlimited = excluded.IsCustomUploadUnlimited,
                Notes = excluded.Notes,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        cmd.Parameters.AddWithValue("@Id", rule.Id.ToString());
        cmd.Parameters.AddWithValue("@Path", rule.CanonicalExecutablePath);
        cmd.Parameters.AddWithValue("@DisplayName", rule.DisplayName);
        cmd.Parameters.AddWithValue("@LaneId", rule.LaneId.HasValue ? rule.LaneId.Value.ToString() : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsEnabled", rule.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@CustomDown", rule.CustomDownloadLimitBps.HasValue ? rule.CustomDownloadLimitBps.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomUp", rule.CustomUploadLimitBps.HasValue ? rule.CustomUploadLimitBps.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDownUnlimited", rule.IsCustomDownloadUnlimited ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsUpUnlimited", rule.IsCustomUploadUnlimited ? 1 : 0);
        cmd.Parameters.AddWithValue("@Notes", rule.Notes ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", rule.CreatedAtUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@UpdatedAtUtc", rule.UpdatedAtUtc.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ApplicationRules WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ApplicationRule ReadRule(DbDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        CanonicalExecutablePath = reader.GetString(1),
        DisplayName = reader.GetString(2),
        LaneId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
        IsEnabled = reader.GetInt32(4) == 1,
        CustomDownloadLimitBps = reader.IsDBNull(5) ? null : reader.GetInt64(5),
        CustomUploadLimitBps = reader.IsDBNull(6) ? null : reader.GetInt64(6),
        IsCustomDownloadUnlimited = reader.GetInt32(7) == 1,
        IsCustomUploadUnlimited = reader.GetInt32(8) == 1,
        Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
        CreatedAtUtc = DateTime.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedAtUtc = DateTime.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind)
    };
}
