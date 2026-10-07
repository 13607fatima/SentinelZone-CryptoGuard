# Known limitations

**0.22.2-rc.1 — PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**

- Physical NVIDIA/AMD/Intel GPU and temperature accuracy requires real hardware evidence. A VM's correct null/unavailable behavior does not establish physical accuracy.
- Windows optional hardware needs the helper directory and suitable permissions/drivers. CPU low-level sensors require additional opt-in. The agent does not install drivers or control hardware.
- There is no per-process GPU collector. Device usage is not assigned to processes.
- Interactive idle depends on a desktop/session helper or Linux graphical-session hints. Headless/Session 0 unknown is valid; logind hints are not a full input audit.
- Protected and short-lived processes, unloaded Windows user hives, namespaces and permissions can limit coverage. Partial inventory cannot prove a process or persistence mechanism is absent.
- Windows Authenticode uses offline/cache information; revocation freshness is not assured. Linux package-origin metadata is not repository-signature assurance.
- Legacy spool JSON and rollback backups remain on disk. Migration/backup may require additional space. Quota pressure causes explicit drop accounting or risk backpressure; backups are an administrator's responsibility.
- ML is shadow-only. Broad host/workload dataset acceptance is incomplete. Synthetic metrics prove pipeline behavior, not detection effectiveness. No production model is bundled/accepted.
- RandomX experimental detection is not included.
- Production backend, enrollment, remote ingestion and Splunk transport are not implemented by design. There is no workload process-kill or persistence-removal mechanism.
- The project owner has not selected a final repository license. Supply-chain inventories are not distribution approval or proof of code signing.
- Hosted GitHub Actions has not been executed as part of this preparation. The measured Linux source build does not prove Windows builds or installer execution.

All 17 production gates remain UNVERIFIED: both 72-hour soaks, both actual OS reboots, physical NVIDIA/AMD/Intel, no-driver/permission-blocked GPU cases, physical temperatures, interactive idle, broad real workloads, physical disk exhaustion, reference-host resource targets, production ML model acceptance, signing and hosted Actions. See the immutable [acceptance matrix](release-evidence/v0.22.2-rc.1/validation/acceptance-matrix.json).

The original release includes short resource measurements and lifecycle evidence with bounded scope. Preserve that scope. Current repository-preparation limitations and results are separate in [RELEASE_MANIFEST.md](../RELEASE_MANIFEST.md).
