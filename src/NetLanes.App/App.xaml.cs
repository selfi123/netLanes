using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetLanes.App.ViewModels;
using NetLanes.Core.Interfaces;
using NetLanes.Infrastructure.Logging;
using NetLanes.Infrastructure.Persistence;
using NetLanes.Infrastructure.Windows;
using NetLanes.NetworkEngine.DriverManagement;
using NetLanes.NetworkEngine.FlowTracking;
using NetLanes.NetworkEngine.TrafficShaping;
using Serilog;

namespace NetLanes.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LoggingConfig.ConfigureLogging();
        Log.Information("Starting NetLanes Application");

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Persistence
                services.AddSingleton<SqliteDatabaseInitializer>();
                services.AddSingleton<ILaneRepository, SqliteLaneRepository>();
                services.AddSingleton<IApplicationRuleRepository, SqliteApplicationRuleRepository>();
                services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();

                // Windows & Process discovery
                services.AddSingleton<ProcessImageResolver>();
                services.AddSingleton<IProcessDiscoveryService, ProcessDiscoveryService>();
                services.AddSingleton<IDriverManager, DriverManager>();

                // Network Engine
                services.AddSingleton<FlowTable>();
                services.AddSingleton<IFlowMonitor, NetworkFlowMonitor>();
                services.AddSingleton<ITrafficShaper, TrafficShapingEngine>();

                // ViewModels
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<LanesViewModel>();
                services.AddSingleton<RulesViewModel>();
                services.AddSingleton<DiagnosticsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<AboutViewModel>();
                services.AddSingleton<MainViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // Initialize SQLite schema & default lanes
        var dbInit = _host.Services.GetRequiredService<SqliteDatabaseInitializer>();
        await dbInit.InitializeAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Stopping NetLanes Application");
        if (_host != null)
        {
            var shaper = _host.Services.GetService<ITrafficShaper>();
            if (shaper != null)
            {
                await shaper.StopShapingAsync();
            }

            var monitor = _host.Services.GetService<IFlowMonitor>();
            if (monitor != null)
            {
                await monitor.StopAsync();
            }

            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
