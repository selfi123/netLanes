using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;

namespace NetLanes.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDriverManager _driverManager;

    [ObservableProperty]
    private ObservableObject _currentView;

    [ObservableProperty]
    private string _activeAdapterName = "Detecting...";

    [ObservableProperty]
    private string _networkStatus = "Connected";

    public DashboardViewModel DashboardVM { get; }
    public LanesViewModel LanesVM { get; }
    public RulesViewModel RulesVM { get; }
    public DiagnosticsViewModel DiagnosticsVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public AboutViewModel AboutVM { get; }

    public MainViewModel(
        DashboardViewModel dashboardVM,
        LanesViewModel lanesVM,
        RulesViewModel rulesVM,
        DiagnosticsViewModel diagnosticsVM,
        SettingsViewModel settingsVM,
        AboutViewModel aboutVM,
        IDriverManager driverManager)
    {
        DashboardVM = dashboardVM;
        LanesVM = lanesVM;
        RulesVM = rulesVM;
        DiagnosticsVM = diagnosticsVM;
        SettingsVM = settingsVM;
        AboutVM = aboutVM;
        _driverManager = driverManager;

        _currentView = dashboardVM;
        DetectNetworkAdapter();
    }

    [RelayCommand]
    public void NavigateDashboard() => CurrentView = DashboardVM;

    [RelayCommand]
    public async Task NavigateLanesAsync()
    {
        await LanesVM.LoadLanesAsync();
        CurrentView = LanesVM;
    }

    [RelayCommand]
    public async Task NavigateRulesAsync()
    {
        await RulesVM.LoadRulesAsync();
        CurrentView = RulesVM;
    }

    [RelayCommand]
    public void NavigateDiagnostics()
    {
        DiagnosticsVM.RefreshMetrics();
        DiagnosticsVM.RefreshLogs();
        CurrentView = DiagnosticsVM;
    }

    [RelayCommand]
    public async Task NavigateSettingsAsync()
    {
        await SettingsVM.LoadSettingsAsync();
        CurrentView = SettingsVM;
    }

    [RelayCommand]
    public void NavigateAbout() => CurrentView = AboutVM;

    private void DetectNetworkAdapter()
    {
        try
        {
            var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .OrderByDescending(nic => nic.Speed)
                .FirstOrDefault();

            if (activeInterface != null)
            {
                ActiveAdapterName = activeInterface.Name;
                NetworkStatus = $"Online ({activeInterface.NetworkInterfaceType})";
            }
            else
            {
                ActiveAdapterName = "No active connection";
                NetworkStatus = "Disconnected";
            }
        }
        catch
        {
            ActiveAdapterName = "Ethernet / Wi-Fi";
            NetworkStatus = "Online";
        }
    }
}
