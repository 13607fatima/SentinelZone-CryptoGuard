# Standalone repository preparation — v0.22.2-rc.1

Release status remains **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**. No application behavior, telemetry/risk contract, existing test, migration or architecture was changed. Original measured acceptance remains 55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE.

- Flattened the 245-file source baseline into the standalone `SentinelZone-CryptoGuard/` repository root.
- Replaced the minimal README with professional English implementation/build/acceptance documentation; added security, contribution, license-decision, privacy, push, release and exact asset guidance plus issue/PR templates.
- Preserved original release evidence, SBOM, manifests, dependency locks, all third-party materials and source notices. Product binaries remain release assets outside Git.
- Expanded ignore rules and added source/index publication screening with narrow, documented synthetic fixture exceptions.
- **Verified packaging defect:** `debian/cryptoguard-agent.docs` referenced lowercase `docs/acceptance.md`, absent on a case-sensitive checkout. Corrected to existing `docs/ACCEPTANCE.md`. The retained original source ZIP and off-tree preparation backup preserve the baseline. The new regression fails against the original and passes against the correction. Full Debian rebuild remains UNVERIFIED in the preparation environment because clang/debhelper are unavailable and system package installation is blocked in this container.
- **Verified locked-restore defect:** Linux agent portable lock omitted the existing shared ML project and current risk/core dependency edges. The exact pinned SDK produced NU1004. Regenerated only that portable lock, verified all external package entries stayed identical, and reran locked restore/build plus the 63 legacy and 59 unified Linux tests. Added a project-graph regression; application C# and schemas are unchanged.
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
