# Phase 23 handoff — inteqrasiya implement edilməyib

Sabit sərhəd `CryptoGuard.Contracts.IEventSink.AcceptAsync(TelemetryEvent,CancellationToken) -> ValueTask<bool>`-dur. Sonrakı mərhələdə HttpsEventSink əlavə edilə bilər. İndi enrollment, token registration, production backend, remote ingestion, Splunk HEC və dashboard yoxdur.

Event növləri telemetry, inventory, risk, health. Schema `contracts/telemetry.schema.json`; risk və ML ayrıca schema ilə izah olunur. `contracts/sample-events` və canonical replay fixtures examples-dir. Health collector errors/latency, spool bytes, dropped events, sensor availability daşıyır. Risk security_risk/resource_impact, ruleset/model, explainability, lifecycle və ml shadow daşıyır.

agent_id ilk run-da generated GUID-dir; upgrade/reinstall zamanı saxlanır, clone üçün boş state yeni ID yaradır. event_uid bir immutable event-in GUID-idir; retry eyni UID və payload saxlamalıdır. sequence diskdə əvvəlcədən rezervasiya olunur, agent üzrə monoton artır; crash boşluq yarada bilər, reuse etməməlidir. observed_at UTC sample observation vaxtıdır; sample_duration_ms faktiki intervaldır. Backend receive time bunun yerinə keçmir.

Sink yalnız server hadisəni davamlı qəbul etdikdən sonra true qaytarmalıdır. Backend `(agent_id,event_uid)` ilə idempotency saxlamalıdır. ACK olmadan normal uğurlu delivery nəticəsində event silinmir. False/cancel/error pending qalır; retry exponential bounded jitter istifadə edə bilər. LocalFakeReceiver bu sərhədi test edir.

İstisna kimi quota/retention siyasəti adi köhnə telemetry-ni açıq dropped_events sayğacı ilə itirə bilər. Risk reserve prioritet hadisələr üçündür; atomic checkpoint-də pending risk varsa spool yer açılana qədər detection backpressure tətbiq edir. Partial write checksum/recovery ilə idarə olunur. Retained legacy migration JSON-ları avtomatik silinmir.

Backend unavailable olduqda agent collection/risk/local spool/export ilə işləyir. Şəbəkə endpoint-i kodda hardcode deyil. Phase 23 token/transport əlavə edəndə redaction, TLS, bounded retry, explicit durable ACK və event dedup acceptance testləri ayrıca tələb olunur. Risk balı compromise probability kimi təqdim edilməməlidir.
