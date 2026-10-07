# Əvvəlki xətalar və son düzəlişlər

| Xəta / risk | Nəticə | Sübut |
|---|---|---|
| Linux uzun sampling intervalında watchdog restartı | FIXED; 90 saniyəlik interval real servisdə keçdi | packaging-tests.json |
| Cron command daxilində `key=value` və quoted arqumentlərin itməsi | FIXED; Linux 0.22.1 düzəlişi saxlanıb | linux-tests.json, cron regression-ları |
| Full spool zamanı confirmed/resolved keçidinin itməsi | FIXED; atomic outbox/checkpoint, restart və retention retry saxlanıb | linux-tests.json; unified-*-tests.json |
| Spool byte hesablaması və crash recovery | FIXED; index/ACK/partial-write regression-ları keçdi | linux-tests.json |
| Windows service path quoting və installer iş qovluğu | FIXED; LocalService və quoted ImagePath real yoxlanıb | packaging-tests.json |
| Windows upgrade identity və state itkisi | FIXED; backup, migration, stage/health və rollback | migration-tests.json; packaging-tests.json |
| Windows single-file helper üçün assembly location istifadəsi | FIXED; executable/base directory sərhədi istifadə olunur | final portable ZIP, standalone və service testləri |
| Windows yalnız ML sahələri olan config standartları sıfırlayırdı | FIXED; defaults üzərinə explicit JSON overlay, unknown property rejection | migration-tests.json, partial config testləri |
| Windows alpha.3 pending alert-in yeni risk array-ə daşınmaması | FIXED; alert ID, lifecycle və event UID saxlanır | confirmed/resolved/pending migration testləri |
| Windows/Linux fərqli risk və ölçmə mənaları | FIXED; ortaq engine, elapsed CPU/network hesablaması | parity-tests.json; 370 native nəticə cütü |
| PID reuse əvvəlki state-i daşıya bilərdi | FIXED; instance identity və boot sərhədi | hər iki legacy suite və unified PID reuse testi |
| Unknown sensor üçün sıfır və ya yanlış CPU temperatur etiketi | FIXED; nullable metric/status, hwmon source/label | contract tests; fiziki sensor dəqiqliyi UNVERIFIED |
| Windows single-file restore adi build lock faylını dəyişirdi | FIXED; ayrı portable/singlefile/hardware lock profilləri | locked-build-windows.json; rebuild-parity-windows.json |
| Debian strip səbəbindən standalone və packaged ELF byte-ları fərqli idi | FIXED release assembly; standalone/tar `.deb`-dəki ELF-dən hazırlanır | artifact-verification.json; rebuild-parity-linux.json |
| PowerShell UTF-8 BOM schema testində qəbul edilmirdi | FIXED verifier; UTF-8 BOM qəbul edilir | schema-tests.json |
| SPDX package checksum tək obyekt kimi serializasiya olunurdu | FIXED; array normalizasiyası və SPDX schema validation | artifact-verification.json |

Uğursuz ilkin build/test cəhdləri düzəliş üçün istifadə edilib; yekun saylar düzəlişlərdən sonrakı icralardır. Fiziki disk exhaustion, 72h, reboot, desktop idle və real GPU testləri FIXED/PASS kimi göstərilmir; acceptance matrix-də ayrıca açıqdır.
