# NetLanes Test Plan & Verification Strategy

## 1. Overview
The NetLanes test strategy consists of two tiers:
1. **Unit Tests (`NetLanes.Core.Tests`)**: Fast, deterministic, zero-network-dependency tests verifying business logic, token-bucket rate limiting under a mock clock, unit conversion, path normalization, flow correlation, and SQLite persistence.
2. **Integration Tests (`NetLanes.Networking.Tests`)**: In-memory and local socket network tests validating socket flow tracking, bounded queues, safe overflow, token-bucket pacing under simulated data streams, driver fallback on privilege denial, and concurrent multi-connection handling.

---

## 2. Unit Test Matrix (`NetLanes.Core.Tests`)

| Test Suite | Test Case | Expected Behavior |
| :--- | :--- | :--- |
| **TokenBucketTests** | `ExactRate_WithinTolerance` | Injects packets over simulated time; verified byte count matches configured bps within 2% margin. |
| **TokenBucketTests** | `BurstAllowance_Honored` | Allows instant initial burst up to bucket capacity, then paces subsequent packets. |
| **TokenBucketTests** | `Unlimited_PassesImmediately` | Unlimited mode returns 0 delay for all packet sizes. |
| **TokenBucketTests** | `ControllableTimeProvider` | Advances time explicitly using `FakeTimeProvider`; produces deterministic pacing. |
| **UnitConverterTests** | `BytesPerSecond_ToDisplay_MiB` | Converts `10,485,760` bps to `10.00 MiB/s`. |
| **UnitConverterTests** | `Display_ToBytesPerSecond` | Parses `"25 Mbps"` to `3,125,000` bps; parses `"5 MiB/s"` to `5,242,880` bps. |
| **UnitConverterTests** | `ZeroAndNegative_Handling` | Distinguishes explicit `0` (block) from `null` / `-1` (unlimited). |
| **PathMatcherTests** | `CaseInsensitive_WindowsPaths` | Matches `c:\program files\steam\steam.exe` with `C:\Program Files\Steam\STEAM.EXE`. |
| **PathMatcherTests** | `PathsWithSpaces_AndUnicode` | Handles paths like `D:\Games\日本語\App.exe` and spaces reliably. |
| **PathMatcherTests** | `TrailingSlashesAndShortNames` | Normalizes path formats to canonical lowercase. |
| **FlowCorrelationTests** | `ReverseDirection_Correlation` | Outbound flow `(TCP, 192.168.1.100:50000, 93.184.216.34:80)` matches inbound packet `(TCP, 93.184.216.34:80, 192.168.1.100:50000)`. |
| **FlowCorrelationTests** | `IPv4AndIPv6_Normalization` | Handles IPv4-mapped IPv6 (`::ffff:192.168.1.1`) correctly. |
| **FlowCorrelationTests** | `MultipleConnections_SamePID` | Multiple active ports for the same PID are aggregated to the owning application. |
| **FlowCorrelationTests** | `ProcessTermination_Invalidation`| When a PID terminates or start time changes, cached association is purged. |
| **RulePrecedenceTests**| `AppRule_Overrides_Lane` | App-specific custom limit takes precedence over assigned lane default. |
| **RulePrecedenceTests**| `LaneDefault_Inherited` | If app custom limit is null, assigned lane limit is enforced. |
| **RulePrecedenceTests**| `Unassigned_UnlimitedFallback`| Unassigned applications default to unlimited traffic pass-through. |
| **PersistenceTests** | `Migrations_ApplyCleanly` | Creates schema with version 1; checks tables, foreign keys, and indexes. |
| **PersistenceTests** | `LanesAndRules_PersistAndReload`| Inserts custom lanes and application assignments; survives database reload. |
| **PersistenceTests** | `CorruptDatabase_Recovery` | Backs up corrupt SQLite file and recreates a clean default database gracefully. |

---

## 3. Integration Test Matrix (`NetLanes.Networking.Tests`)

| Test Suite | Scenario | Verification Method |
| :--- | :--- | :--- |
| **TrafficShapingTests** | Single-connection socket throttling | Runs a local TCP server and client; sends 10 MiB of data with a 2 MiB/s cap; verifies elapsed duration is ~5 seconds. |
| **TrafficShapingTests** | Unthrottled application concurrent test | Concurrently streams data through a capped client and an uncapped client; verifies only the capped client is throttled. |
| **TrafficShapingTests** | Limit removal mid-stream | Dynamically changes rate limit from 1 MiB/s to Unlimited during an active transfer; verifies throughput instantly spikes to line speed. |
| **TrafficShapingTests** | Disable control under load | Toggles "Disable Control" while multiple streams are active; verifies all packets immediately pass without dropped connections. |
| **BoundedQueueTests** | Queue saturation & safe overflow | Pushes packets at 100 MiB/s through a 1 MiB/s token bucket; verifies queue does not exceed max packet limit and surplus traffic passes without process crash. |
| **BoundedQueueTests** | Emergency Stop | Triggers emergency stop with 1,000 packets queued; verifies all queues flush instantly and zero packets are lost. |
| **DriverLifecycleTests**| Unprivileged user fallback | Simulates unprivileged access; verifies engine transitions gracefully to `MonitorOnly` with descriptive error notice. |
| **DriverLifecycleTests**| Safe shutdown and disposal | Initializes and disposes engine 50 times in rapid succession; verifies no handle leaks or hanging threads. |

---

## 4. Manual Verification Scenarios
- **Scenario A**: High-throughput download test via `curl` / `Invoke-WebRequest` against a test endpoint with NetLanes throttling enabled.
- **Scenario B**: Concurrent Steam download + YouTube 4K stream test (see [BUILD_AND_RUN.md](file:///d:/docs and projects/projects/netLanes/docs/BUILD_AND_RUN.md)).
- **Scenario C**: Sleep / Resume & Network Adapter Disconnect test to verify flow table self-healing.
