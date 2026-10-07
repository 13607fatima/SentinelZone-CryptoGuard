# Əsas dəyişən fayllar

Orijinal Linux 0.22.1 və Windows alpha.3 workspace-ləri dəyişdirilməyib. Bu source onları birləşdirən mövcud `SentinelZone.CryptoGuard` ağacıdır; yeni boş məhsul yazılmayıb. `docs/history/` baseline fayl hash-ləri saxlayır.

| Fayl / qovluq | Dəyişiklik |
|---|---|
| src/CryptoGuard.Contracts/Models.cs, ReleaseVersions.cs | Ortaq envelope, risk/resource, trust, health, nullable sensor və ML shadow contract |
| src/CryptoGuard.Contracts/LegacyWindows/ | Əvvəlki Windows wire formatı migration və regression üçün saxlanıb |
| src/CryptoGuard.Core/DurableSpool.cs, LocalCommands.cs, CapabilitySummary.cs, SummaryWindow.cs | Ortaq spool/export/status/doctor; UID collision, priority recovery, elapsed-weighted summary |
| src/CryptoGuard.Risk/RuleEngine.cs, ReplayRunner.cs | Ortaq explainable risk, lifecycle continuity/recovery və platformdan asılı olmayan replay |
| src/CryptoGuard.Platform.Windows/Normalization.cs, ConfigurationLoader.cs | Windows collector-larını ortaq contract-a çevirir; partial config defaults qorunur |
| src/CryptoGuard.Agent.Windows/Program.cs, Runner.cs, StateMigration.cs | Shared engine/spool, service/CLI, köhnə identity/alert/pending migration və helper yolu |
| src/CryptoGuard.Platform.Windows/ | Mövcud CPU/process/network/persistence/trust/idle kollektorları saxlanıb və adapterlə bağlanıb |
| src/CryptoGuard.HardwareHost.Windows/Program.cs | Pinned LHM 0.9.6, ayrıca helper, memory/GPU və opt-in CPU sensorları |
| src/CryptoGuard.Platform.Linux/ | /proc ölçmələri, labelled hwmon/NVML, package-origin metadata və cron.* müşahidəsi |
| src/CryptoGuard.Agent.Linux/Program.cs | Yeni contract/status/doctor/export/replay, əvvəlki systemd/watchdog/outbox mexanizmi saxlanıb |
| src/CryptoGuard.ML/Features.cs, ShadowForest.cs | 22 feature, null availability, SHA256 ilə yoxlanan bounded numeric forest və fail-safe shadow |
| research/training/ | Dataset collector, host-group train/test/CV, train-only imputation, RF export və metodologiya testləri |
| contracts/, schemas/, rules/ | Ortaq telemetry/risk/ML schemas, 5 canonical replay və sample telemetry/risk/health |
| tests/Contract, tests/WindowsMigration, tests/Packaging | Əlavə 59 shared test, 8 migration/config testi və real VM lifecycle runner-ları |
| packaging/windows/, debian/ | Installer backup/rollback, self-contained publish, signing input, preserved Debian packaging |
| scripts/ | Soak/reboot runner-ları, schema/parity yoxlaması, source/release arxivləşdirmə və artifact verifier |
| .github/workflows/ci.yml, Directory.Build.props, packages*.lock.json | Windows/Linux jobs, parity gate, tag-only release, pinned actions/dependencies və ayrı restore profilləri |
| docs/, licenses/ | Azərbaycan dilində handoff/installation/risk/ML/privacy/limitations, upstream attribution və corresponding sources |

Arxivdəki tam fayl siyahısı və SHA256-lar `source/source-tree-manifest.json` daxilindədir. DLL/EXE/ELF-in təkrar build hash-ləri `validation/rebuild-parity-*.json` ilə yoxlanır.
