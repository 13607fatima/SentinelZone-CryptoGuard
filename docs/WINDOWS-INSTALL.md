# Windows x64 quraşdırma

Status: UNSIGNED EVALUATION / RC. Setup Administrator ilə işləyir. Binary-lər `%ProgramFiles%\SentinelZone\CryptoGuard\versions\0.22.2-rc.1`, mutable state `%ProgramData%\SentinelZone\CryptoGuard`, config `config.json` içindədir. Əvvəlki `agent.json` silinmədən bir dəfə köçürülür.

`SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe` service-i `SentinelZoneCryptoGuard`, LocalService, automatic delayed startup və recovery policy ilə qurur. `CryptoGuardAgent.exe doctor --data C:\CryptoGuardTest --hardware` diaqnostika verir. Portable ZIP-i bütöv çıxarın. Standalone EXE core üçün self-contained-dir; LHM helper və DLL-lər üçün portable/installer-dəki `hardware` qovluğu lazımdır. Helper olmayanda hardware unavailable olur, core işləyir.

Əmrlər: `version`, `doctor`, `run --data DIR --duration 60 --capture`, `status --data DIR`, `export --data DIR --out NEW.jsonl`, `replay --input FILE.jsonl --config FILE`. Administrator olmayan portable run üçün yazıla bilən `--data` seçin. Binary qovluğuna state yazılmır.

GPU hardware provider `--hardware` və ya config EnableHardware ilə açılır. CPU aşağı səviyyəli LHM sensorları ayrıca `enable_cpu_hardware=true` tələb edir; uyğun imzalı driver/permission olmadıqda null/status normaldır. Agent driver quraşdırmır və fan/voltage/clock control istifadə etmir. Session helper logon Run entry-dən açılır, Session 0 GetLastInputInfo nəticəsi interactive idle kimi göstərilmir.

Upgrade əvvəlki service-i dayandırır, yeni versiya qovluğuna stage edir, state-in ACL qorunan backup-ını saxlayır, service path-i dəyişir və fresh health/version/RAM yoxlayır. Uğursuz halda əvvəlki service path/config/state qaytarılır; uğursuz sınağın state-i də ayrıca saxlanır. Backup `%ProgramData%\SentinelZone\CryptoGuard-rollback` içindədir; disk sərfi ayrıca nəzərə alınmalıdır. Köhnə spool JSON-ları avtomatik silinmir və quota-da sayılır.

Uninstall service və agentin idle helper-lərini dayandırır, Run entry-ni silir; ID/config/spool saxlanır. Data-nı silmək ayrıca istifadəçi qərarıdır. Clone/golden image üçün agenti dayandırıb state qovluğunu yeni endpoint-ə köçürməyin; yeni boş state ilk run-da yeni ID yaradır.
