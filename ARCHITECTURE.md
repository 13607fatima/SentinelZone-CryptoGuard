# Architecture

CryptoGuard 0.22.2-rc.1 is a local Phase 21–22 agent. Phase 23 is **NOT IMPLEMENTED BY DESIGN**. See the conceptual diagram in [README.md](README.md#architecture).

## Components and boundaries

| Component | Responsibility |
|---|---|
| `CryptoGuard.Contracts` | Canonical events, snapshots, measurement availability and interfaces; compatibility Windows models |
| `CryptoGuard.Platform.Windows` | Native Windows host/process/network/trust and persistence observation; normalization adapter |
| `CryptoGuard.Platform.Linux` | Linux host/process/network/trust and persistence observation |
| `CryptoGuard.Agent.Windows` | CLI/service lifecycle, isolated child collectors, config/state migration |
| `CryptoGuard.HardwareHost.Windows` | Optional separate LibreHardwareMonitor sensor process |
| `CryptoGuard.Agent.Linux` | Standalone/core/collector modes and Unix-socket communication |
| `CryptoGuard.Core` | Spool, checkpoint/recovery, local commands, privacy and test delivery boundary |
| `CryptoGuard.Risk` | Shared heuristic engine, independent resource impact and replay |
| `CryptoGuard.ML` | Versioned feature extraction and optional verified shadow forest |
| `CryptoGuard.Replay` | Shared replay entry point |

Both agents use the shared rule engine, durable spool and risk checkpoint store. Compatibility namespaces retain old Windows models/behavior for migration and regression coverage; they are not a second production backend.

Windows expensive WMI/persistence and hardware operations use bounded child processes. The installed service runs as LocalService. Interactive idle requires a session helper; Session 0 is not treated as verified desktop input time. The agent may stop its own timed-out helpers as lifecycle management, not terminate monitored workloads.

Linux systemd mode separates a `cryptoguard` user core from a restricted root collector. The collector observes the OS and communicates through a Unix socket. Both units have hardening settings in `debian/`. Standalone mode collects with the caller's permissions.

## Data and semantics

Process CPU is `100 * delta_process_cpu_seconds / (actual_elapsed_seconds * logical_cpu_count)`. Windows identity hashes agent + boot + PID + creation UTC ticks; Linux hashes agent + boot + PID + start ticks. PID reuse must not inherit another process's risk history.

Snapshot interval values remain distinct from weighted summaries. Host network rates use actual elapsed time and non-loopback interface counters; interface/counter discontinuity becomes unknown. Device GPU utilization is not process GPU utilization. Missing sensors retain null values and availability status.

The heuristic engine emits reasons, evidence, unknown data, coverage and lifecycle. Resource impact and security risk are independent. ML shadow output cannot replace heuristic decisions. None of these scores is a calibrated compromise probability. Persistence collection is observational/read-only.

## Durability and integration

Risk state and pending risk events are checkpointed before further detection progress. Events retain immutable identifiers through retries. Spool quota/retention can discard ordinary telemetry with explicit dropped-event accounting; pending priority risk can cause backpressure. Old migration JSON and rollback backups are retained, not silently deleted.

Local status/export is implemented. `IEventSink.AcceptAsync` and `LocalFakeReceiver` define/test a future durable-ACK boundary. There is no configured remote sink, production backend, enrollment or Splunk transport. Future transport must preserve deduplication, availability, risk/resource semantics and redaction. See [Phase 23 handoff](docs/PHASE23-HANDOFF.md).
