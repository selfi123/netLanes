using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NetLanes.Core.Models;

namespace NetLanes.Infrastructure.Persistence;

public class SqliteDatabaseInitializer
{
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly ILogger<SqliteDatabaseInitializer>? _logger;

    public string ConnectionString => _connectionString;
    public string DatabasePath => _databasePath;

    public SqliteDatabaseInitializer(string? customDbPath = null, ILogger<SqliteDatabaseInitializer>? logger = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _databasePath = customDbPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "NetLanes");
            Directory.CreateDirectory(folder);
            _databasePath = Path.Combine(folder, "netlanes.db");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await RunMigrationsAsync(ct);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 11 or 26 ||
                                         ex.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("not a database", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogError(ex, "Corrupt or invalid SQLite database encountered at {Path}. Attempting backup and recovery.", _databasePath);
            RecoverCorruptDatabase();
            await RunMigrationsAsync(ct);
        }
    }

    private void RecoverCorruptDatabase()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath))
            {
                string backup = $"{_databasePath}.corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_databasePath, backup, overwrite: true);
                _logger?.LogWarning("Backed up corrupt database to {BackupPath}", backup);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to move corrupt database file");
        }
    }

    private async Task RunMigrationsAsync(CancellationToken ct)
    {
        string? dir = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
            await pragmaCmd.ExecuteNonQueryAsync(ct);
        }

        int currentVersion = 0;
        await using (var versionCmd = connection.CreateCommand())
        {
            versionCmd.CommandText = "PRAGMA user_version;";
            object? result = await versionCmd.ExecuteScalarAsync(ct);
            if (result != null && int.TryParse(result.ToString(), out int v))
            {
                currentVersion = v;
            }
        }

        if (currentVersion < 1)
        {
            await ApplyMigrationV1Async(connection, ct);
        }
    }

    private static async Task ApplyMigrationV1Async(SqliteConnection connection, CancellationToken ct)
    {
        await using var tx = await connection.BeginTransactionAsync(ct);

        string ddl = """
            CREATE TABLE IF NOT EXISTS Lanes (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                DownloadLimitBps INTEGER NOT NULL,
                UploadLimitBps INTEGER NOT NULL,
                IsUnlimitedDownload INTEGER NOT NULL,
                IsUnlimitedUpload INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ApplicationRules (
                Id TEXT PRIMARY KEY,
                CanonicalExecutablePath TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                LaneId TEXT,
                IsEnabled INTEGER NOT NULL,
                CustomDownloadLimitBps INTEGER,
                CustomUploadLimitBps INTEGER,
                IsCustomDownloadUnlimited INTEGER NOT NULL,
                IsCustomUploadUnlimited INTEGER NOT NULL,
                Notes TEXT,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                FOREIGN KEY (LaneId) REFERENCES Lanes(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ApplicationRules_CanonicalPath ON ApplicationRules (CanonicalExecutablePath);
            CREATE INDEX IF NOT EXISTS IX_ApplicationRules_LaneId ON ApplicationRules (LaneId);
            """;

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = ddl;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Seed default lanes if empty
        var defaultLanes = new[]
        {
            Lane.CreateDefaultStreaming(),
            Lane.CreateDefaultGameDownloads(),
            Lane.CreateDefaultBackground()
        };

        foreach (var lane in defaultLanes)
        {
            await using var seedCmd = connection.CreateCommand();
            seedCmd.Transaction = (SqliteTransaction)tx;
            seedCmd.CommandText = """
                INSERT OR IGNORE INTO Lanes 
                (Id, Name, Description, DownloadLimitBps, UploadLimitBps, IsUnlimitedDownload, IsUnlimitedUpload, CreatedAtUtc, UpdatedAtUtc)
                VALUES (@Id, @Name, @Description, @DownloadLimitBps, @UploadLimitBps, @IsUnlimitedDownload, @IsUnlimitedUpload, @CreatedAtUtc, @UpdatedAtUtc);
                """;
            seedCmd.Parameters.AddWithValue("@Id", lane.Id.ToString());
            seedCmd.Parameters.AddWithValue("@Name", lane.Name);
            seedCmd.Parameters.AddWithValue("@Description", lane.Description ?? (object)DBNull.Value);
            seedCmd.Parameters.AddWithValue("@DownloadLimitBps", lane.DownloadLimitBps);
            seedCmd.Parameters.AddWithValue("@UploadLimitBps", lane.UploadLimitBps);
            seedCmd.Parameters.AddWithValue("@IsUnlimitedDownload", lane.IsUnlimitedDownload ? 1 : 0);
            seedCmd.Parameters.AddWithValue("@IsUnlimitedUpload", lane.IsUnlimitedUpload ? 1 : 0);
            seedCmd.Parameters.AddWithValue("@CreatedAtUtc", lane.CreatedAtUtc.ToString("o"));
            seedCmd.Parameters.AddWithValue("@UpdatedAtUtc", lane.UpdatedAtUtc.ToString("o"));
            await seedCmd.ExecuteNonQueryAsync(ct);
        }

        await using (var updateVerCmd = connection.CreateCommand())
        {
            updateVerCmd.Transaction = (SqliteTransaction)tx;
            updateVerCmd.CommandText = "PRAGMA user_version = 1;";
            await updateVerCmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}
