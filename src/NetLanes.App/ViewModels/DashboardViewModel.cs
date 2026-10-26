using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IFlowMonitor _flowMonitor;
    private readonly ITrafficShaper _trafficShaper;
    private readonly ILaneRepository _laneRepo;
    private readonly IApplicationRuleRepository _ruleRepo;
    private readonly IDriverManager _driverManager;

    private readonly List<ApplicationRowViewModel> _allApplications = new();
    public ObservableCollection<ApplicationRowViewModel> FilteredApplications { get; } = new();
    public ObservableCollection<Lane> AvailableLanes { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private bool _isShaping;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private double _totalDownloadRateBps;

    [ObservableProperty]
    private double _totalUploadRateBps;

    [ObservableProperty]
    private string _statusSummary = "Ready";

    [ObservableProperty]
    private EnforcementState _enforcementState = EnforcementState.Inactive;

    [ObservableProperty]
    private ApplicationRowViewModel? _selectedApplication;

    public string FormattedTotalDownloadRate => UnitConverter.FormatAdaptiveRate(TotalDownloadRateBps);
    public string FormattedTotalUploadRate => UnitConverter.FormatAdaptiveRate(TotalUploadRateBps);

    [ObservableProperty]
    private bool _isElevationRequired;

    public DashboardViewModel(
        IFlowMonitor flowMonitor,
        ITrafficShaper trafficShaper,
        ILaneRepository laneRepo,
        IApplicationRuleRepository ruleRepo,
        IDriverManager driverManager)
    {
        _flowMonitor = flowMonitor;
        _trafficShaper = trafficShaper;
        _laneRepo = laneRepo;
        _ruleRepo = ruleRepo;
        _driverManager = driverManager;

        _isElevationRequired = !driverManager.IsAdministrator;
        _flowMonitor.FlowsUpdated += OnFlowsUpdated;
        _trafficShaper.StatusChanged += OnShaperStatusChanged;
    }

    public async Task InitializeAsync()
    {
        await ReloadLanesAndRulesAsync();
        await StartMonitoringAsync();
    }

    public async Task ReloadLanesAndRulesAsync()
    {
        var lanes = await _laneRepo.GetAllAsync();
        AvailableLanes.Clear();
        foreach (var l in lanes)
        {
            AvailableLanes.Add(l);
        }

        var rules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(rules, lanes);

        var ruleMap = rules.ToDictionary(r => r.CanonicalExecutablePath, StringComparer.OrdinalIgnoreCase);
        var laneMap = lanes.ToDictionary(l => l.Id);

        foreach (var app in _allApplications)
        {
            if (ruleMap.TryGetValue(app.CanonicalPath, out var r))
            {
                Lane? lane = r.LaneId.HasValue && laneMap.TryGetValue(r.LaneId.Value, out var l) ? l : null;
                app.AssignedLaneId = lane?.Id;
                app.AssignedLaneName = lane?.Name ?? "Unassigned";
                app.DownloadLimitBps = r.GetEffectiveDownloadLimitBps(lane);
                app.UploadLimitBps = r.GetEffectiveUploadLimitBps(lane);
                app.RuleStatus = IsShaping && lane != null ? RuleEnforcementStatus.Active : (lane != null ? RuleEnforcementStatus.Configured : RuleEnforcementStatus.None);
            }
        }
    }

    [RelayCommand]
    public void RestartAsAdministrator()
    {
        if (_driverManager.RestartAsAdministrator())
        {
            Application.Current?.Shutdown();
        }
    }

    [RelayCommand]
    public async Task StartMonitoringAsync()
    {
        await _flowMonitor.StartAsync();
        IsMonitoring = true;
        IsElevationRequired = !_driverManager.IsAdministrator;
        StatusSummary = IsElevationRequired
            ? "Unprivileged Mode — Administrator rights required for WinDivert packet capture & throttling"
            : "Monitoring Active — WinDivert packet capture running";
        EnforcementState = EnforcementState.MonitorOnly;
    }

    [RelayCommand]
    public async Task StopMonitoringAsync()
    {
        await _flowMonitor.StopAsync();
        IsMonitoring = false;
        TotalDownloadRateBps = 0;
        TotalUploadRateBps = 0;
        StatusSummary = "Monitoring stopped";
        EnforcementState = EnforcementState.Inactive;
    }

    [RelayCommand]
    public async Task EnableControlAsync()
    {
        if (!_driverManager.IsAdministrator)
        {
            var res = MessageBox.Show(
                "Active Traffic Control requires Administrator privileges to open the packet divert driver.\n\nWould you like to restart NetLanes as Administrator?",
                "Administrator Elevation Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                _driverManager.RestartAsAdministrator();
                Application.Current.Shutdown();
            }
            return;
        }

        await ReloadLanesAndRulesAsync();
        await _trafficShaper.StartShapingAsync();
        IsShaping = true;
        IsPaused = false;
        EnforcementState = _trafficShaper.Status.State;
        StatusSummary = _trafficShaper.Status.StatusMessage ?? "Active Traffic Control running";

        foreach (var app in _allApplications)
        {
            if (app.AssignedLaneId.HasValue)
            {
                app.RuleStatus = RuleEnforcementStatus.Active;
            }
        }
    }

    [RelayCommand]
    public async Task DisableControlAsync()
    {
        await _trafficShaper.StopShapingAsync();
        IsShaping = false;
        IsPaused = false;
        EnforcementState = IsMonitoring ? EnforcementState.MonitorOnly : EnforcementState.Inactive;
        StatusSummary = "Traffic control disabled. Normal traffic handling restored.";

        foreach (var app in _allApplications)
        {
            if (app.AssignedLaneId.HasValue)
            {
                app.RuleStatus = RuleEnforcementStatus.Configured;
            }
        }
    }

    [RelayCommand]
    public async Task PauseControlAsync()
    {
        if (IsPaused)
        {
            await _trafficShaper.ResumeShapingAsync();
            IsPaused = false;
            StatusSummary = "Traffic control resumed";
        }
        else
        {
            await _trafficShaper.PauseShapingAsync();
            IsPaused = true;
            StatusSummary = "Traffic control paused (Bypass mode)";
        }
    }

    [RelayCommand]
    public void EmergencyStop()
    {
        _trafficShaper.TriggerEmergencyStop();
        IsShaping = false;
        IsPaused = false;
        EnforcementState = EnforcementState.Inactive;
        StatusSummary = "Emergency Stop triggered: All queues flushed & driver released.";
    }

    [RelayCommand]
    public async Task AssignToLaneAsync(object? parameter)
    {
        ApplicationRowViewModel? app = SelectedApplication;
        Lane? lane = null;

        if (parameter is ValueTuple<ApplicationRowViewModel, Lane> tuple)
        {
            app = tuple.Item1;
            lane = tuple.Item2;
        }
        else if (parameter is Lane l)
        {
            lane = l;
        }
        else if (parameter is string laneName)
        {
            lane = AvailableLanes.FirstOrDefault(x => string.Equals(x.Name, laneName, StringComparison.OrdinalIgnoreCase));
        }

        if (app == null && _allApplications.Count > 0)
        {
            app = _allApplications.FirstOrDefault(a => a.DisplayName.Contains("steam", StringComparison.OrdinalIgnoreCase)) ?? _allApplications.First();
        }

        if (app == null || lane == null)
        {
            Serilog.Log.Warning("AssignToLaneAsync called with null app ({App}) or lane ({Lane})", app?.DisplayName, lane?.Name);
            return;
        }

        Serilog.Log.Information("Assigning application {AppName} ({Path}) to lane {LaneName}", app.DisplayName, app.CanonicalPath, lane.Name);

        var rule = await _ruleRepo.GetByPathAsync(app.CanonicalPath);
        if (rule == null)
        {
            rule = new ApplicationRule
            {
                CanonicalExecutablePath = app.CanonicalPath,
                DisplayName = app.DisplayName,
                LaneId = lane.Id,
                IsEnabled = true
            };
        }
        else
        {
            rule.LaneId = lane.Id;
            rule.IsEnabled = true;
        }

        await _ruleRepo.SaveAsync(rule);

        // Update target app row immediately
        app.AssignedLaneId = lane.Id;
        app.AssignedLaneName = lane.Name;
        app.DownloadLimitBps = rule.GetEffectiveDownloadLimitBps(lane);
        app.UploadLimitBps = rule.GetEffectiveUploadLimitBps(lane);
        app.RuleStatus = IsShaping ? RuleEnforcementStatus.Active : RuleEnforcementStatus.Configured;

        await ReloadLanesAndRulesAsync();
    }

    [RelayCommand]
    public async Task ResetToUnlimitedAsync(object? parameter)
    {
        var app = parameter as ApplicationRowViewModel ?? SelectedApplication;
        if (app == null) return;

        var rule = await _ruleRepo.GetByPathAsync(app.CanonicalPath);
        if (rule != null)
        {
            rule.LaneId = null;
            rule.CustomDownloadLimitBps = null;
            rule.CustomUploadLimitBps = null;
            rule.IsCustomDownloadUnlimited = true;
            rule.IsCustomUploadUnlimited = true;
            await _ruleRepo.SaveAsync(rule);
            await ReloadLanesAndRulesAsync();
        }

        app.AssignedLaneId = null;
        app.AssignedLaneName = "Unassigned";
        app.DownloadLimitBps = null;
        app.UploadLimitBps = null;
        app.RuleStatus = RuleEnforcementStatus.None;
    }

    private void OnFlowsUpdated(IReadOnlyList<NetworkFlow> flows)
    {
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            double totalDown = 0;
            double totalUp = 0;

            // Group flows by canonical executable path or process name
            var groups = flows
                .GroupBy(f => !string.IsNullOrEmpty(f.CanonicalPath) ? f.CanonicalPath : (!string.IsNullOrEmpty(f.ProcessName) ? f.ProcessName : $"PID {f.ProcessId}"))
                .ToList();

            var currentCanonicalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                var first = g.First();
                string key = g.Key;
                currentCanonicalSet.Add(key);

                // Query app-level aggregated transfer stats from flow monitor
                var (appDownRate, appUpRate, appBytesDown, appBytesUp) = !string.IsNullOrEmpty(first.CanonicalPath)
                    ? _flowMonitor.GetAppTransferStats(first.CanonicalPath)
                    : (0, 0, 0, 0);

                double flowSumDown = g.Sum(f => f.DownloadRateBps);
                double flowSumUp = g.Sum(f => f.UploadRateBps);
                long flowSumBytesDown = g.Sum(f => f.TotalBytesDownloaded);
                long flowSumBytesUp = g.Sum(f => f.TotalBytesUploaded);

                double groupDownRate = Math.Max(flowSumDown, appDownRate);
                double groupUpRate = Math.Max(flowSumUp, appUpRate);
                long groupBytesDown = Math.Max(flowSumBytesDown, appBytesDown);
                long groupBytesUp = Math.Max(flowSumBytesUp, appBytesUp);
                var pids = g.Select(f => f.ProcessId).Where(p => p > 0).Distinct().ToList();

                totalDown += groupDownRate;
                totalUp += groupUpRate;

                var existing = _allApplications.FirstOrDefault(a => string.Equals(a.CanonicalPath, key, StringComparison.OrdinalIgnoreCase) || string.Equals(a.DisplayName, key, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    var rule = !string.IsNullOrEmpty(first.CanonicalPath)
                        ? await _ruleRepo.GetByPathAsync(first.CanonicalPath)
                        : null;

                    Lane? lane = rule?.LaneId.HasValue == true
                        ? AvailableLanes.FirstOrDefault(l => l.Id == rule.LaneId.Value)
                        : null;

                    var newRow = new ApplicationRowViewModel
                    {
                        DisplayName = first.ProcessName ?? key,
                        ExecutablePath = first.ExecutablePath ?? string.Empty,
                        CanonicalPath = first.CanonicalPath ?? key,
                        PidDisplay = pids.Count > 1 ? $"PIDs: {string.Join(", ", pids)}" : (pids.Count == 1 ? $"PID: {pids[0]}" : string.Empty),
                        DownloadRateBps = groupDownRate,
                        UploadRateBps = groupUpRate,
                        TotalDownloadedBytes = groupBytesDown,
                        TotalUploadedBytes = groupBytesUp,
                        IsAttributed = first.IsAttributed,
                        IsControllable = first.IsAttributed && first.ProcessId > 4,
                        AssignedLaneId = lane?.Id,
                        AssignedLaneName = lane?.Name ?? "Unassigned",
                        DownloadLimitBps = rule?.GetEffectiveDownloadLimitBps(lane),
                        UploadLimitBps = rule?.GetEffectiveUploadLimitBps(lane),
                        RuleStatus = rule != null ? (IsShaping ? RuleEnforcementStatus.Active : RuleEnforcementStatus.Configured) : RuleEnforcementStatus.None
                    };

                    newRow.ProcessIds.AddRange(pids);
                    _allApplications.Add(newRow);
                }
                else
                {
                    existing.DownloadRateBps = groupDownRate;
                    existing.UploadRateBps = groupUpRate;
                    existing.TotalDownloadedBytes = groupBytesDown;
                    existing.TotalUploadedBytes = groupBytesUp;
                    existing.PidDisplay = pids.Count > 1 ? $"PIDs: {string.Join(", ", pids)}" : (pids.Count == 1 ? $"PID: {pids[0]}" : string.Empty);
                    if (IsShaping && existing.DownloadLimitBps.HasValue)
                    {
                        existing.RuleStatus = RuleEnforcementStatus.Active;
                    }
                }
            }

            // Remove applications that have stopped transmitting and have zero rate
            _allApplications.RemoveAll(a => !currentCanonicalSet.Contains(a.CanonicalPath) && a.DownloadRateBps < 1 && a.UploadRateBps < 1);

            TotalDownloadRateBps = totalDown > 0 ? totalDown : _flowMonitor.GlobalDownloadRateBps;
            TotalUploadRateBps = totalUp > 0 ? totalUp : _flowMonitor.GlobalUploadRateBps;

            ApplyFilter();
        });
    }

    private void OnShaperStatusChanged(EnforcementStatus status)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            EnforcementState = status.State;
            StatusSummary = status.StatusMessage ?? "Ready";
            IsShaping = status.State == EnforcementState.ActiveShaping;
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredApplications.Clear();
        var query = SearchText.Trim();

        var matches = string.IsNullOrEmpty(query)
            ? _allApplications
            : _allApplications.Where(a =>
                a.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.AssignedLaneName.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var app in matches.OrderByDescending(a => a.DownloadRateBps + a.UploadRateBps))
        {
            FilteredApplications.Add(app);
        }
    }

    partial void OnTotalDownloadRateBpsChanged(double value) => OnPropertyChanged(nameof(FormattedTotalDownloadRate));
    partial void OnTotalUploadRateBpsChanged(double value) => OnPropertyChanged(nameof(FormattedTotalUploadRate));
}
