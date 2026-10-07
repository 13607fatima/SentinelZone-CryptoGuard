# Ubuntu x64 quraşdırma

`sudo apt install ./cryptoguard-agent_0.22.2-rc.1_amd64.deb`

Artifact fayl adında release version işlənir, Debian daxili version `0.22.2~rc.1`-dir; `~` stable 0.22.2-dən əvvəl sıralanması üçündür. Native ELF runtime .NET quraşdırılmasını tələb etmir. Binary `/usr/lib/cryptoguard/cryptoguard-agent`, symlink `/usr/bin/cryptoguard-agent`, config `/etc/cryptoguard/agent.json`, state `/var/lib/cryptoguard`, unitlər `/usr/lib/systemd/system/`, sənədlər `/usr/share/doc/cryptoguard-agent/`.

`sudo systemctl status cryptoguard-agent cryptoguard-collector`; `cryptoguard-agent doctor`; `sudo cryptoguard-agent status`; `sudo cryptoguard-agent export --output /tmp/NEW.jsonl`. Portable: `./cryptoguard-agent run --state /tmp/NEW-STATE --samples 12`; replay: `cryptoguard-agent replay contracts/replay/mining-like.jsonl`.

Core `cryptoguard` istifadəçisidir. Root collector read-only OS məlumatı oxuyur, yalnız Unix socket ilə əlaqə saxlayır. NoNewPrivileges, filesystem hardening, capabilities və system-call məhdudiyyətləri mövcuddur. Idle logind-in lokal graphical session hint-indən gəlir; headless halda null/status normaldır.

Upgrade state və ID-ni saxlayır. `sudo apt remove cryptoguard-agent` state-i saxlayır; `sudo apt purge cryptoguard-agent` paketə aid `/var/lib/cryptoguard` state-ni qəsdən silir. Purge-dan əvvəl lazım olan export/backup alın. Yeni clone boş state ilə yaradılmalıdır.

GPU: NVML driver-dən dinamik yüklənir, NVIDIA library paketə daxil deyil. hwmon label/source saxlanır; generic thermal zone CPU kimi tanınmır. DRM vendor/hardware yoxlamaları acceptance matrix-də göstərilir. Əlçatmaz provider null/status verir.
