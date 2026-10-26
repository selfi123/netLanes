using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.NetworkEngine.DriverManagement;
using NetLanes.NetworkEngine.FlowTracking;
using NetLanes.NetworkEngine.TrafficShaping;
using Xunit;

namespace NetLanes.Networking.Tests;

public class DriverLifecycleTests
{
    private class MockDriverManager : IDriverManager
    {
        public bool IsAdministrator { get; set; }
        public bool IsDriverInstalled { get; set; }
        public bool CanIntercept => IsAdministrator && IsDriverInstalled;
        public string DriverStatusDescription => "Mock driver manager";
        public bool RestartAsAdministrator() => true;
    }

    [Fact]
    public void RealDriverManager_CanBeQueriedWithoutCrashing()
    {
        var manager = new DriverManager();
        bool isAdmin = manager.IsAdministrator;
        bool isInstalled = manager.IsDriverInstalled;
        string desc = manager.DriverStatusDescription;

        Assert.NotNull(desc);
        Assert.NotEmpty(desc);
    }

    [Fact]
    public async Task StartShaping_WhenUnprivileged_SetsElevationRequiredStatusSafely()
    {
        var mockDriver = new MockDriverManager
        {
            IsAdministrator = false,
            IsDriverInstalled = true
        };

        var flowTable = new FlowTable();
        var engine = new TrafficShapingEngine(flowTable, mockDriver);

        await engine.StartShapingAsync();

        var status = engine.Status;
        Assert.Equal(EnforcementState.ElevationRequired, status.State);
        Assert.Contains("Administrator", status.StatusMessage, StringComparison.OrdinalIgnoreCase);

        await engine.DisposeAsync();
    }

    [Fact]
    public async Task EmergencyStop_TransitionsToInactiveAndCleansUp()
    {
        var mockDriver = new MockDriverManager
        {
            IsAdministrator = false,
            IsDriverInstalled = true
        };

        var flowTable = new FlowTable();
        var engine = new TrafficShapingEngine(flowTable, mockDriver);

        await engine.StartShapingAsync();
        engine.TriggerEmergencyStop();

        var status = engine.Status;
        Assert.Equal(EnforcementState.Inactive, status.State);
        Assert.Contains("Emergency", status.StatusMessage, StringComparison.OrdinalIgnoreCase);

        await engine.DisposeAsync();
    }
}
