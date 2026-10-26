using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.ViewModels;

public partial class RulesViewModel : ObservableObject
{
    private readonly IApplicationRuleRepository _ruleRepo;
    private readonly ILaneRepository _laneRepo;
    private readonly ITrafficShaper _trafficShaper;

    public ObservableCollection<RuleItemViewModel> Rules { get; } = new();
    public ObservableCollection<Lane> AvailableLanes { get; } = new();

    [ObservableProperty]
    private RuleItemViewModel? _selectedRule;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editPath = string.Empty;

    [ObservableProperty]
    private string _editDisplayName = string.Empty;

    [ObservableProperty]
    private Lane? _editSelectedLane;

    [ObservableProperty]
    private bool _editIsEnabled = true;

    [ObservableProperty]
    private bool _editUseCustomLimits;

    [ObservableProperty]
    private double _editCustomDownValue = 5;

    [ObservableProperty]
    private string _editCustomDownUnit = "MiB/s";

    [ObservableProperty]
    private bool _editIsCustomDownUnlimited = false;

    [ObservableProperty]
    private double _editCustomUpValue = 1;

    [ObservableProperty]
    private string _editCustomUpUnit = "MiB/s";

    [ObservableProperty]
    private bool _editIsCustomUpUnlimited = false;

    public List<string> SupportedUnits => UnitConverter.SupportedUnits.ToList();

    public RulesViewModel(
        IApplicationRuleRepository ruleRepo,
        ILaneRepository laneRepo,
        ITrafficShaper trafficShaper)
    {
        _ruleRepo = ruleRepo;
        _laneRepo = laneRepo;
        _trafficShaper = trafficShaper;
    }

    public async Task LoadRulesAsync()
    {
        var lanes = await _laneRepo.GetAllAsync();
        var rules = await _ruleRepo.GetAllAsync();

        AvailableLanes.Clear();
        foreach (var l in lanes)
        {
            AvailableLanes.Add(l);
        }

        Rules.Clear();
        foreach (var r in rules)
        {
            var lane = lanes.FirstOrDefault(l => l.Id == r.LaneId);
            Rules.Add(new RuleItemViewModel(r, lane));
        }
    }

    [RelayCommand]
    public void BrowseExecutable()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Application Executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() == true)
        {
            EditPath = dlg.FileName;
            EditDisplayName = PathMatcher.GetFileName(dlg.FileName);
            EditSelectedLane = AvailableLanes.FirstOrDefault();
            EditIsEnabled = true;
            EditUseCustomLimits = false;
            SelectedRule = null;
            IsEditing = true;
        }
    }

    [RelayCommand]
    public void StartEditRule(RuleItemViewModel? item)
    {
        if (item == null) return;

        SelectedRule = item;
        EditPath = item.Rule.CanonicalExecutablePath;
        EditDisplayName = item.Rule.DisplayName;
        EditSelectedLane = AvailableLanes.FirstOrDefault(l => l.Id == item.Rule.LaneId);
        EditIsEnabled = item.Rule.IsEnabled;

        bool hasCustom = item.Rule.CustomDownloadLimitBps.HasValue || item.Rule.CustomUploadLimitBps.HasValue;
        EditUseCustomLimits = hasCustom;

        if (hasCustom)
        {
            EditCustomDownValue = item.Rule.CustomDownloadLimitBps.HasValue && item.Rule.CustomDownloadLimitBps.Value > 0
                ? (double)item.Rule.CustomDownloadLimitBps.Value / UnitConverter.MiB
                : 5;
            EditIsCustomDownUnlimited = item.Rule.IsCustomDownloadUnlimited;

            EditCustomUpValue = item.Rule.CustomUploadLimitBps.HasValue && item.Rule.CustomUploadLimitBps.Value > 0
                ? (double)item.Rule.CustomUploadLimitBps.Value / UnitConverter.MiB
                : 1;
            EditIsCustomUpUnlimited = item.Rule.IsCustomUploadUnlimited;
        }

        IsEditing = true;
    }

    [RelayCommand]
    public async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditPath)) return;

        long? downBps = EditUseCustomLimits
            ? (EditIsCustomDownUnlimited ? 0 : UnitConverter.ParseRateToBps(EditCustomDownValue, EditCustomDownUnit))
            : null;

        long? upBps = EditUseCustomLimits
            ? (EditIsCustomUpUnlimited ? 0 : UnitConverter.ParseRateToBps(EditCustomUpValue, EditCustomUpUnit))
            : null;

        ApplicationRule ruleToSave;
        if (SelectedRule != null)
        {
            ruleToSave = SelectedRule.Rule;
            ruleToSave.CanonicalExecutablePath = EditPath.Trim();
            ruleToSave.DisplayName = !string.IsNullOrWhiteSpace(EditDisplayName) ? EditDisplayName.Trim() : PathMatcher.GetFileName(EditPath);
            ruleToSave.LaneId = EditSelectedLane?.Id;
            ruleToSave.IsEnabled = EditIsEnabled;
            ruleToSave.CustomDownloadLimitBps = downBps;
            ruleToSave.CustomUploadLimitBps = upBps;
            ruleToSave.IsCustomDownloadUnlimited = EditIsCustomDownUnlimited;
            ruleToSave.IsCustomUploadUnlimited = EditIsCustomUpUnlimited;
        }
        else
        {
            ruleToSave = new ApplicationRule
            {
                CanonicalExecutablePath = EditPath.Trim(),
                DisplayName = !string.IsNullOrWhiteSpace(EditDisplayName) ? EditDisplayName.Trim() : PathMatcher.GetFileName(EditPath),
                LaneId = EditSelectedLane?.Id,
                IsEnabled = EditIsEnabled,
                CustomDownloadLimitBps = downBps,
                CustomUploadLimitBps = upBps,
                IsCustomDownloadUnlimited = EditIsCustomDownUnlimited,
                IsCustomUploadUnlimited = EditIsCustomUpUnlimited
            };
        }

        await _ruleRepo.SaveAsync(ruleToSave);
        var allLanes = await _laneRepo.GetAllAsync();
        var allRules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(allRules, allLanes);

        IsEditing = false;
        await LoadRulesAsync();
    }

    [RelayCommand]
    public void CancelEdit() => IsEditing = false;

    [RelayCommand]
    public async Task ToggleRuleAsync(RuleItemViewModel? item)
    {
        if (item == null) return;
        item.Rule.IsEnabled = !item.Rule.IsEnabled;
        await _ruleRepo.SaveAsync(item.Rule);

        var allLanes = await _laneRepo.GetAllAsync();
        var allRules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(allRules, allLanes);

        await LoadRulesAsync();
    }

    [RelayCommand]
    public async Task DeleteRuleAsync(RuleItemViewModel? item)
    {
        if (item == null) return;

        await _ruleRepo.DeleteAsync(item.Rule.Id);
        var allLanes = await _laneRepo.GetAllAsync();
        var allRules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(allRules, allLanes);

        await LoadRulesAsync();
    }
}

public class RuleItemViewModel
{
    public ApplicationRule Rule { get; }
    public Lane? Lane { get; }

    public string DisplayName => Rule.DisplayName;
    public string ExecutablePath => Rule.CanonicalExecutablePath;
    public string LaneName => Lane?.Name ?? "None";
    public bool IsEnabled => Rule.IsEnabled;

    public string EffectiveLimitsDisplay
    {
        get
        {
            if (!Rule.IsEnabled) return "Disabled";
            long? down = Rule.GetEffectiveDownloadLimitBps(Lane);
            long? up = Rule.GetEffectiveUploadLimitBps(Lane);
            string d = down.HasValue ? UnitConverter.FormatRate(down.Value) : "Unlimited";
            string u = up.HasValue ? UnitConverter.FormatRate(up.Value) : "Unlimited";
            return $"↓ {d} / ↑ {u}";
        }
    }

    public RuleItemViewModel(ApplicationRule rule, Lane? lane)
    {
        Rule = rule;
        Lane = lane;
    }
}
