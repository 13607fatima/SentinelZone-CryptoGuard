# GitHub research traceability

| Layihə | Pin / məqsəd | Daxil edilən / edilməyən |
|---|---|---|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | NuGet LibreHardwareMonitorLib 0.9.6; araşdırılmış HEAD `65a16ef5a9481d17383d1d070140870a13d4b825` ayrıca research pin-dir | Computer/IHardware/ISensor və CPU/GPU modulları pinned dependency adapterindən istifadə olunur; source kor-koranə copy edilməyib. MPL-2.0 notices və 0.9.6 corresponding source archive saxlanıb. |
| [Chayesh/cryptoguard](https://github.com/Chayesh/cryptoguard/tree/9e08ca73f2a9abf08d5fc3700f3fefce8552bf3d) | MIT, 2026 Chayesh; dataset/RF/evaluation methodology reference | collect_data_linux.py, train_model.py, retrain_fix2.py, stress_test.py ideyaları müstəqil tətbiq edilib. Hazır pickle/scaler/dataset copy edilməyib. |
| [PubuduTerance/CryptoJackGuard](https://github.com/PubuduTerance/CryptoJackGuard/tree/ef69126c20cdfe2ecfb3e4a70dfeb3c3df2a8a50) | Reference only; yoxlanılan tree-də aydın LICENSE yoxdur | Process/network correlation, bounded history, privacy, reason scoring kimi davranış ideyaları. Source, tests, main.py, dashboard, kill/terminate, pkl və synthetic dataset məhsula copy edilməyib. |
| [tevador/randomx-sniffer](https://github.com/tevador/randomx-sniffer/tree/8402301b2a87c31647a28cff9c4b9ed25d9e15e2) | CC0 reference | Core dependency deyil, kod daxil edilməyib, default disabled. Thread-context sampling və confirmed cryptojacking verdict yoxdur. |

Chayesh README 22 feature iddia edir; yoxlanılan collector-da 21 numerical candidate var. SentinelZone öz **22-feature versioned contract**-ını istifadə edir. Upstream model uyğunluğu iddia edilmir. `CHAYESH-FEATURE-AUDIT.md` hər original feature-i göstərir.

LHM adapter Computer.Open/Update/Close və sensor ağacını oxuyur; yazı/control API-si yoxdur. PawnIO modul materialları LHM transitive komponentidir; driver ayrıca user installation olmadan agent tərəfindən quraşdırılmır. PawnIO LGPL materialları və LHM notices corresponding-source arxivində saxlanır. Microsoft.Windows.CsWin32 build dependency license-i NuGet inventory-dən göstərilir. Faktiki paket/version/license `sbom.spdx.json` və bundled NuGet metadata-da yoxlanılır.

MPL/LGPL asılılıqları üçün müvafiq notices/source hüquqları saxlanmalıdır; layihə sahibinin öz məhsul lisenziyası ayrıca qərardır. Bu sənəd uncertain-license CryptoJackGuard kodundan istifadə icazəsi olduğunu iddia etmir.

[PawnIO.Modules 0.1.6](https://github.com/namazso/PawnIO.Modules/tree/0.1.6) LHM 0.9.6 resurs README-sində göstərilən modul versiyasıdır. Mənbə header-lərində `LGPL-2.1-or-later` yazılır. Həmin tag-in source arxivi, COPYING və arxiv SHA256/git object məlumatı `licenses/` altında ayrıca saxlanır. Bu materiallar hardware driver-in agent tərəfindən quraşdırılması demək deyil.
