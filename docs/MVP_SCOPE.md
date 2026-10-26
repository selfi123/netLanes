# NetLanes MVP Scope and Specification

## 1. Executive Summary
**NetLanes** is a Windows desktop network traffic management application designed to group running applications into logical network lanes (e.g., Streaming, Game Downloads, Background) and throttle bandwidth consumption on a per-application basis. NetLanes ensures that bandwidth-heavy applications (such as game download clients or cloud synchronization agents) do not saturate the user's connection, preserving responsiveness for interactive workloads (such as video streaming, conferencing, or web browsing).

### Conceptual Boundaries
- **Logical Traffic Shaping vs. Physical Networks**: NetLanes does **not** create virtual network adapters, VPN tunnels, or separate physical network connections. It operates by monitoring socket connections and applying token-bucket pacing to intercepted packets belonging to specific application flows.
- **No Artificial Bandwidth Creation**: NetLanes cannot increase physical line speed or guarantee upstream QoS across the public internet. It prevents local link monopolization by pacing transmission and reception at the host level.

---

## 2. Target Platform & Technology Stack
- **Operating System**: Windows 10 (version 1903+) and Windows 11 x64 (primary target: Windows 11).
- **Runtime & Language**: C# 13 / .NET 10 LTS (`net10.0-windows` for WPF UI, `net10.0` for core libraries).
- **UI Framework**: Windows Presentation Foundation (WPF) with modern custom dark theme.
- **Architecture Pattern**: Model-View-ViewModel (MVVM) powered by `CommunityToolkit.Mvvm`.
- **Dependency Injection**: `Microsoft.Extensions.DependencyInjection` via .NET Generic Host (`Microsoft.Extensions.Hosting`).
- **Configuration & Persistence**: SQLite via `Microsoft.Data.Sqlite` with versioned schema migrations (`PRAGMA user_version`).
- **Logging**: Serilog with rolling file sink (`logs/netlanes-.log`) and strict PII/payload exclusion.
- **Process & Socket Discovery**: Windows IP Helper API (`GetExtendedTcpTable`, `GetExtendedUdpTable`) and Process APIs (`QueryFullProcessImageNameW`).
- **Packet Interception & Shaping**: WinDivert (Windows Packet Divert) v2.2.x user-mode driver interface with token-bucket rate limiting.
- **Automated Testing**: xUnit with fluent assertions, controllable test clocks (`System.TimeProvider`), and in-memory test sockets.

---

## 3. Included Features (In-Scope for MVP)

### Feature A: Live Application Monitoring
- Live process and socket enumeration via Windows IP Helper table polling and WinDivert flow events.
- Attribution of active TCP and UDP flows to executable paths, application names, and Process IDs.
- Real-time download and upload rate measurement smoothed via Exponential Moving Average (~1s interval).
- Current session byte counters (bytes received, bytes sent).
- Visual indication of assigned lane and enforcement state (Monitored, Active Capped, Unlimited, Unattributed).
- Grouping of multiple process instances running the same executable into a unified application view while preserving individual socket/PID tracking.
- Resilient parsing of executable paths containing spaces, UNC identifiers, and non-ASCII characters.
- Passive monitor mode that runs without installing or opening kernel interception handles if administrative rights are absent.

### Feature B: Logical Network Lanes
- CRUD management for user-defined logical network lanes.
- Default pre-configured lanes:
  1. **Streaming**: High priority, unconstrained or high burst allowance.
  2. **Game Downloads**: Default download cap (e.g., 35 MiB/s) to avoid choking the network.
  3. **Background**: Low bandwidth cap (e.g., 5 MiB/s down, 1 MiB/s up) for background sync tools.
- Attributes: Name, description, default download limit (bps), default upload limit (bps), unlimited toggles.
- Assignment of applications to lanes via dropdown, context menu, or manual executable path selection.
- Persistence of lane assignments using canonical executable paths across restarts.

### Feature C: Per-Application Speed Rules
- Granular speed limits (bytes/second) defined at the application level or inherited from the assigned lane.
- Explicit "Unlimited" option distinguished from a zero limit.
- Distinct enforcement states:
  - `Configured`: Rule exists in database.
  - `Active`: Interception and token-bucket shaping are actively running for this application's flows.
  - `Unsupported`: The application's traffic cannot be safely intercepted (e.g., system-critical loopback, protected process).
- Accurate, consistent rate units with explicit conversion (B/s, KB/s, MB/s, MiB/s, Mbps).

### Feature D: Real Traffic Shaping
- Kernel-level packet interception using WinDivert network layer filters.
- Real-time 5-tuple flow correlation (Protocol, LocalIP, LocalPort, RemoteIP, RemotePort) matched against the active IP Helper table.
- High-precision Token Bucket rate-limiting algorithm enforcing sustained download and upload byte rates.
- Asynchronous packet processing to prevent thread pool starvation and UI freezes.
- Anti-loopback / reinjection loop prevention using WinDivert handle priorities and injection tags.
- Bounded packet queues per application with safe overflow pass-through to prevent memory exhaustion and latency spikes.
- Separation of Monitor Mode vs. Active Control Mode.

### Feature E: Global Controls & Desktop UI
- Global controls: Start Monitoring, Enable Traffic Control, Disable Traffic Control, Pause Enforcement, Emergency Stop.
- Modern WPF interface with dark theme, responsive layout, clear status indicators, and keyboard navigation.
- Dedicated screens: Dashboard, Lane Management, Application Rules, Diagnostics & Logs, Settings, About & Licenses.
- Graceful privilege elevation prompt and non-administrator fallback.

---

## 4. Excluded Features (Explicitly Out-of-Scope for MVP)
- Multi-WAN routing, interface binding, or forcing an application onto a secondary physical adapter.
- Virtual Private Network (VPN) or WireGuard/OpenVPN tunnel creation.
- Deep Packet Inspection (DPI), payload inspection, or TLS decryption.
- Per-application DNS or proxy redirection.
- System-wide firewalling, domain blocking, or parental control filtering.
- Cloud dashboard, telemetry collection, or remote control webhooks.
- Custom kernel driver development (MVP strictly utilizes authentic, signed WinDivert releases).
