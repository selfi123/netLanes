# NetLanes

**Windows Per-Application Network Traffic Management MVP**

NetLanes allows Windows users to group running applications into logical network lanes (e.g., *Streaming*, *Game Downloads*, *Background*) and control bandwidth consumption on a per-application basis using token-bucket traffic shaping and the authentic WinDivert packet interception driver.

---

## 🌟 Key Features

- **Live Application Monitoring**: Discovers running processes and active TCP/UDP sockets using Windows IP Helper APIs (`iphlpapi.dll`) and maps them to canonical executable paths. Real-time rates are smoothed using an Exponential Moving Average (~1s interval).
- **Logical Network Lanes**: Pre-configured with *Streaming*, *Game Downloads*, and *Background* profiles. Full CRUD support for user-defined custom lanes with distinct download and upload caps.
- **Granular Speed Rules**: Per-executable speed limits with explicit "Unlimited" and custom overrides. Rule state tracking distinguishes `Configured`, `Active` (enforced), and `Unsupported` states.
- **Real Traffic Shaping**: Microsecond-resolution token-bucket rate limiter pacing packets via WinDivert 2.2. Includes anti-loopback safeguards, bounded queues, and safe overflow pass-through to ensure network reliability.
- **Windows Driver & Privilege Safety**: Clear UAC elevation flow. Falls back gracefully to Passive Monitor Mode when launched by standard unprivileged users without crashing.
- **Modern WPF Desktop UI**: Polished dark theme interface built with MVVM (`CommunityToolkit.Mvvm`), reactive data binding, context menus, and diagnostic observability.
- **Zero-Telemetry Local Storage**: SQLite configuration persistence with versioned migrations (`PRAGMA user_version`) and rolling file logging via Serilog with sensitive payload exclusion.

---

## 📂 Repository Layout

```text
NetLanes.slnx
├── src/
│   ├── NetLanes.Core/                  # Models, token-bucket algorithms, unit converters, interfaces
│   ├── NetLanes.Infrastructure/        # SQLite persistence, IP Helper interop, process resolution, Serilog
│   ├── NetLanes.NetworkEngine/         # WinDivert native P/Invoke, 5-tuple flow correlation, traffic shaper
│   └── NetLanes.App/                   # WPF MVVM desktop application
│
├── tests/
│   ├── NetLanes.Core.Tests/            # Unit tests (Token bucket, unit conversions, path matching, SQLite)
│   └── NetLanes.Networking.Tests/      # Integration tests (Local socket pacing, bounded queues, driver lifecycle)
│
└── docs/
    ├── ARCHITECTURE.md                 # Detailed architectural blueprint & diagrams
    ├── MVP_SCOPE.md                    # MVP feature scope and boundaries
    ├── BUILD_AND_RUN.md                # Build, run, and manual validation instructions
    ├── TEST_PLAN.md                    # Verification matrix & automated test scenarios
    ├── SECURITY.md                     # Elevation model, driver safety & privacy policy
    └── KNOWN_LIMITATIONS.md            # Protocol constraints & edge case documentation
```

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build Solution
```powershell
dotnet build NetLanes.slnx -c Release
```

### Run Automated Tests
```powershell
dotnet test NetLanes.slnx -c Release --logger "console;verbosity=normal"
```

### Publish & Run Application
```powershell
# Publish win-x64 binary
dotnet publish src/NetLanes.App/NetLanes.App.csproj -c Release -r win-x64 --self-contained false -o ./publish

# Run as Administrator for active packet shaping:
Start-Process ./publish/NetLanes.App.exe -Verb RunAs

# Or run as Standard User for passive monitoring:
./publish/NetLanes.App.exe
```

---

## 📜 Documentation Links

- [System Architecture](file:///d:/docs and projects/projects/netLanes/docs/ARCHITECTURE.md)
- [MVP Scope & Boundaries](file:///d:/docs and projects/projects/netLanes/docs/MVP_SCOPE.md)
- [Build and Run Instructions](file:///d:/docs and projects/projects/netLanes/docs/BUILD_AND_RUN.md)
- [Test Plan](file:///d:/docs and projects/projects/netLanes/docs/TEST_PLAN.md)
- [Security & Driver Safety](file:///d:/docs and projects/projects/netLanes/docs/SECURITY.md)
- [Known Limitations](file:///d:/docs and projects/projects/netLanes/docs/KNOWN_LIMITATIONS.md)
