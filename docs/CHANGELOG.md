# 0.22.2-rc.1

- Mövcud Linux 0.22.1 və Windows alpha.3 source-ları qorunaraq shared contract/risk/core birləşdirildi.
- security_risk/resource_impact, sensor status, platform trust metadata, reason codes, ML shadow və canonical replay əlavə edildi.
- Windows identity/sequence/alert/pending-event migration və installer state rollback backup əlavə edildi; original spool saxlanır.
- Missing CPU və uzun sampling gap səhv resolve yaratmır; resolved alert ID checkpoint-də qalır. Spool UID-payload collision yoxlanır; deferred priority event drop sayılmır.
- Windows və Linux weighted summary, network actual elapsed semantics uyğunlaşdırıldı. Linux cron.* inventory və executable metadata genişləndi.
- Feature catalog, privacy-preserving DatasetCollector, host-grouped Random Forest training və bounded hash-verified JSON inference əlavə edildi.
- Köhnə 63 Linux və 72 Windows regression test saxlanıb; ortaq və migration testləri əlavə edilib.
- Phase 23 real backend əlavə edilməyib.
