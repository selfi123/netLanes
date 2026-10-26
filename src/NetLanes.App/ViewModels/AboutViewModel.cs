using CommunityToolkit.Mvvm.ComponentModel;

namespace NetLanes.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string AppVersion => "NetLanes MVP 1.0.0 (.NET 10 LTS x64)";

    public string WinDivertNotice => """
        WinDivert (Windows Packet Divert) v2.2.2
        Copyright (c) 2018-2022 basil
        WinDivert is free software distributed under the GNU Lesser General Public License (LGPL) version 3, 
        or the GNU General Public License (GPL) version 2.
        Source code and driver binaries available at: https://github.com/basil00/WinDivert
        """;

    public string ThirdPartyNotices => """
        • CommunityToolkit.Mvvm (MIT License) - Copyright (c) .NET Foundation and Contributors
        • Microsoft.Data.Sqlite (MIT License) - Copyright (c) .NET Foundation and Contributors
        • Serilog (Apache License 2.0) - Copyright (c) Serilog Contributors
        • Microsoft.Extensions.Hosting (MIT License) - Copyright (c) .NET Foundation
        """;

    public string ArchitecturalNotice => """
        NetLanes is a Windows per-application traffic management utility designed to prevent 
        bandwidth-heavy processes from monopolizing active connections. 
        NetLanes operates through logical traffic-shaping profiles and packet pacing; 
        it does not create virtual network adapters, VPNs, or separate internet links.
        """;
}
