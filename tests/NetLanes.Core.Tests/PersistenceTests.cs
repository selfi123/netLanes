using NetLanes.Core.Models;
using NetLanes.Infrastructure.Persistence;
using Xunit;

namespace NetLanes.Core.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly SqliteLaneRepository _laneRepo;
    private readonly SqliteApplicationRuleRepository _ruleRepo;
    private readonly SqliteSettingsRepository _settingsRepo;

    public PersistenceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"netlanes_test_{Guid.NewGuid():N}.db");
        _initializer = new SqliteDatabaseInitializer(_tempDbPath);
        _laneRepo = new SqliteLaneRepository(_initializer);
        _ruleRepo = new SqliteApplicationRuleRepository(_initializer);
        _settingsRepo = new SqliteSettingsRepository(_initializer);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch
        {
            // Ignore temp file deletion errors
        }
    }

    [Fact]
    public async Task Initializer_SeedsDefaultLanesOnFirstRun()
    {
        await _initializer.InitializeAsync();

        var lanes = await _laneRepo.GetAllAsync();
        Assert.NotEmpty(lanes);
        Assert.Contains(lanes, l => l.Name == "Streaming");
        Assert.Contains(lanes, l => l.Name == "Game Downloads");
        Assert.Contains(lanes, l => l.Name == "Background");
    }

    [Fact]
    public async Task LaneRepository_SavesAndRetrievesCustomLane()
    {
        await _initializer.InitializeAsync();

        var lane = new Lane
        {
            Name = "Work Meetings",
            Description = "Video conferencing bandwidth",
            IsUnlimitedDownload = false,
            DownloadLimitBps = 15 * 1024 * 1024,
            IsUnlimitedUpload = true
        };

        await _laneRepo.SaveAsync(lane);

        var retrieved = await _laneRepo.GetByIdAsync(lane.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Work Meetings", retrieved.Name);
        Assert.Equal(15 * 1024 * 1024, retrieved.DownloadLimitBps);
        Assert.True(retrieved.IsUnlimitedUpload);

        await _laneRepo.DeleteAsync(lane.Id);
        var deleted = await _laneRepo.GetByIdAsync(lane.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task ApplicationRuleRepository_PersistsCanonicalPathAndLimits()
    {
        await _initializer.InitializeAsync();

        var rule = new ApplicationRule
        {
            CanonicalExecutablePath = @"C:\Program Files\Game\Client.exe",
            DisplayName = "Game Client",
            IsEnabled = true,
            IsCustomDownloadUnlimited = false,
            CustomDownloadLimitBps = 20 * 1024 * 1024
        };

        await _ruleRepo.SaveAsync(rule);

        // Retrieve using different casing
        var retrieved = await _ruleRepo.GetByPathAsync(@"c:\program files\game\client.exe");
        Assert.NotNull(retrieved);
        Assert.Equal("Game Client", retrieved.DisplayName);
        Assert.Equal(20 * 1024 * 1024, retrieved.CustomDownloadLimitBps);
    }

    [Fact]
    public async Task SettingsRepository_PersistsAndRestoresPreferences()
    {
        await _initializer.InitializeAsync();

        var settings = new ApplicationSettings
        {
            RefreshIntervalMs = 500,
            SafeOverflowEnabled = true,
            RateUnit = "MB/s",
            BoundedQueueMaxPackets = 5000
        };

        await _settingsRepo.SaveSettingsAsync(settings);
        var loaded = await _settingsRepo.GetSettingsAsync();

        Assert.Equal(500, loaded.RefreshIntervalMs);
        Assert.True(loaded.SafeOverflowEnabled);
        Assert.Equal("MB/s", loaded.RateUnit);
        Assert.Equal(5000, loaded.BoundedQueueMaxPackets);
    }

    [Fact]
    public async Task CorruptDatabase_IsBackedUpAndCleanDatabaseInitialized()
    {
        // Write garbage bytes to database path
        await File.WriteAllTextAsync(_tempDbPath, "THIS IS CORRUPT NOT A VALID SQLITE DB HEADER");

        // Initialization should recover and succeed
        await _initializer.InitializeAsync();

        var lanes = await _laneRepo.GetAllAsync();
        Assert.NotEmpty(lanes);
    }
}
