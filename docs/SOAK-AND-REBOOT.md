# Açıq uzunmüddətli testlər

Bu release-də real 72 saat və reboot nəticəsi yoxdur. Sınaq runner-ları qısa smoke-run ilə yoxlanılır; həmin nəticə 72h acceptance deyil. Reference host: ən az 8 logical CPU, 16 GiB RAM, representative proses sayı. Target average CPU ≤1%, p95 ≤3%, RAM ≤250 MiB-dir; VM qısa nəticələri bu reference gate-ni bağlamır.

Windows-da administrator terminalında işləyən servisi ölçün:

```powershell
powershell -NoProfile -File scripts/soak-windows.ps1 -Hours 72 -OutputDirectory C:\CryptoGuardTests\soak72h
```

Ubuntu-da ayrıca test hostunda native agenti ölçün:

```sh
python3 scripts/soak.py /usr/bin/cryptoguard-agent /tmp/cryptoguard-soak72h --hours 72
```

Hər ikisi `samples.jsonl` və `report.json` yaradır. CPU faktiki elapsed vaxta və logical CPU sayına bölünür; RAM, spool bytes, dropped events, collector errors, memory growth və crash/restart vəziyyəti qeyd olunur. Qısaömürlü helper CPU-sunun sample-lar arasına düşməsi ölçmənin məlum məhdudiyyətidir. Report `MEASURED_REVIEW_REQUIRED` olsa belə, statistikaları və host profilini ayrıca qiymətləndirin.

Reboot yoxlaması özü maşını restart etmir. Əvvəl:

```sh
python3 scripts/reboot-check.py before --state /var/lib/cryptoguard --evidence /tmp/cryptoguard-before-reboot.json
```

Yalnız host sahibi uyğun vaxtda reboot etdikdən sonra `after` rejimi ilə eyni record-u verin. Windows state yolu `C:\ProgramData\SentinelZone\CryptoGuard`-dır; script-in `--help` çıxışı parametrləri göstərir. agent_id, boot_id, sequence, pending event UID-lər və service status yoxlanır. Test olunmayan physical NVIDIA/AMD/Intel, desktop idle və driver/permission matrix bəndləri UNVERIFIED qalır.
