# NetLanes Build and Run Guide

## 1. System Requirements & Prerequisites
- **Operating System**: Windows 10 (Build 19041+) or Windows 11 x64 (recommended: Windows 11).
- **.NET SDK**: .NET 10.0 SDK (version `10.0.400` or higher) with `Microsoft.WindowsDesktop.App` runtime.
- **Administrative Privileges**:
  - Required for **Active Traffic Control Mode** (loading and opening the signed `WinDivert64.sys` driver).
  - Not required for **Monitor Mode** or UI configuration viewing/editing.
- **Visual C++ Redistributable**: MSVC 2015-2022 x64 Runtime (standard on modern Windows 10/11 installations).

---

## 2. Solution Structure
```text
NetLanes.sln
├── src/
│   ├── NetLanes.Core/              (Target: net10.0)
│   ├── NetLanes.Infrastructure/    (Target: net10.0)
│   ├── NetLanes.NetworkEngine/     (Target: net10.0-windows)
│   └── NetLanes.App/               (Target: net10.0-windows, OutputType: WinExe)
└── tests/
    ├── NetLanes.Core.Tests/        (Target: net10.0)
    └── NetLanes.Networking.Tests/  (Target: net10.0)
```

---

## 3. Building the Solution

### Building in Debug Mode
Open PowerShell in the repository root:
```powershell
dotnet build -c Debug
```

### Building in Release Mode
```powershell
dotnet build -c Release
```

### Publishing a Self-Contained / Framework-Dependent Application
```powershell
dotnet publish src/NetLanes.App/NetLanes.App.csproj -c Release -r win-x64 --self-contained false -o ./publish
```

---

## 4. Running the Automated Tests
Run all unit and integration test suites:
```powershell
dotnet test -c Release --logger "console;verbosity=normal"
```

To run only the core unit tests:
```powershell
dotnet test tests/NetLanes.Core.Tests/NetLanes.Core.Tests.csproj
```

To run the networking integration tests:
```powershell
dotnet test tests/NetLanes.Networking.Tests/NetLanes.Networking.Tests.csproj
```

---

## 5. Running the Application

### Option A: Standard (Unprivileged) Mode - Monitor Only
```powershell
dotnet run --project src/NetLanes.App/NetLanes.App.csproj
```
In this mode:
- Live applications and active TCP/UDP flows are discovered via Windows IP Helper APIs.
- Real-time download/upload rates are calculated and displayed.
- Lanes and application rules can be created, edited, and saved to SQLite.
- Traffic shaping is reported as unavailable until elevated.

### Option B: Elevated (Administrator) Mode - Full Traffic Shaping
Open PowerShell as Administrator:
```powershell
Start-Process dotnet -ArgumentList "run --project src/NetLanes.App/NetLanes.App.csproj" -Verb RunAs
```
Or directly run the published binary as Administrator:
```powershell
Start-Process ./publish/NetLanes.App.exe -Verb RunAs
```
In this mode:
- WinDivert kernel driver (`WinDivert64.sys`) is loaded safely.
- Active per-application token-bucket rate limiting can be toggled via "Enable Traffic Control".
- Emergency Stop instantly releases queued packets and flushes network buffers.

---

## 6. Manual Verification: Steam & Browser Scenario
1. Launch NetLanes as Administrator.
2. In the dashboard, click **Start Monitoring**.
3. Launch a web browser (e.g., Microsoft Edge or Google Chrome) and open a high-bitrate video stream (e.g., YouTube 4K).
4. Launch Steam and start or resume a game download.
5. In NetLanes:
   - Identify `steam.exe` in the application list.
   - Right-click `steam.exe` -> **Assign to Lane** -> select **Game Downloads** (configured with a 10 MiB/s limit).
   - Alternatively, open **Application Rules**, select `steam.exe`, and set a specific cap (e.g., 5 MiB/s).
6. Click **Enable Traffic Control**.
7. Observe Steam's download graph throttle down to the specified cap within 2-3 seconds.
8. Simultaneously observe the web browser video playback continuing smoothly without buffer starvation.
9. Click **Disable Traffic Control** or **Emergency Stop** and verify Steam immediately returns to line speed.
