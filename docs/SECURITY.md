# NetLanes Security & Driver Safety Policy

## 1. Privilege Model & Elevation Requirements

### Why Administrator Rights are Required for Traffic Control
Windows restricts low-level packet capture and injection to kernel drivers and elevated processes to prevent unauthorized packet eavesdropping or spoofing. NetLanes uses **WinDivert** (Windows Packet Divert), which communicates with an authenticated kernel driver (`WinDivert64.sys`).
- Opening a WinDivert handle requires the calling process to possess administrative credentials (`SeDebugPrivilege` / Administrator security token).
- NetLanes features an explicit UAC restart trigger with clear messaging explaining why elevation is requested.

### Passive Non-Administrator Mode
When launched by a standard, non-elevated user:
- NetLanes **does not** attempt to load or open kernel drivers.
- NetLanes uses Windows IP Helper APIs (`GetExtendedTcpTable` / `GetExtendedUdpTable`) to discover active network sockets and display real-time connection bandwidth passively.
- The UI displays an "Elevation Required for Traffic Control" banner, allowing users to inspect traffic and configure lanes without kernel interception.

---

## 2. Privacy & Data Protection

### Zero Packet Payload Inspection or Retention
- **Headers Only**: NetLanes exclusively examines Layer 3 (IPv4/IPv6) and Layer 4 (TCP/UDP) headers to extract protocol, IP addresses, port numbers, and packet lengths.
- **Payload Privacy**: Packet payloads (e.g., HTTP bodies, TLS application data, user messages) are **never** inspected, recorded, decoded, or written to disk.
- **Zero Telemetry**: NetLanes transmits no telemetry, diagnostics, or crash reports to external servers or cloud services. All state resides locally.

### Local SQLite Database Security
- The configuration database (`netlanes.db`) is stored in the local application data directory (`%LOCALAPPDATA%\NetLanes\`).
- Only metadata is stored: lane definitions, canonical executable paths, user-configured limits, and application preferences.
- Transient flow records, IP addresses, and live packet counts are kept strictly in-memory and are never written to permanent storage.

---

## 3. Kernel Driver Safety & Operating System Integrity

### Driver Authenticity & Digital Signatures
- NetLanes bundles authentic, official WinDivert releases signed with valid Microsoft WHQL / Authenticode certificates.
- NetLanes **never**:
  - Modifies Windows Driver Signature Enforcement (DSE).
  - Prompts to enable Windows Test-Signing mode (`bcdedit /set testsigning on`).
  - Alters Windows Defender, Antivirus exclusions, or UAC policies.
  - Leaves orphaned kernel services running in the background after exit.

### Clean Driver Lifecycle & Unload
- When the NetLanes application terminates or traffic control is disabled, all WinDivert handles are closed via `WinDivertClose`.
- WinDivert automatically unloads the driver service when all open handles are closed, leaving the system in a clean, default state.

### Non-Invasive Network Operation
- NetLanes operates solely via packet pacing. It **never**:
  - Modifies DNS servers or hosts files.
  - Adds or modifies static IP routes or interface metrics.
  - Changes Windows Firewall rules.
  - Installs virtual TAP/TUN network adapters.
  - Alters WinHTTP, WinINet, or system proxy configurations.

---

## 4. Fail-Safe Operations & Emergency Controls

### Safe Overflow Pass-Through
If a sudden burst of high-throughput traffic threatens to overwhelm userspace processing queues, NetLanes invokes its **Safe Overflow Pass-Through** mechanism. Excess packets bypass token-bucket queues and are reinjected immediately rather than dropped, preserving network connectivity and preventing application crashes or connection timeouts.

### Instant Emergency Stop
The desktop UI provides a prominent, hotkey-accessible **Emergency Stop** button. Activating Emergency Stop immediately:
1. Suspends all token-bucket delay timers.
2. Flushes and transmits all queued packets immediately.
3. Closes all kernel divert handles.
4. Transitions NetLanes into passive pass-through mode.
