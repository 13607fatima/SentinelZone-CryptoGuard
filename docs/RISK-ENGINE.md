# Risk mühərriki

High CPU/GPU/temperature təkbaşına malware nəticəsi deyil. Compute 30 saniyə davam edəndə 15 risk balı əlavə edə bilər; severity aşağı qalır. Resource impact ayrıca CPU, aid edilmiş proses GPU və RSS/host RAM nisbətinin maksimumudur.

Sübut qrupları: SUSTAINED_COMPUTE, MINING_ARGUMENTS, MINING_NETWORK_EVIDENCE, SUSPICIOUS_PATH, PERSISTENCE_PRESENT, BEHAVIOR_ANOMALY, UNTRUSTED_EXECUTABLE, KNOWN_MALICIOUS_HASH. Unsigned/trust sübutu yalnız dəstəkləyici kontekstdir və ayrıca yüksək bal yaratmır. Tək port IoC sayılmır. IP/hash IOC üçün etibarlı mənbə, confidence >=0.9 və gələcək expiry tələb olunur.

Əsas çəkilər: compute 15, mining arqument kombinasiyası 25, etibarlı process-IP IOC 30, mining sübutu ilə writable path 10, həmin executable persistence 10, mining sübutu ilə bounded CPU anomaly 10, məlum malicious hash 95. High üçün mining-specific sübut və >=50 bal, ən azı iki qrup tələb olunur; hash ayrıca qaydadır. Critical >=75 high və ya malicious hash-dir. Açıq hash+path workload icazəsi malicious hash-i bağışlamır.

Lifecycle observed → suspected → confirmed → resolved. Normal halda high sübut 60 saniyə davam etməlidir. Məlum malicious hash ayrıca birbaşa təsdiqlənə bilər. Fasilə davamlılığı sıfırlayır; bir CPU spike confirmed etmir. CPU unknown zamanı əvvəlki confirmed alert təmiz sayılmır. Process-in yoxluğu yalnız kifayət qədər inventory coverage və davamlı müşahidə ilə resolve edilir; reboot başqa boot ID ilə əvvəlki prosesin bitməsini bildirir.

Risk checkpoint post-evaluation state və pending risk hadisəsini atomik saxlayır. Pending hadisə spool-a davamlı yazılmadan növbəti detection addımı başlamır. Restart confirmed/resolved alert ID-lərini saxlayır. Resolved tombstone-lar retention və say limiti ilə məhduddur.

Heç bir kill, remote action və persistence disable funksiyası yoxdur. RandomX core dependency deyil.
