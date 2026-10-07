# RELEASE_MANIFEST — standalone CryptoGuard handoff

Prepared: 2026-10-07. Product version: **0.22.2-rc.1**.

**Repository handoff prepared; validation status: PARTIAL. Product: PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY.**

There are actual passing source/build tests, an explicitly recorded restricted-container live-test failure, a standard parallel-restore failure, and unverified native/platform/production gates. No GitHub repository, tag, release or hosted Actions run was created by this preparation.

## Source baseline and archive inspection

- Input: `SentinelZone-CryptoGuard-0.22.2-rc.1-FINAL.zip`.
- Input SHA-256: `66894c79a95d64f811b7453e918d42b2930aad25db654ff14e136e5f9443a925`.
- Original source: `release/source/SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip`.
- Source ZIP SHA-256: `5ac89e48dfd7017cc0f4dd90b5ba7fd942d584aac4603f6d6d0555f8fc2209e9`.
- Original source root: `SentinelZone.CryptoGuard/`, **245 files**.
- Full bundle inspected before source edits: 109 archive members, CRC checks, all 108 SHA256SUMS entries, nested ZIP CRCs and inventories, Linux tar payload and Debian contents. The existing `scripts/validate-release.py` also passed on the original bundle, checking source-manifest hashes and EXE/portable/ELF/tar/DEB byte consistency.
- Prepared archive root: exactly one directory, **`SentinelZone-CryptoGuard/`**, with `src/`, `tests/`, build configuration and README immediately inside it.

## Change inventory

| Item | Count |
|---|---:|
| Prepared repository files | 316 |
| Added | 71 |
| Modified | 12 |
| Removed | 0 |
| Original files unchanged | 233 |
| C# application/test files byte-identical | 63 |
| Protected C#/contract/schema/rule/license/notice files byte-identical | 95 |
| Original validation evidence files preserved byte-for-byte | 35 |

Application C#, existing tests, telemetry/risk semantics, contracts, schemas, rules, migration implementation and architecture remain unchanged. External NuGet package entries in the corrected lock are identical. All `licenses/`, `SOURCES.md` and `THIRD-PARTY-NOTICES.txt` bytes are preserved. Original binaries are **not** in the source repository; corresponding-source license ZIPs retain their upstream resources.

### Focused corrections

1. **Debian docs case mismatch:** `debian/cryptoguard-agent.docs` referenced missing lowercase `docs/acceptance.md`. Corrected to existing `docs/ACCEPTANCE.md`. A regression fails against the original source and passes after correction.
2. **Stale Linux portable lock:** exact SDK locked restore reported **NU1004** because the lock omitted the existing ML project and current risk/core dependency edges. Regenerated only `src/CryptoGuard.Agent.Linux/packages.portable.lock.json` with SDK 10.0.401. Verified no external package/version/content hash changed. A project-graph regression fails against baseline and passes after correction; serialized locked restore, source build and 122 Linux tests then passed.
3. **Locked Windows migration validation:** `Publish.ps1` now explicitly restores `tests/WindowsMigration` in locked mode and runs it with `--no-restore`. Existing test coverage is retained; native PowerShell execution remains UNVERIFIED.
4. Existing four-job, pinned cross-platform CI retained; source/publication checks and explicit Linux solution restore/build added. No fake badge or hosted-run claim.

Original archive/source and targeted pre-edit backups were retained outside deliverables. No private backups or build caches are shipped. No application rewrite or Phase 23 implementation was performed.

### Modified files

- `.github/workflows/ci.yml`
- `.gitignore`
- `ARCHITECTURE.md`
- `CHANGELOG.md`
- `README.md`
- `debian/cryptoguard-agent.docs`
- `docs/ARCHITECTURE.md`
- `docs/KNOWN-LIMITATIONS.md`
- `docs/RELEASE.md`
- `docs/TESTING.md`
- `packaging/windows/Publish.ps1`
- `src/CryptoGuard.Agent.Linux/packages.portable.lock.json`

### Added files

- `.github/ISSUE_TEMPLATE/bug_report.yml`
- `.github/ISSUE_TEMPLATE/feature_request.yml`
- `.github/pull_request_template.md`
- `CONTRIBUTING.md`
- `GITHUB_RELEASE_ASSETS.md`
- `LICENSE-TODO.md`
- `PRIVACY.md`
- `PUSH_TO_GITHUB.md`
- `RELEASE.md`
- `RELEASE_MANIFEST.md`
- `SECURITY.md`
- `docs/CONFIGURATION.md`
- `docs/GITHUB-RELEASE-NOTES.md`
- `docs/PUBLICATION-CHECKS.md`
- `docs/release-evidence/v0.22.2-rc.1/README.md`
- `docs/release-evidence/v0.22.2-rc.1/SHA256SUMS`
- `docs/release-evidence/v0.22.2-rc.1/release-manifest.json`
- `docs/release-evidence/v0.22.2-rc.1/sbom.spdx.json`
- `docs/release-evidence/v0.22.2-rc.1/source-tree-manifest.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/ACCEPTANCE.md`
- `docs/release-evidence/v0.22.2-rc.1/validation/BUG-STATUS.md`
- `docs/release-evidence/v0.22.2-rc.1/validation/CHANGED-FILES.md`
- `docs/release-evidence/v0.22.2-rc.1/validation/DETECTION-EVALUATION.md`
- `docs/release-evidence/v0.22.2-rc.1/validation/ML-EVALUATION.md`
- `docs/release-evidence/v0.22.2-rc.1/validation/acceptance-matrix.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/artifact-verification.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/detection-evaluation.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/license-inventory.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/linux-live-integration.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/linux-resource.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/linux-service-report.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/linux-soak-smoke.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/linux-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/locked-build-windows.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/migration-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/ml-evaluation.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/packaging-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/parity-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/rebuild-parity-linux.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/rebuild-parity-windows.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/release-byte-evidence-linux.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/release-byte-evidence-windows.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/research-only/ml-evaluation.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/research-only/model-manifest.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/research-only/model.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/schema-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/ssh-cleanup.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/test-summary.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/training-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/unified-linux-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/unified-windows-tests.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/windows-resource.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/windows-soak-smoke.json`
- `docs/release-evidence/v0.22.2-rc.1/validation/windows-tests.json`
- `docs/repository-preparation/README.md`
- `docs/repository-preparation/build-validation.json`
- `docs/repository-preparation/feature-schema.json`
- `docs/repository-preparation/gitignore.json`
- `docs/repository-preparation/linux-legacy-tests.json`
- `docs/repository-preparation/linux-native-replay.json`
- `docs/repository-preparation/linux-unified-tests.json`
- `docs/repository-preparation/live-integration.json`
- `docs/repository-preparation/ml-methodology.json`
- `docs/repository-preparation/packaging-static.json`
- `docs/repository-preparation/preservation.json`
- `docs/repository-preparation/python-environment.json`
- `docs/repository-preparation/repository-tests.json`
- `scripts/check-repository.py`
- `scripts/secret-scan.py`
- `tests/Repository/test_packaging.py`
- `tests/Repository/test_secret_scan.py`

### Removed files

None. Flattening changes the outer directory name only; it does not delete the baseline source.

## Actual preparation validation

Tests were performed from a clean candidate extraction. The final ZIP is separately checked for exact file inventory, safe paths, one top-level directory and CRC/checksum. Report-only/documentation updates after runtime tests do not change the tested application bytes.

| Check | Result | Actual scope |
|---|---|---|
| Original release verifier | PASS | 108 checksums, 245 original source hashes and release payload parity |
| Exact SDK integrity | PASS | SDK 10.0.401 archive SHA-512 matches the original pinned container definition |
| Linux locked restore | PARTIAL | Serialized restore PASS; ordinary parallel restore exited 1 in this container |
| Linux solution Release build | PASS | Serial MSBuild; 0 warnings, 0 errors |
| Linux legacy harness | PASS | **63 PASS / 0 FAIL / 0 SKIPPED**, unchanged suite |
| Linux unified contract/risk harness | PASS | **59 PASS / 0 FAIL / 0 SKIPPED**, unchanged suite |
| Python ML methodology | PASS | **6 PASS / 0 FAIL**, rerun with all five pinned research dependencies |
| Packaging/publication regressions | PASS | **6 PASS / 0 FAIL**; two verified baseline defects reproduce as expected failures on original source |
| Linux binary replay | PASS | 10 heuristic/shadow cases, each repeated twice; 20 invocations, 370 result rows; original binary |
| ML isolation in Linux replay | PASS | 5 fixtures retain identical heuristic fields with shadow enabled |
| JSON schemas | PASS | 6 schema meta-validations; 188 fixture/sample telemetry events; 370 risk objects; 640 synthetic 22-feature records |
| Existing bounded live integration | **FAIL** | IPv4 process-ownership assertion failed; container permission restrictions independently demonstrated |
| Static publication checks | PASS | 9 checks: root, project refs, JSON/Python/XML, Debian docs, README targets, Action pins, runtime hygiene, public paths, original acceptance counts |
| Git ignore behavior | PASS | 18 ignored cases and 11 required/allowed cases |
| Pattern secret scan | PASS | Source and staged index bytes; nested third-party archives; exact reviewed synthetic/expression matches only |
| YAML/shell packaging checks | PASS | Four CI jobs/permissions, two issue templates, shell syntax for three scripts; not a Windows/installer execution result |
| License/source/evidence preservation | PASS | Byte comparisons, as counted above |
| Windows native build/tests | UNVERIFIED | No native Windows/PowerShell execution environment |
| Native Linux/Debian package rebuild | UNVERIFIED | Compiler/debhelper prerequisites unavailable; apt failed on container identity restrictions |
| Fresh native Windows/Linux parity | UNVERIFIED | This run has Linux only; historical native parity evidence is retained separately |
| Hosted GitHub Actions | **UNVERIFIED** | Workflow inspection is not hosted execution |
| Full Git-history/entropy scan | UNVERIFIED | No supplied history or dedicated entropy scanner; index/source pattern screening actually ran |

### Execution details and failed checks

The following Linux commands passed with the exact SDK, without weakening locking or tests:

```bash
dotnet restore SentinelZone.CryptoGuard.Linux.slnx --locked-mode --configfile NuGet.Config --disable-parallel -m:1 -p:BuildInParallel=false
dotnet build SentinelZone.CryptoGuard.Linux.slnx -c Release --no-restore -m:1 -p:BuildInParallel=false
dotnet tests/CryptoGuard.Tests/bin/Release/net10.0/CryptoGuard.Tests.dll /temporary/test-output
dotnet tests/Contract/bin/Release/net10.0/CryptoGuard.Unified.Tests.dll --report /temporary/unified-report.json --fixtures /temporary/generated-fixtures
```

Ordinary parallel restore exited 1, including after the lock correction. Diagnostic output showed a `ConvertToAbsolutePath` task failure without an ordinary MSBuild error; the underlying cause was not established. Serialized execution passed. This failure is retained in `docs/repository-preparation/build-validation.json`, not hidden as a green default-command result.

The existing `scripts/live-integration.py` ran the original Linux binary and failed at its IPv4 ownership assertion. Agent exit/CPU coverage/reference tolerance checks completed first; later risk/redaction assertions were not reached. The agent emitted permission-denied/partial coverage. A separate sibling-process probe of namespace/fd links returned PermissionError errno 13. This demonstrates an environment limitation; it does not establish an application defect or supersede the supplied VM evidence. Rerun unchanged on a supported disposable host. No raw live exports, process identities, spool or runtime logs are included.

Pinned Python run: Python 3.12.14; NumPy 2.2.6, SciPy 1.15.3, scikit-learn 1.7.2, joblib 1.5.2, threadpoolctl 3.6.0; schema validator jsonschema 4.25.1. An earlier six-test run also passed using preinstalled newer dependencies; the final reported run uses the repository pins. Structured reports are under `docs/repository-preparation/`.

### Security and publication review

No observed real credentials, private keys/certificates, environment files, personal home paths, SentinelZone lab IPs, live endpoint exports, runtime identity/state/spool/logs, build caches or product release binaries are included. Ignore rules were tested, not merely inspected. Source and staged-byte pattern scans read nested third-party archives too. The unrestricted Git whitespace check reported original whitespace in five preserved third-party notice/license files; their bytes were deliberately retained. The documented first-party whitespace check excludes only `licenses/**` for cosmetic formatting, while secret scanning still includes all vendor content.

Fake redaction strings, the clearly synthetic JWT/loopback fixtures, documentation language, PowerShell command names and upstream variable/function expressions received exact path/match dispositions. Entire test/vendor directories were not exempted. Scanner limitations are documented: this is not a guarantee against every possible secret or a history/entropy audit. Synthetic test values are safe because they are explicitly constructed test input, not service credentials.

## Preserved original product evidence

These are historical release measurements, not invented new acceptance results:

| Suite/status | Preserved value |
|---|---|
| Acceptance | **55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE** |
| Windows legacy + unified + migration | 139 PASS / 0 FAIL / 0 SKIPPED |
| Linux legacy + unified | 122 PASS / 0 FAIL / 0 SKIPPED |
| Native Windows/Linux replay parity | 10 PASS |
| ML training methodology | 6 PASS |

Original manifests/SBOM/checksums remain under `docs/release-evidence/v0.22.2-rc.1/`. They describe the original bundle, not this ZIP or a future rebuilt binary. Source tag and prebuilt baseline provenance are explicitly separated in release notes. No signing status is invented.

## Remaining UNVERIFIED production gates

All 17 remain: 72-hour Windows soak; 72-hour Linux soak; real Windows reboot; real Linux reboot; physical NVIDIA, AMD and Intel GPUs; physical GPU with missing driver; physical GPU with denied permissions; physical temperature sensors; interactive desktop idle; broad real-workload evaluation; physical disk exhaustion; 72-hour reference-host CPU/RAM targets; production ML model acceptance; code signing; hosted GitHub Actions.

Project-license selection also remains an owner decision. This is not an open-source license declaration or production-readiness certification. Native packaging/Windows verification and the container-specific restore/live-test limitations remain explicit handoff items.

## Known limitations and phase boundary

No per-process GPU collector; no guarantees for protected/short-lived processes, unloaded Windows hives, namespaces or unavailable sensors; offline/cache trust limits; retained migration spool/rollback disk cost. Unknown is not zero/healthy. High resource utilization is not malware. Risk/resource are separate, heuristic scores are not probabilities, persistence is read-only and ML is shadow-only. Synthetic evaluation does not prove real-world accuracy.

Phase 23 backend, enrollment, production remote ingestion and Splunk transport remain **NOT IMPLEMENTED BY DESIGN**. No workload kill or persistence-removal capability was added. See `docs/KNOWN-LIMITATIONS.md` and `docs/PHASE23-HANDOFF.md`.

## Publishing and assets

`PUSH_TO_GITHUB.md` contains first-push and existing-repository procedures, staged scanning and real Actions verification. `GITHUB_RELEASE_ASSETS.md` maps each original asset to its exact filename, size and SHA-256 and supplies prerelease/upload commands. `docs/GITHUB-RELEASE-NOTES.md` visibly retains PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY.

The repository owner is intentionally `USERNAME` in commands. The wider integration reference is https://github.com/Ulvu11/SentinelZone-SOC-Platform; this component does not depend on a checkout or lab address.

## Archive checksum

The final ZIP's SHA-256 is recorded in the external file `SentinelZone-CryptoGuard-GitHub-Ready-v0.22.2-rc.1.zip.sha256` and appended to the standalone delivered copy of this manifest. The manifest inside the ZIP deliberately cannot contain the hash of its own enclosing archive; that would be circular. This internal report is the preparation snapshot; the external report adds final archive verification and checksum only.
