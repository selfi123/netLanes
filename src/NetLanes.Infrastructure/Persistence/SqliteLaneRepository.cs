using System.Data.Common;
using Microsoft.Data.Sqlite;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;

namespace NetLanes.Infrastructure.Persistence;

public class SqliteLaneRepository : ILaneRepository
{
    private readonly SqliteDatabaseInitializer _initializer;

    public SqliteLaneRepository(SqliteDatabaseInitializer initializer)
    {
        _initializer = initializer;
    }

    private SqliteConnection CreateConnection() => new(_initializer.ConnectionString);

    public async Task<IReadOnlyList<Lane>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, DownloadLimitBps, UploadLimitBps, IsUnlimitedDownload, IsUnlimitedUpload, CreatedAtUtc, UpdatedAtUtc FROM Lanes ORDER BY Name ASC;";

        var list = new List<Lane>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadLane(reader));
        }

        return list;
    }

    public async Task<Lane?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Description, DownloadLimitBps, UploadLimitBps, IsUnlimitedDownload, IsUnlimitedUpload, CreatedAtUtc, UpdatedAtUtc FROM Lanes WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadLane(reader);
        }

        return null;
    }

    public async Task SaveAsync(Lane lane, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        lane.UpdatedAtUtc = DateTime.UtcNow;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Lanes (Id, Name, Description, DownloadLimitBps, UploadLimitBps, IsUnlimitedDownload, IsUnlimitedUpload, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@Id, @Name, @Description, @DownloadLimitBps, @UploadLimitBps, @IsUnlimitedDownload, @IsUnlimitedUpload, @CreatedAtUtc, @UpdatedAtUtc)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Description = excluded.Description,
                DownloadLimitBps = excluded.DownloadLimitBps,
                UploadLimitBps = excluded.UploadLimitBps,
                IsUnlimitedDownload = excluded.IsUnlimitedDownload,
                IsUnlimitedUpload = excluded.IsUnlimitedUpload,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        cmd.Parameters.AddWithValue("@Id", lane.Id.ToString());
        cmd.Parameters.AddWithValue("@Name", lane.Name);
        cmd.Parameters.AddWithValue("@Description", lane.Description ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@DownloadLimitBps", lane.DownloadLimitBps);
        cmd.Parameters.AddWithValue("@UploadLimitBps", lane.UploadLimitBps);
        cmd.Parameters.AddWithValue("@IsUnlimitedDownload", lane.IsUnlimitedDownload ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsUnlimitedUpload", lane.IsUnlimitedUpload ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAtUtc", lane.CreatedAtUtc.ToString("o"));
        cmd.Parameters.AddWithValue("@UpdatedAtUtc", lane.UpdatedAtUtc.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Lanes WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id.ToString());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Lane ReadLane(DbDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Name = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        DownloadLimitBps = reader.GetInt64(3),
        UploadLimitBps = reader.GetInt64(4),
        IsUnlimitedDownload = reader.GetInt32(5) == 1,
        IsUnlimitedUpload = reader.GetInt32(6) == 1,
        CreatedAtUtc = DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedAtUtc = DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind)
    };
}
