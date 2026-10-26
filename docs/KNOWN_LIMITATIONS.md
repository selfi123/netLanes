# NetLanes Known Limitations & Technical Constraints

## 1. Network Protocol & Transport Limitations

### TCP Traffic Shaping (Inbound & Outbound)
- **Outbound TCP**: Supported and highly accurate. Outbound TCP packets are paced directly in user-mode queues before transmission, effectively constraining upload rates.
- **Inbound TCP**: Supported via arrival pacing. NetLanes delays inbound TCP packets before handing them up to the Windows TCP/IP stack. As the application's receive buffer fills and TCP window updates/acknowledgments are delayed, the remote sender's congestion control algorithm (CUBIC, BBR, Reno) throttles transmission.
- *Notice*: For extremely small file transfers (< 100 KB), TCP slow-start dynamics may complete before the rate-limiting feedback loop fully takes effect.

### UDP and QUIC (HTTP/3)
- **Outbound UDP / QUIC**: Supported. Datagrams are paced according to token-bucket availability.
- **Inbound UDP / QUIC**: Pacing delays datagrams. QUIC includes robust congestion control and adapts to the delayed delivery similarly to TCP. Unreliable raw UDP (such as DNS queries or certain VoIP streams) is exempt from shaping to prevent audio stutter or packet timeouts.

### Raw Sockets & ICMP
- Raw sockets and ICMP ping/traceroute packets are passed through unshaped to ensure network diagnostic tools operate accurately.

---

## 2. Process Attribution & Flow Correlation Edge Cases

### Ephemeral & Extremely Short-Lived Connections
- When an application opens a socket, sends a single HTTP request, and terminates the connection within milliseconds (e.g., telemetry pings), the connection may close before the periodic IP Helper snapshot captures the owning PID. NetLanes marks such transient packets as "Unattributed Flow" and passes them through without interference.

### Shared Service Processes (`svchost.exe`)
- Multiple Windows background services (such as Windows Update, BITS, and Windows Time) often share single instances of `svchost.exe`.
- NetLanes identifies `svchost.exe` as a system-protected process and excludes it from default traffic shaping to prevent accidental disruption to core Windows OS operations.

### Virtualization & Containers (WSL, Hyper-V, Sandbox)
- Network traffic emanating from WSL2 or Hyper-V virtual machines flows through virtual network switches (`vEthernet`). Packets from within WSL2 appear to the host as belonging to the virtual switch interface or `vmwp.exe` rather than the Linux binary (e.g., `apt` or `wget`).

### Loopback Traffic (`127.0.0.1` and `::1`)
- Local inter-process communication over loopback is filtered out of active traffic shaping by default (`!loopback`) to prevent accidental interception loops and avoid slowing down local database or IPC servers.

---

## 3. High-Throughput Hardware Constraints
- Pacing packets in user-mode via WinDivert introduces context switching between kernel space and user space. On multi-gigabit connections (e.g., 2.5 Gbps, 5 Gbps, 10 Gbps LANs) running at line rate, user-mode packet divert can incur noticeable CPU overhead. NetLanes is optimized for broadband internet connections up to 1–2 Gbps.
- NetLanes incorporates a **Safe Overflow Pass-Through** safeguard to release packets immediately if queue thresholds are exceeded.
