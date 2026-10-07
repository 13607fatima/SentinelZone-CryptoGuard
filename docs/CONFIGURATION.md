# Configuration reference

Configuration adapters are platform-specific. Keys are snake_case. Use generated/default Windows `config.json` and Linux `packaging/agent.json` as the starting point, not a cross-platform copy of the same document.

| Purpose | Windows key/default | Linux key/default |
|---|---|---|
| Sample interval | `sample_seconds`: 5 | `sample_seconds`: 5 |
| Network interval | `network_seconds`: 10 | `network_seconds`: 10 |
| Hardware interval | `hardware_seconds`: 10 | `sensor_seconds`: 10 |
| Summary/export | `summary_seconds`: 60 | `export_seconds`: 60 |
| Inventory/persistence | `inventory_seconds`: 900; `persistence_seconds`: 900 | Same |
| Spool bytes | `spool_max_bytes`: 268435456 | `spool_limit_bytes`: 268435456 |
| Reserved risk bytes | `risk_reserve_bytes`: 33554432 | Same |
| Retention | `retention_days`: 7 | `retention_hours`: 168 |
| Hardware opt-in | `enable_hardware`: false; `enable_cpu_hardware`: false | Provider discovery subject to availability |
| ML | `ml_shadow_enabled`: false; path/hash unset | Same |

Windows state comes from `--data` or the ProgramData default. Linux `state_directory` defaults to `/var/lib/cryptoguard`, `collector_socket` to `/run/cryptoguard/collector.sock`; CLI overrides are `--state` and `--socket`. Linux also has `max_processes=8192`, `include_hostname=false`, `confirm_seconds=60`, `resolve_seconds=60`, `high_cpu_percent_host_capacity=15`, `mining_authorization=unknown`. Windows normalization supplies the shared policy; do not assume all Linux fields are accepted by its strict adapter.

Windows authorization uses `authorization` with `sha256`, `exe_path`, `decision` (allowed/denied), and optional `signer`. Linux uses `authorized_workloads` with `sha256`, `exe_path`, `policy_id`, and optional `publisher`. Review the actual classes in `src/CryptoGuard.Contracts/LegacyWindows/Models.cs` and `src/CryptoGuard.Contracts/Models.cs` before writing entries. Do not authorize by executable name alone.

Windows IOC entries use `kind`, `value`, `source`, `confidence` (high) and `expires_at`; Linux uses `type`, `value`, `source`, numeric `confidence` and `expires_at`. Supported IOC kinds/types are IP and SHA-256. These are evidence inputs, not production transport settings. Empty lists are the default.

For optional research ML, set `ml_model_path` to a reviewed local model file and `ml_model_sha256` to its independently verified 64-character SHA-256. A placeholder must be replaced before enabling. Model errors do not override heuristics. There is no accepted production model and no remote model download/enrollment setting.

Use generic local paths in documentation. Keep real config/state out of Git. Preserve unknown sensor values rather than adding zero-valued fallback readings. See [ML-MODEL.md](ML-MODEL.md), [PRIVACY.md](../PRIVACY.md) and [TELEMETRY-CONTRACT.md](TELEMETRY-CONTRACT.md).
