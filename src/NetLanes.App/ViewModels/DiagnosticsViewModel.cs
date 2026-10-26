using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetLanes.Core.Interfaces;
using NetLanes.Core.Models;
using NetLanes.Core.Utilities;
using NetLanes.Infrastructure.Logging;

namespace NetLanes.App.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly ITrafficShaper _trafficShaper;
    private readonly IDriverManager _driverManager;

    [ObservableProperty]
    private EnforcementState _state;

    [ObservableProperty]
    private int _activeFlows;

    [ObservableProperty]
    private int _controlledFlows;

    [ObservableProperty]
    private long _queuedPackets;

    [ObservableProperty]
    private string _queuedBytesDisplay = "0 B";

    [ObservableProperty]
    private long _droppedPackets;

    [ObservableProperty]
    private long _bypassedPackets;

    [ObservableProperty]
    private string _driverStatus = string.Empty;

    [ObservableProperty]
    private string _memoryUsage = "0 MB";

    public ObservableCollection<string> RecentLogLines { get; } = new();

    public DiagnosticsViewModel(ITrafficShaper trafficShaper, IDriverManager driverManager)
    {
        _trafficShaper = trafficShaper;
        _driverManager = driverManager;
        _trafficShaper.StatusChanged += OnStatusChanged;
        RefreshMetrics();
        RefreshLogs();
    }

    private void OnStatusChanged(EnforcementStatus status)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            State = status.State;
            ActiveFlows = status.ActiveFlowCount;
            ControlledFlows = status.ControlledFlowCount;
            QueuedPackets = status.QueuedPackets;
            QueuedBytesDisplay = UnitConverter.FormatBytes(status.QueuedBytes);
            DroppedPackets = status.DroppedPackets;
            BypassedPackets = status.BypassedPackets;
            DriverStatus = _driverManager.DriverStatusDescription;
            MemoryUsage = $"{Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB";
        });
    }

    [RelayCommand]
    public void RefreshMetrics()
    {
        var status = _trafficShaper.Status;
        State = status.State;
        ActiveFlows = status.ActiveFlowCount;
        ControlledFlows = status.ControlledFlowCount;
        QueuedPackets = status.QueuedPackets;
        QueuedBytesDisplay = UnitConverter.FormatBytes(status.QueuedBytes);
        DroppedPackets = status.DroppedPackets;
        BypassedPackets = status.BypassedPackets;
        DriverStatus = _driverManager.DriverStatusDescription;
        MemoryUsage = $"{Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)} MB";
    }

    [RelayCommand]
    public void RefreshLogs()
    {
        RecentLogLines.Clear();
        string logDir = LoggingConfig.LogDirectoryPath;
        if (!Directory.Exists(logDir)) return;

        var latestLog = Directory.GetFiles(logDir, "netlanes-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (latestLog != null && File.Exists(latestLog))
        {
            try
            {
                using var fs = new FileStream(latestLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                var lines = new List<string>();
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }

                // Show most recent 100 lines
                foreach (var l in lines.TakeLast(100))
                {
                    RecentLogLines.Add(l);
                }
            }
            catch
            {
                RecentLogLines.Add("Could not read current log file (file in use)");
            }
        }
        else
        {
            RecentLogLines.Add("No log entries recorded yet.");
        }
    }

    [RelayCommand]
    public void OpenLogFolder()
    {
        string logDir = LoggingConfig.LogDirectoryPath;
        if (Directory.Exists(logDir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = logDir,
                UseShellExecute = true
            });
        }
    }
}
