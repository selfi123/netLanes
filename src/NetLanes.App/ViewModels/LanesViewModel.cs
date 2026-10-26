using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.ViewModels;

public partial class LanesViewModel : ObservableObject
{
    private readonly ILaneRepository _laneRepo;
    private readonly IApplicationRuleRepository _ruleRepo;
    private readonly ITrafficShaper _trafficShaper;

    public ObservableCollection<LaneItemViewModel> Lanes { get; } = new();

    [ObservableProperty]
    private LaneItemViewModel? _selectedLane;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private double _editDownloadValue = 10;

    [ObservableProperty]
    private string _editDownloadUnit = "MiB/s";

    [ObservableProperty]
    private bool _editIsUnlimitedDownload;

    [ObservableProperty]
    private double _editUploadValue = 2;

    [ObservableProperty]
    private string _editUploadUnit = "MiB/s";

    [ObservableProperty]
    private bool _editIsUnlimitedUpload;

    public List<string> SupportedUnits => UnitConverter.SupportedUnits.ToList();

    public LanesViewModel(
        ILaneRepository laneRepo,
        IApplicationRuleRepository ruleRepo,
        ITrafficShaper trafficShaper)
    {
        _laneRepo = laneRepo;
        _ruleRepo = ruleRepo;
        _trafficShaper = trafficShaper;
    }

    public async Task LoadLanesAsync()
    {
        var lanes = await _laneRepo.GetAllAsync();
        var rules = await _ruleRepo.GetAllAsync();

        Lanes.Clear();
        foreach (var l in lanes)
        {
            int assignedCount = rules.Count(r => r.LaneId == l.Id);
            Lanes.Add(new LaneItemViewModel(l, assignedCount));
        }
    }

    [RelayCommand]
    public void StartAddLane()
    {
        SelectedLane = null;
        EditName = "New Lane";
        EditDescription = "Custom traffic-shaping lane";
        EditDownloadValue = 20;
        EditDownloadUnit = "MiB/s";
        EditIsUnlimitedDownload = false;
        EditUploadValue = 5;
        EditUploadUnit = "MiB/s";
        EditIsUnlimitedUpload = false;
        IsEditing = true;
    }

    [RelayCommand]
    public void StartEditLane(LaneItemViewModel? item)
    {
        if (item == null) return;

        SelectedLane = item;
        EditName = item.Lane.Name;
        EditDescription = item.Lane.Description ?? string.Empty;
        EditIsUnlimitedDownload = item.Lane.IsUnlimitedDownload;
        EditIsUnlimitedUpload = item.Lane.IsUnlimitedUpload;

        EditDownloadValue = item.Lane.DownloadLimitBps > 0 ? (double)item.Lane.DownloadLimitBps / UnitConverter.MiB : 10;
        EditDownloadUnit = "MiB/s";

        EditUploadValue = item.Lane.UploadLimitBps > 0 ? (double)item.Lane.UploadLimitBps / UnitConverter.MiB : 2;
        EditUploadUnit = "MiB/s";

        IsEditing = true;
    }

    [RelayCommand]
    public async Task SaveEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;

        long downBps = EditIsUnlimitedDownload ? 0 : UnitConverter.ParseRateToBps(EditDownloadValue, EditDownloadUnit);
        long upBps = EditIsUnlimitedUpload ? 0 : UnitConverter.ParseRateToBps(EditUploadValue, EditUploadUnit);

        Lane laneToSave;
        if (SelectedLane != null)
        {
            laneToSave = SelectedLane.Lane;
            laneToSave.Name = EditName.Trim();
            laneToSave.Description = EditDescription.Trim();
            laneToSave.DownloadLimitBps = downBps;
            laneToSave.UploadLimitBps = upBps;
            laneToSave.IsUnlimitedDownload = EditIsUnlimitedDownload;
            laneToSave.IsUnlimitedUpload = EditIsUnlimitedUpload;
        }
        else
        {
            laneToSave = new Lane
            {
                Name = EditName.Trim(),
                Description = EditDescription.Trim(),
                DownloadLimitBps = downBps,
                UploadLimitBps = upBps,
                IsUnlimitedDownload = EditIsUnlimitedDownload,
                IsUnlimitedUpload = EditIsUnlimitedUpload
            };
        }

        await _laneRepo.SaveAsync(laneToSave);
        var allLanes = await _laneRepo.GetAllAsync();
        var allRules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(allRules, allLanes);

        IsEditing = false;
        await LoadLanesAsync();
    }

    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    public async Task DeleteLaneAsync(LaneItemViewModel? item)
    {
        if (item == null) return;

        await _laneRepo.DeleteAsync(item.Lane.Id);
        var allLanes = await _laneRepo.GetAllAsync();
        var allRules = await _ruleRepo.GetAllAsync();
        _trafficShaper.UpdateRules(allRules, allLanes);

        await LoadLanesAsync();
    }
}

public class LaneItemViewModel
{
    public Lane Lane { get; }
    public int AssignedApplicationCount { get; }

    public string Name => Lane.Name;
    public string? Description => Lane.Description;
    public string DownloadLimitDisplay => Lane.IsUnlimitedDownload ? "Unlimited" : UnitConverter.FormatRate(Lane.DownloadLimitBps);
    public string UploadLimitDisplay => Lane.IsUnlimitedUpload ? "Unlimited" : UnitConverter.FormatRate(Lane.UploadLimitBps);

    public LaneItemViewModel(Lane lane, int assignedCount)
    {
        Lane = lane;
        AssignedApplicationCount = assignedCount;
    }
}
