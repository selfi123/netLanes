using Microsoft.Data.Sqlite;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;

namespace NetLanes.Infrastructure.Persistence;

public class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqliteDatabaseInitializer _initializer;

    public SqliteSettingsRepository(SqliteDatabaseInitializer initializer)
    {
        _initializer = initializer;
    }

    private SqliteConnection CreateConnection() => new(_initializer.ConnectionString);

    public async Task<ApplicationSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings;";

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        var settings = new ApplicationSettings();
        if (dict.TryGetValue("RefreshIntervalMs", out var refresh) && int.TryParse(refresh, out var r))
            settings.RefreshIntervalMs = r;
        if (dict.TryGetValue("SafeOverflowEnabled", out var safe) && bool.TryParse(safe, out var s))
            settings.SafeOverflowEnabled = s;
        if (dict.TryGetValue("BoundedQueueMaxPackets", out var maxPackets) && int.TryParse(maxPackets, out var mp))
            settings.BoundedQueueMaxPackets = mp;
        if (dict.TryGetValue("BoundedQueueMaxBytes", out var maxBytes) && long.TryParse(maxBytes, out var mb))
            settings.BoundedQueueMaxBytes = mb;
        if (dict.TryGetValue("RateUnit", out var unit))
            settings.RateUnit = unit;
        if (dict.TryGetValue("SmoothingFactor", out var smooth) && double.TryParse(smooth, out var sm))
            settings.SmoothingFactor = sm;
        if (dict.TryGetValue("DarkTheme", out var theme) && bool.TryParse(theme, out var dt))
            settings.DarkTheme = dt;
        if (dict.TryGetValue("StartMinimized", out var min) && bool.TryParse(min, out var smn))
            settings.StartMinimized = smn;
        if (dict.TryGetValue("DefaultLaneId", out var defLane) && Guid.TryParse(defLane, out var g))
            settings.DefaultLaneId = g;

        return settings;
    }

    public async Task SaveSettingsAsync(ApplicationSettings settings, CancellationToken ct = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await using var tx = await connection.BeginTransactionAsync(ct);

        var pairs = new (string Key, string Value)[]
        {
            ("RefreshIntervalMs", settings.RefreshIntervalMs.ToString()),
            ("SafeOverflowEnabled", settings.SafeOverflowEnabled.ToString()),
            ("BoundedQueueMaxPackets", settings.BoundedQueueMaxPackets.ToString()),
            ("BoundedQueueMaxBytes", settings.BoundedQueueMaxBytes.ToString()),
            ("RateUnit", settings.RateUnit),
            ("SmoothingFactor", settings.SmoothingFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("DarkTheme", settings.DarkTheme.ToString()),
            ("StartMinimized", settings.StartMinimized.ToString()),
            ("DefaultLaneId", settings.DefaultLaneId.HasValue ? settings.DefaultLaneId.Value.ToString() : string.Empty)
        };

        foreach (var (key, value) in pairs)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT INTO Settings (Key, Value) VALUES (@Key, @Value) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            cmd.Parameters.AddWithValue("@Key", key);
            cmd.Parameters.AddWithValue("@Value", value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}
