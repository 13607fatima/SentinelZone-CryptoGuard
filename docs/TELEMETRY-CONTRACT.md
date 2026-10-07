# Telemetry contract

Wire schema `1.2.0`; agent `0.22.2-rc.1`; ruleset və feature contract ayrıca versiyalanır. JSON snake_case-dir. Tam schema `contracts/telemetry.schema.json`, risk schema `contracts/risk.schema.json` içindədir. Köhnə 1.0.0 spool formatı bərpa üçün oxunur; yeni hadisələr 1.2.0-dır.

Envelope: schema_version, event_type, event_uid, agent_id, agent_version, sequence, observed_at, sample_duration_ms. Snapshot daxilində host, processes, connections, persistence, devices, sensors, coverage və collector errors var. Risk nəticələri envelope risk massivindədir; health envelope health sahəsindədir.

Host: platform, os_version, architecture, boot_id, hostname, logical_cpu_count, CPU/RAM və host network rates. Process: process_instance_id, pid, ppid (unknown=null), started_at, exe_path, sha256, arguments_redacted, cpu_percent_host_capacity, rss_bytes, trust. Köhnə `redacted_arguments` və `file_trust` adları keçid uyğunluğu üçün eyni dəyərin alias-larıdır.

Ölçmə obyekti: `{value,unit,status,source}`. `status=ok` olduqda value sonlu rəqəmdir; unknown/unsupported/permission_denied/temporarily_unavailable olduqda value=null. Sensor ayrıca sensor_id, device_id, label, source, unit, status saxlayır. Temperatur zonası avtomatik CPU sayılmır.

Risk: security_risk və uyğunluq alias-ı score, ayrıca resource_impact (ölçmə yoxdursa null), severity, assessment, assessment_source=heuristic, reasons/reason_codes, evidence, unknown_data, coverage, lifecycle, alert_id, first_seen, last_seen, ruleset_version, model_version, ml. Score faizlə compromise ehtimalı deyil.

Eyni normalize edilmiş Snapshot, eyni policy və eyni zaman ardıcıllığı deterministik risk verir. Platform fərqi yalnız real kollektorun coverage və unknown məlumatında görünür. Eyni coverage verilən replay-də coverage də eynidir. Replay monotonic zamanı envelope sample_duration_ms cəmindən qurur; observed_at IOC expiry üçündür.
