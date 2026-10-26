using CommunityToolkit.Mvvm.ComponentModel;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;

namespace NetLanes.App.ViewModels;

public partial class ApplicationRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _canonicalPath = string.Empty;

    [ObservableProperty]
    private string _pidDisplay = string.Empty;

    public List<int> ProcessIds { get; } = new();

    [ObservableProperty]
    private double _downloadRateBps;

    [ObservableProperty]
    private double _uploadRateBps;

    [ObservableProperty]
    private long _totalDownloadedBytes;

    [ObservableProperty]
    private long _totalUploadedBytes;

    [ObservableProperty]
    private string _assignedLaneName = "Unassigned";

    [ObservableProperty]
    private Guid? _assignedLaneId;

    [ObservableProperty]
    private RuleEnforcementStatus _ruleStatus = RuleEnforcementStatus.None;

    [ObservableProperty]
    private bool _isAttributed = true;

    [ObservableProperty]
    private bool _isControllable = true;

    [ObservableProperty]
    private long? _downloadLimitBps;

    [ObservableProperty]
    private long? _uploadLimitBps;

    public string FormattedDownloadRate => UnitConverter.FormatAdaptiveRate(DownloadRateBps);
    public string FormattedUploadRate => UnitConverter.FormatAdaptiveRate(UploadRateBps);
    public string FormattedDownloadedBytes => UnitConverter.FormatBytes(TotalDownloadedBytes);
    public string FormattedUploadedBytes => UnitConverter.FormatBytes(TotalUploadedBytes);

    public string LimitsSummary
    {
        get
        {
            if (!DownloadLimitBps.HasValue && !UploadLimitBps.HasValue)
            {
                return "Unlimited";
            }
            string down = DownloadLimitBps.HasValue ? $"↓ {UnitConverter.FormatRate(DownloadLimitBps.Value)}" : "↓ Unlimited";
            string up = UploadLimitBps.HasValue ? $"↑ {UnitConverter.FormatRate(UploadLimitBps.Value)}" : "↑ Unlimited";
            return $"{down} / {up}";
        }
    }

    public string StatusBadgeText => RuleStatus switch
    {
        RuleEnforcementStatus.Active => "ENFORCED",
        RuleEnforcementStatus.Configured => "CONFIGURED",
        RuleEnforcementStatus.Unsupported => "UNSUPPORTED",
        _ => "PASS-THROUGH"
    };

    partial void OnDownloadRateBpsChanged(double value) => OnPropertyChanged(nameof(FormattedDownloadRate));
    partial void OnUploadRateBpsChanged(double value) => OnPropertyChanged(nameof(FormattedUploadRate));
    partial void OnTotalDownloadedBytesChanged(long value) => OnPropertyChanged(nameof(FormattedDownloadedBytes));
    partial void OnTotalUploadedBytesChanged(long value) => OnPropertyChanged(nameof(FormattedUploadedBytes));
    partial void OnDownloadLimitBpsChanged(long? value) => OnPropertyChanged(nameof(LimitsSummary));
    partial void OnUploadLimitBpsChanged(long? value) => OnPropertyChanged(nameof(LimitsSummary));
    partial void OnRuleStatusChanged(RuleEnforcementStatus value) => OnPropertyChanged(nameof(StatusBadgeText));
}
