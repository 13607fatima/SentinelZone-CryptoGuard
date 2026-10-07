> TARİXİ BASELINE SƏNƏDİ: Bu fayl Linux 0.22.1-dən saxlanıb. 0.22.2-rc.1 üçün README.md, docs/TELEMETRY-CONTRACT.md, docs/ACCEPTANCE.md və release validation/ nəticələri əsasdır.

# CryptoGuard 0.22.1 acceptance status

This is an unsigned local candidate built on the requested Ubuntu 24.04 x64
host. It does not inherit the 0.22.0 release's container, replay or performance
results. Current evidence is delivered separately under release/validation/.

The Ubuntu managed regression suite passed 63 tests, including full-priority-
queue confirmed/resolved event recovery, crash-window duplicate delivery,
checkpoint IO failure, legacy state migration, expiry during backpressure,
cron key=value/quoted commands, indexed queue accounting and idle heartbeats.
All 37 synthetic telemetry fixtures passed full JSON Schema validation.

Native executable live collection, package installation, service restart,
90-second sampling with a 60-second watchdog, benign cron inventory and bounded
resource measurements have dedicated server-test reports. Only a report with
pass=true is evidence that its corresponding test completed.

Not validated by these tests: actual machine reboot, a 72-hour soak/offline
run, reference 8 CPU/16 GiB/1000-process performance, physical/multi-GPU and
desktop-idle coverage, Ubuntu 22.04/26.04 compatibility of this new build,
Windows collector/installer, dataset-level detection quality, signed release
provenance and public redistribution licensing.

The detection rules are unchanged and uncalibrated. High stable CPU plus mining
arguments alone reaches 40 points without another independent signal. A full
priority spool defers collection until its durable pending risk event is
accepted; new short-lived processes may be missed during that backpressure.
Complete filesystem exhaustion can prevent any further durable writes.

Phase 23 backend transport remains disabled. No mining program, external pool
traffic, active response or process termination feature is included.
