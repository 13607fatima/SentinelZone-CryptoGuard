> TARİXİ BASELINE SƏNƏDİ: Bu fayl Linux 0.22.1-dən saxlanıb. 0.22.2-rc.1 üçün README.md, docs/TELEMETRY-CONTRACT.md, docs/ACCEPTANCE.md və release validation/ nəticələri əsasdır.

# Telemetry contract 1.0.0

Every event has schema_version, event_type, event_uid, agent_id, agent_version,
durable monotonic sequence, UTC observed_at, sample_duration_ms, snapshot, risk,
and health. Validate JSONL against schemas/telemetry.schema.json. `validate` also
checks envelope and null/status consistency without an external dependency.

Agent ID is a random UUID created in private state at first run. Packaging never
ships an ID. A corrupt identity stops startup rather than silently changing ID.
Process identity is SHA-256(agent UUID + boot ID + PID + start ticks). Event UUID
is assigned once and survives local retries. Acknowledgement is by that UUID.

Process CPU is 100 * delta process CPU seconds / (actual elapsed seconds * online
logical CPU count). One fully occupied CPU on a 4 CPU VM is 25 percent of host
capacity. Initial/reset samples have null value and warm_up/unavailable status.
Host CPU uses /proc/stat deltas excluding guest fields from the sum. RAM uses
MemTotal and MemAvailable. Network rates are host-namespace bytes/actual seconds,
never attributed to processes. Counters that reset or interfaces that change
produce warm-up readings. Connections include namespace, inode, family, state,
endpoints and known/unknown process owner. Cached ownership never crosses PID reuse.

All measurements carry unit, source and status. Unknown is null, never synthetic
zero. GPU device measurements are not mapped onto process GPU. Thermal labels
and driver sources are preserved; thermal_zone0 is not assumed to be CPU.
Process creation count is snapshot-observed new identities, not an OS event count.

Linux package metadata is trust_type=debian_package, not a digital signature.
The optional MD5 comparison is local dpkg metadata integrity only; SHA-256 is the
security identity. Package origin never vetoes mining evidence.

Raw process arguments are only inspected in collector memory. All argument
values and unknown flags are discarded before IPC, spool, logs and export.
Only a small fixed switch vocabulary plus mining-combination booleans survives.
No environment variables, packet payloads, password/token values, wallet strings
or URL credentials are saved. Hostname is redacted by default. Executable paths
and remote IPs remain operational evidence and may identify a local user/system.
Persistence entry locations redact /home usernames. Spool is private (0700).

CPU/process reads occur every 5 seconds; network/hardware every 10; inventory and
persistence every 15 minutes plus bounded suspicious-process refresh. Ordinary
telemetry is a 60-second summary; risk lifecycle changes persist immediately.
Replay requires full normalized sample history for identical state evolution.
Replaying 60-second summaries alone does not reproduce the missing 5-second
samples; the included fixture supplies the complete sample sequence.

Risk rules are deterministic, versioned and uncalibrated initial rules. High CPU,
RAM, temperature, a port number, a process name or persistence alone cannot
confirm mining. Authorization is separate local policy (unknown by default).
Allowlist entries require exact SHA-256, executable path and a policy ID.
Domain IOC correlation is unavailable without a verified passive DNS source.

Phase 23 interfaces: IEventSink, ISpoolStore and LocalFakeReceiver. Export is
non-destructive. Only an explicit acknowledgement removes a transmitted event.
RetryDelay exposes bounded exponential jitter; no live transport is enabled.

Agent 0.22.1 retains telemetry contract 1.0.0 and ruleset 0.22.0-rules.1.
Its private checkpoint format combines lifecycle state and one pending risk
event; this file is not part of the telemetry contract. Collection backpressure
is logged as risk_delivery_deferred; expiry and idle heartbeat work continues.
