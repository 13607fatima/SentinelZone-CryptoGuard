# Privacy

Command-line qiymətləri secret siyahısına güvənərək maskalanmır; ümumiyyətlə saxlanmır. Linux safe flag adı + redacted placeholder, Windows yalnız mining semantic flags qaytarır. Password, token, Authorization/Bearer, JWT, URL credentials, connection-string password, API/session secret raw formada çıxmamalıdır. Testlər disk spool və JSONL-dən də yoxlayır.

Health faylı proseslər, arqumentlər, connection/persistence inventarı və summary saxlamır. Log-larda exception message əvəzinə tipi/status yazılır. Dataset yalnız rəqəmsal feature-lər, availability və pseudonymous metadata saxlayır.

Executable paths, PID, hashes, network endpoint və persistence locations telemetry-də təhlükəsizlik sübutudur; bunlar həssas inventar ola bilər. Linux hostname default redacted-dir; Windows native hostname mövcud adapterdən gəlir. Offline export-u yalnız etibar edilən şəxslərlə paylaşın. Spool/local config ACL-ləri service user/admin-lə məhduddur. Remote transport yoxdur.

Source/release paketlərinə canlı endpoint export-u, şəxsi agent ID/state, SSH/private key və token daxil edilmir. Validation summary-ləri və sintetik sample events paylanır.
