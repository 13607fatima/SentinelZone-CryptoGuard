> TARİXİ BASELINE SƏNƏDİ: Bu fayl Linux 0.22.1-dən saxlanıb. 0.22.2-rc.1 üçün README.md, docs/TELEMETRY-CONTRACT.md, docs/ACCEPTANCE.md və release validation/ nəticələri əsasdır.

# CryptoGuard 0.22.1 — düzəliş buraxılışı

Risk keçidi və lifecycle vəziyyəti risk-checkpoint.json faylında atomik saxlanır.
Yazılmamış risk hadisəsi eyni event_uid ilə təkrar yazılır. Növbə dolanda
bir pending event saxlanılır və toplanma gözləyir; retention və watchdog davam edir.
Köhnə risk-state.json ilk miqrasiya zamanı oxunur.

Cron parser-i artıq əmrdəki key=value argumentlərini ötürmür. Qəsdən uzun
ölçmə gözləməsində watchdog heartbeat göndərilir. Spool ölçüsü və expiration
indekslə izlənir; hər append bütün backlog-u oxumur.

Ubuntu 24.04 x64 serverində 63/63 regressiya və 37/37 sintetik schema yoxlaması
keçib. Son binary, quraşdırılmış servis və qısa resurs sınaqlarının ayrıca
nəticələri release/validation/ altındadır. Əvvəlki 0.22.0 testləri bu
buraxılışa aid nəticə kimi istifadə edilməməlidir.

Risk siyasəti dəyişməyib; qaydalar 0.22.0-rules.1 olaraq qalır. Backend
transportu yoxdur. Windows agenti, 72 saatlıq sınaq, real reboot, fiziki GPU,
aşkarlama dataset-i və digər Ubuntu versiyalarında yoxlama tamamlanmış sayılmır.
Paket imzalanmamış lokal namizəddir; geniş istifadə üçün qəbul edilməyib.

Remove state-i saxlayır. Purge agent ID-si, telemetry və export daxil olmaqla
/var/lib/cryptoguard qovluğunu silir. Köhnə versiyaya downgrade üçün uyğun
state backup lazımdır; 0.22.0 yeni checkpoint formatını oxumur.
