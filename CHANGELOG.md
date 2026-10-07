# Standalone repository preparation — v0.22.2-rc.1

## Repository packaging correction — rc.2 handoff

- Replaced the rendered Mono.Posix.NETStandard GitHub HTML page with the complete upstream plain-text notice and preserved its attribution in `THIRD-PARTY-NOTICES.txt` and Debian copyright metadata.
- Restricted Debian license installation to reviewed plain-text notices, metadata and source archives; the Debian preflight now rejects rendered license pages before package build.
- Updated Inno Setup 6.7.3 preparation to use the official GitHub release asset while retaining the pinned checksum and Authenticode verification.

Release status remains **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**. The application and telemetry/risk contracts are unchanged.

Release status remains **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**. No application behavior, telemetry/risk contract, existing test, migration or architecture was changed. Original measured acceptance remains 55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE.

- Flattened the 245-file source baseline into the standalone `SentinelZone-CryptoGuard/` repository root.
- Replaced the minimal README with professional English implementation/build/acceptance documentation; added security, contribution, license-decision, privacy, push, release and exact asset guidance plus issue/PR templates.
- Preserved original release evidence, SBOM, manifests, dependency locks, all third-party materials and source notices. Product binaries remain release assets outside Git.
- Expanded ignore rules and added source/index publication screening with narrow, documented synthetic fixture exceptions.
- **Verified packaging defect:** `debian/cryptoguard-agent.docs` referenced lowercase `docs/acceptance.md`, absent on a case-sensitive checkout. Corrected to existing `docs/ACCEPTANCE.md`. The retained original source ZIP and off-tree preparation backup preserve the baseline. The new regression fails against the original and passes against the correction. Full Debian rebuild remains UNVERIFIED in the preparation environment because .NET SDK/clang/debhelper are unavailable.
- Made Windows migration-test restore explicitly locked, then used `--no-restore` for execution. No tests removed or weakened. Full PowerShell execution remains UNVERIFIED here.
- Added repository checks and explicit Linux solution locked restore/build to existing pinned cross-platform CI. Hosted Actions remains UNVERIFIED.

See [RELEASE_MANIFEST.md](RELEASE_MANIFEST.md) for actual new tests, byte-preservation checks, changed-file inventory and remaining gates. The original release history follows unchanged.

---

# 0.22.2-rc.1

- Mövcud Linux 0.22.1 və Windows alpha.3 source-ları qorunaraq shared contract/risk/core birləşdirildi.
- security_risk/resource_impact, sensor status, platform trust metadata, reason codes, ML shadow və canonical replay əlavə edildi.
- Windows identity/sequence/alert/pending-event migration və installer state rollback backup əlavə edildi; original spool saxlanır.
- Missing CPU və uzun sampling gap səhv resolve yaratmır; resolved alert ID checkpoint-də qalır. Spool UID-payload collision yoxlanır; deferred priority event drop sayılmır.
- Windows və Linux weighted summary, network actual elapsed semantics uyğunlaşdırıldı. Linux cron.* inventory və executable metadata genişləndi.
- Feature catalog, privacy-preserving DatasetCollector, host-grouped Random Forest training və bounded hash-verified JSON inference əlavə edildi.
- Köhnə 63 Linux və 72 Windows regression test saxlanıb; ortaq və migration testləri əlavə edilib.
- Phase 23 real backend əlavə edilməyib.
