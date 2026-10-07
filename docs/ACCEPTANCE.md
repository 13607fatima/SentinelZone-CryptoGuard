# 0.22.2-rc.1 acceptance hesabatı

**RC / UNSIGNED EVALUATION. Production acceptance tamamlanmayıb.** Aşağıdakı PASS yalnız göstərilən sınaq scope-u üçün keçərlidir.

| Bölmə | PASS | FAIL | SKIPPED |
|---|---:|---:|---:|
| Windows legacy | 72 | 0 | 0 |
| Linux legacy | 63 | 0 | 0 |
| Unified Windows | 59 | 0 | 0 |
| Unified Linux | 59 | 0 | 0 |
| Windows migration/config | 8 | 0 | 0 |
| ML training methodology | 6 | 0 | 0 |
| Native replay/parity | 10 | 0 | 0 |
| Real VM packaging/lifecycle | 35 | 0 | 0 |
| Schema | 4 | 0 | 0 |

ML runtime testləri hər OS-də 15/15-dir və həmin 59 unified testin daxilindədir; yenidən toplamaya əlavə edilməməlidir. Replay 10 hal / 370 tam risk array müqayisəsidir. Schema yoxlaması 226 envelope, 370 risk və 37 ML feature record-u əhatə edir.

Windows toplam legacy+unified+migration: **139 PASS, 0 FAIL, 0 SKIPPED**. Linux legacy+unified: **122 PASS, 0 FAIL, 0 SKIPPED**.

Qısa resurs sınaqları: Windows 180.45 s, CPU orta 0.199%, p95 0.934%, pik RAM 105.48 MiB; Linux 180.40 s, CPU orta 0.574%, p95 2.044%, pik RAM 93.68 MiB. Dropped delta hər ikisində 0; Linux iki servisdə restart delta 0. Linux son snapshot-da process IOException görünür (prosesin yox olması/oxuma icazəsi kimi qeyri-tam collector coverage); 0 error iddiası yoxdur. Qısaömürlü helper-lərin CPU-su sample-lar arasına düşə bilər.

## Komponent statusları

| Komponent | Status |
|---|---|
| Windows Agent | PARTIAL |
| Linux Agent | PARTIAL |
| Cross-platform contract | PASS |
| Packaging | PASS |
| Heuristic Risk Engine | PASS |
| Explainability | PASS |
| ML Shadow | PASS |
| Detection Evaluation | PARTIAL |
| False Positive Evaluation | PARTIAL |
| Windows Hardware | PARTIAL |
| Linux Hardware | PARTIAL |
| Replay | PASS |
| Spool | PASS |
| Migration | PASS |
| Privacy | PASS |

## Acceptance matrix

| Sahə | Status | Sübut və scope |
|---|---|
| Windows clean install | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows Service | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows restart | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows upgrade | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows failed upgrade rollback | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows uninstall | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows reinstall | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Windows agent ID persistence | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Clone identity from empty state | PASS | packaging-tests.json: Real VM lifecycle. Upgrade from alpha.3 also recorded before final partial-config fix; final bytes clean install/reinstall and failed-candidate rollback verified. |
| Linux clean install | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux systemd | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux stop | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux restart | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux upgrade | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux remove | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux purge | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux reinstall | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux agent ID persistence | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Linux watchdog 90s interval | PASS | packaging-tests.json: Real Ubuntu 24.04 VM; original data retained/restored; Splunk unchanged. |
| Windows portable EXE / ZIP | PASS | packaging-tests.json: Actual final portable ZIP extracted on Windows; standalone without hardware directory and 65-second collection. |
| Linux standalone ELF | PASS | linux-live-integration.json: Exact ELF extracted from tested DEB, benign loopback workload. |
| PID reuse | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| CPU normalized semantics | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| RAM measurement | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Hash cache | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Persistence read-only parsing | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Privacy redaction | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Spool full / risk reserve | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Partial spool write | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Corrupt spool recovery | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Crash/checkpoint recovery | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Risk confirmed | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Risk resolved | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Unacked event UID retry | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| Missing sensors null/status | PASS | windows-tests.json; linux-tests.json; unified-windows-tests.json; unified-linux-tests.json: Regression/contract tests; scope is the cases named in the reports, not all real hardware/processes. |
| IPv4 ownership | PASS | windows-tests.json; linux-live-integration.json: Actual loopback sockets mapped to owning processes on both VMs. |
| IPv6 ownership | PASS | windows-tests.json; linux-live-integration.json: Actual loopback sockets mapped to owning processes on both VMs. |
| CPU reference measurement | PASS | linux-live-integration.json: Bounded Linux CPU worker compared to /proc reference; Windows API warmup/range and formula regression separately tested. |
| Authenticode | PASS | windows-tests.json: Signed dotnet host and cache regression; not a claim that CryptoGuard itself is signed. |
| Linux package trust | PASS | linux-tests.json; linux-live-integration.json: Local package-origin provider; repository-signature assurance is not claimed. |
| Windows/Linux replay parity | PASS | parity-tests.json: 10 cases / 370 full risk arrays, 5 heuristic plus 5 shadow, native Windows EXE and packaged Linux ELF. |
| Migration old identity / alert / pending | PASS | migration-tests.json; unified-linux-tests.json; packaging-tests.json: Original files preserved on failure, confirmed/resolved states and pending event identifiers verified. |
| ML shadow cannot change heuristic | PASS | ml-evaluation.json: 15 ML runtime tests on each OS, real numeric research model replay; default no validated model. |
| ML missing features | PASS | ml-evaluation.json: 15 ML runtime tests on each OS, real numeric research model replay; default no validated model. |
| ML model hash verification | PASS | ml-evaluation.json: 15 ML runtime tests on each OS, real numeric research model replay; default no validated model. |
| ML malformed model fail-safe | PASS | ml-evaluation.json: 15 ML runtime tests on each OS, real numeric research model replay; default no validated model. |
| Normal-workload false positive fixtures | PASS | detection-evaluation.json: 14 named synthetic workload scenarios; high compute remains low security. |
| Controlled positive detection | PASS | detection-evaluation.json: Deterministic fixture plus real benign Linux CPU and loopback IOC simulation; no miner or external pool. |
| No-GPU VM | PASS | packaging-tests.json; linux-live-integration.json: Unsupported/unknown values remain null; no physical GPU assertion. |
| Headless idle | PASS | packaging-tests.json; schema-tests.json: Service/headless unknown values are allowed; does not measure interactive input time. |
| Short resource measurement | PASS | windows-resource.json; linux-resource.json: Approximately 3 minutes on each VM; sampled helpers, not 72h/reference-host certification. |
| Windows reboot | UNVERIFIED | KNOWN-LIMITATIONS.md: No remote reboot performed. |
| Linux reboot | UNVERIFIED | KNOWN-LIMITATIONS.md: No remote reboot performed on Splunk host. |
| 72h Windows | UNVERIFIED | KNOWN-LIMITATIONS.md: Soak runner smoke test only. |
| 72h Linux | UNVERIFIED | KNOWN-LIMITATIONS.md: Soak runner smoke test only. |
| Physical NVIDIA | UNVERIFIED | KNOWN-LIMITATIONS.md: No physical GPU available. |
| Physical AMD | UNVERIFIED | KNOWN-LIMITATIONS.md: No physical GPU available. |
| Physical Intel | UNVERIFIED | KNOWN-LIMITATIONS.md: No physical GPU available. |
| GPU no-driver physical matrix | UNVERIFIED | KNOWN-LIMITATIONS.md: No suitable physical hardware. |
| GPU permission-blocked physical matrix | UNVERIFIED | KNOWN-LIMITATIONS.md: No suitable physical hardware. |
| Physical temperature sensors | UNVERIFIED | KNOWN-LIMITATIONS.md: VM cannot establish accuracy on physical hardware. |
| Desktop interactive idle | UNVERIFIED | KNOWN-LIMITATIONS.md: No real interactive-session timing comparison. |
| Full normal workloads on real applications | UNVERIFIED | KNOWN-LIMITATIONS.md: Browser/video/build/update/AV/render/AI/gaming suite requires diverse real sessions. |
| Physical disk full | UNVERIFIED | KNOWN-LIMITATIONS.md: IO failure injection and spool budget covered; physical filesystem exhaustion not performed. |
| 72h reference-host CPU/RAM targets | UNVERIFIED | KNOWN-LIMITATIONS.md: Measured VM duration and hardware do not satisfy reference profile. |
| Production model acceptance | UNVERIFIED | KNOWN-LIMITATIONS.md: Own representative host-grouped dataset not yet collected. |
| Code signing | UNVERIFIED | KNOWN-LIMITATIONS.md: UNSIGNED EVALUATION; organization certificate absent. |
| Hosted GitHub Actions run | UNVERIFIED | KNOWN-LIMITATIONS.md: Workflow and local build primitives checked; no GitHub run dispatched. |
| SBOM verification | PASS | artifact-verification.json; license-inventory.json; rebuild-parity-windows.json; rebuild-parity-linux.json: Release candidate schema, checksum, package/source inventory and both same-source rebuild hashes verified; final archive verifier reruns these checks. |
| Checksum verification | PASS | artifact-verification.json; license-inventory.json; rebuild-parity-windows.json; rebuild-parity-linux.json: Release candidate schema, checksum, package/source inventory and both same-source rebuild hashes verified; final archive verifier reruns these checks. |
| Source/artifact consistency | PASS | artifact-verification.json; license-inventory.json; rebuild-parity-windows.json; rebuild-parity-linux.json: Release candidate schema, checksum, package/source inventory and both same-source rebuild hashes verified; final archive verifier reruns these checks. |
| License inventory | PASS | artifact-verification.json; license-inventory.json; rebuild-parity-windows.json; rebuild-parity-linux.json: Release candidate schema, checksum, package/source inventory and both same-source rebuild hashes verified; final archive verifier reruns these checks. |
| Phase23 production backend | NOT APPLICABLE | docs/PHASE23-HANDOFF.md: NOT IMPLEMENTED BY DESIGN |
| RandomX native detector | NOT APPLICABLE | docs/RESEARCH-SOURCES.md: Experimental reference only; disabled/not included. |

Yekun matrix: PASS=55, FAIL=0, UNVERIFIED=17, NOT APPLICABLE=2.

## Açıq gates

72h Windows/Linux, real reboot Windows/Linux, physical NVIDIA/AMD/Intel və temperatur, real desktop idle, geniş real normal-workload dataset, physical disk exhaustion, reference-host resurs norması, model promotion, code signing və hosted CI run açıqdır. Soak/reboot runner-ları var; 72h və reboot keçirilməyib. Phase23 production backend NOT IMPLEMENTED BY DESIGN.

## Fayllar və əvvəlki xətalar

`BUG-STATUS.md` düzəldilən hər əvvəlki problemi və sübutunu, `CHANGED-FILES.md` əsas dəyişiklikləri, `RESEARCH-SOURCES.md` source origin-i göstərir. Sample events synthetikdir; canlı host process export-ları public release-ə daxil edilmir. Binaries-in imzasız olduğu real Authenticode yoxlaması ilə təsdiqlənib. Release testləri üzrə fail gizlədilməyib.
