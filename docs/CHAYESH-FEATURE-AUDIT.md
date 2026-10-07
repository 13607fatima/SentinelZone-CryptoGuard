# Chayesh feature audit

Commit: `9e08ca73f2a9abf08d5fc3700f3fefce8552bf3d`.

`collect_data_linux.py` lists **21 numeric candidate features**, not 22; metadata columns are timestamp, session and label. `train_model.py` can drop unavailable temperature, changing the count again. Our versioned contract has 22 explicitly named features and is not compatible with its pickle.

| Original feature | Decision |
|---|---|
| `cpu_total_percent` | Methodology reference only; no source/model/data copied. |
| `cpu_max_core_percent` | Methodology reference only; no source/model/data copied. |
| `cpu_mean_core_percent` | Methodology reference only; no source/model/data copied. |
| `cpu_core_std` | Methodology reference only; no source/model/data copied. |
| `cpu_core_count` | Methodology reference only; no source/model/data copied. |
| `cpu_spike_duration_sec` | Methodology reference only; no source/model/data copied. |
| `cpu_spike_count` | Methodology reference only; no source/model/data copied. |
| `cpu_temp_celsius` | Never use -1/0 for missing temperature; null plus status. |
| `memory_percent` | Methodology reference only; no source/model/data copied. |
| `memory_available_mb` | Methodology reference only; no source/model/data copied. |
| `swap_percent` | Methodology reference only; no source/model/data copied. |
| `net_bytes_sent_delta` | Original raw deltas lack elapsed units. Use bytes/sec with actual elapsed; disk collector remains unavailable. |
| `net_bytes_recv_delta` | Original raw deltas lack elapsed units. Use bytes/sec with actual elapsed; disk collector remains unavailable. |
| `net_sent_recv_ratio` | Methodology reference only; no source/model/data copied. |
| `disk_read_delta` | Original raw deltas lack elapsed units. Use bytes/sec with actual elapsed; disk collector remains unavailable. |
| `disk_write_delta` | Original raw deltas lack elapsed units. Use bytes/sec with actual elapsed; disk collector remains unavailable. |
| `process_count` | Methodology reference only; no source/model/data copied. |
| `top_process_cpu_percent` | Methodology reference only; no source/model/data copied. |
| `high_cpu_process_count` | Methodology reference only; no source/model/data copied. |
| `miner_process_detected` | Do not use a process-name label as reliable malicious ground truth. |
| `mining_pool_connection` | Port match alone is not evidence of mining or a high risk result. |

Original full-data median imputation and row splitting are not adopted. SentinelZone fits imputation only in grouped training folds and holds entire hosts out of final evaluation.

References: [collector](https://github.com/Chayesh/cryptoguard/blob/9e08ca73f2a9abf08d5fc3700f3fefce8552bf3d/collect_data_linux.py), [training](https://github.com/Chayesh/cryptoguard/blob/9e08ca73f2a9abf08d5fc3700f3fefce8552bf3d/train_model.py).