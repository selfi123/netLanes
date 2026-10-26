using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepo;

    [ObservableProperty]
    private int _refreshIntervalMs = 1000;

    [ObservableProperty]
    private bool _safeOverflowEnabled = true;

    [ObservableProperty]
    private int _boundedQueueMaxPackets = 2000;

    [ObservableProperty]
    private int _boundedQueueMaxMb = 4;

    [ObservableProperty]
    private string _rateUnit = "MiB/s";

    [ObservableProperty]
    private double _smoothingFactor = 0.35;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public List<string> SupportedUnits => UnitConverter.SupportedUnits.ToList();

    public SettingsViewModel(ISettingsRepository settingsRepo)
    {
        _settingsRepo = settingsRepo;
    }

    public async Task LoadSettingsAsync()
    {
        var settings = await _settingsRepo.GetSettingsAsync();
        RefreshIntervalMs = settings.RefreshIntervalMs;
        SafeOverflowEnabled = settings.SafeOverflowEnabled;
        BoundedQueueMaxPackets = settings.BoundedQueueMaxPackets;
        BoundedQueueMaxMb = (int)Math.Max(1, settings.BoundedQueueMaxBytes / (1024 * 1024));
        RateUnit = settings.RateUnit;
        SmoothingFactor = settings.SmoothingFactor;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var settings = new ApplicationSettings
        {
            RefreshIntervalMs = RefreshIntervalMs,
            SafeOverflowEnabled = SafeOverflowEnabled,
            BoundedQueueMaxPackets = BoundedQueueMaxPackets,
            BoundedQueueMaxBytes = (long)BoundedQueueMaxMb * 1024 * 1024,
            RateUnit = RateUnit,
            SmoothingFactor = SmoothingFactor
        };

        await _settingsRepo.SaveSettingsAsync(settings);
        StatusMessage = "Settings saved successfully.";
        await Task.Delay(2000);
        StatusMessage = string.Empty;
    }
}
