using NetLanes.Core.Models;
using Xunit;

namespace NetLanes.Core.Tests;

public class RulePrecedenceTests
{
    [Fact]
    public void CustomAppLimit_OverridesLaneDefault()
    {
        var lane = new Lane
        {
            Name = "Limited Lane",
            IsUnlimitedDownload = false,
            DownloadLimitBps = 10 * 1024 * 1024 // 10 MiB/s
        };

        var rule = new ApplicationRule
        {
            DisplayName = "Test App",
            IsEnabled = true,
            LaneId = lane.Id,
            IsCustomDownloadUnlimited = false,
            CustomDownloadLimitBps = 2 * 1024 * 1024 // 2 MiB/s custom override
        };

        long? effective = rule.GetEffectiveDownloadLimitBps(lane);
        Assert.Equal(2 * 1024 * 1024, effective);
    }

    [Fact]
    public void LaneDefault_InheritedWhenCustomLimitIsNull()
    {
        var lane = new Lane
        {
            Name = "Game Downloads",
            IsUnlimitedDownload = false,
            DownloadLimitBps = 35 * 1024 * 1024 // 35 MiB/s
        };

        var rule = new ApplicationRule
        {
            DisplayName = "Steam",
            IsEnabled = true,
            LaneId = lane.Id,
            CustomDownloadLimitBps = null // Inherit from lane
        };

        long? effective = rule.GetEffectiveDownloadLimitBps(lane);
        Assert.Equal(35 * 1024 * 1024, effective);
    }

    [Fact]
    public void DisabledRule_ResultsInUnlimitedTraffic()
    {
        var lane = new Lane
        {
            IsUnlimitedDownload = false,
            DownloadLimitBps = 1 * 1024 * 1024
        };

        var rule = new ApplicationRule
        {
            IsEnabled = false, // Disabled rule
            LaneId = lane.Id,
            CustomDownloadLimitBps = 500 * 1024
        };

        long? effective = rule.GetEffectiveDownloadLimitBps(lane);
        Assert.Null(effective); // null represents unlimited
    }

    [Fact]
    public void CustomUnlimited_OverridesLimitedLane()
    {
        var lane = new Lane
        {
            IsUnlimitedDownload = false,
            DownloadLimitBps = 10 * 1024 * 1024
        };

        var rule = new ApplicationRule
        {
            IsEnabled = true,
            LaneId = lane.Id,
            IsCustomDownloadUnlimited = true,
            CustomDownloadLimitBps = 0
        };

        long? effective = rule.GetEffectiveDownloadLimitBps(lane);
        Assert.Null(effective);
    }
}
