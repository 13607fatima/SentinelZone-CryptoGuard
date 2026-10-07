# SentinelZone CryptoGuard v0.22.2-rc.1

**PRE-RELEASE — UNSIGNED EVALUATION — NOT PRODUCTION READY**

Cross-platform Windows/Linux endpoint telemetry, explainable heuristic security risk, independent resource impact, durable local spool and optional ML shadow. High CPU/GPU utilization alone does not mean malware. Missing sensors remain null/unavailable/unknown. Persistence collection is read-only. The agent explains evidence and does not make unsupported compromise claims. Risk scores are not calibrated compromise probabilities.

ML is shadow-only; heuristics remain authoritative. Synthetic fixtures and training metrics do not establish real-world detection accuracy. No production ML model has been accepted.

Phase 21–22 is local agent/risk scope. Phase 23 production backend, enrollment, remote ingestion and Splunk transport are NOT IMPLEMENTED BY DESIGN. No monitored-workload process-kill or persistence-removal feature is included.

## Original measured evidence

- Acceptance: 55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE.
- Windows legacy + unified + migration: 139 PASS / 0 FAIL / 0 SKIPPED.
- Linux legacy + unified: 122 PASS / 0 FAIL / 0 SKIPPED.
- Native Windows/Linux replay parity: 10 PASS (370 rows).
- ML training methodology: 6 PASS.

These measurements come from the supplied original evaluation bundle. Repository preparation did not rerun the native .NET suites or hosted Actions and did not rebuild the attached binaries. See RELEASE_MANIFEST.md for actual new checks and limitations.

## Provenance

The prebuilt assets correspond to the original `SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip` (SHA-256 `5ac89e48dfd7017cc0f4dd90b5ba7fd942d584aac4603f6d6d0555f8fc2209e9`). The repository tag/GitHub-generated source archive represent the prepared standalone source with documentation, publication checks, a Debian documentation-path correction and locked Windows migration-test restore. Application source is unchanged; no new binary-parity claim is made for a rebuild from this tag.

Original SHA256SUMS/SBOM/release-manifest describe original release bytes. The GitHub-ready source ZIP has its own checksum. Keep required third-party notices and corresponding-source archives. The project owner has not yet selected a final repository license; no project-license grant is invented.

## Open gates

UNVERIFIED: both 72-hour soaks; both real OS reboots; physical NVIDIA, AMD and Intel GPUs; no-driver and permission-blocked physical GPU cases; physical temperatures; interactive desktop idle; broad real workloads; physical disk exhaustion; reference-host resource targets; production ML model acceptance; code signing; hosted GitHub Actions.

HOSTED CI: UNVERIFIED in the delivered preparation evidence. If a real run subsequently succeeds, add its URL and exact commit as new evidence rather than changing the meaning of the original report.

Windows assets: Setup, Portable ZIP and standalone core EXE. Keep the full portable payload for optional hardware helpers. Linux assets: native ELF, tarball and Debian package. All are evaluation assets. See the repository installation guides and GITHUB_RELEASE_ASSETS.md.
