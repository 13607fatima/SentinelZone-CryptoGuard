# Testing and evidence

Use .NET SDK 10.0.401 as pinned in `global.json`. The C# tests are executable harnesses. Their reported results, platform and artifact hashes determine scope; an unavailable tool is UNVERIFIED, not PASS. Keep generated reports in ignored `validation/` or `test-results/`. Immutable original reports live under [release-evidence](release-evidence/v0.22.2-rc.1/README.md).

## Linux legacy and unified

```bash
dotnet restore tests/CryptoGuard.Tests --locked-mode --configfile NuGet.Config
dotnet run --project tests/CryptoGuard.Tests -c Release --no-restore -- test-results
dotnet restore tests/Contract --locked-mode --configfile NuGet.Config
dotnet run --project tests/Contract -c Release --no-restore -- --report validation/unified-linux-tests.json --fixtures contracts/replay
```

## Windows legacy, unified and migration (PowerShell)

```powershell
dotnet restore SentinelZone.CryptoGuard.Windows.slnx --locked-mode --configfile NuGet.Config
dotnet build SentinelZone.CryptoGuard.Windows.slnx -c Release --no-restore
dotnet tests/Windows/bin/Release/net10.0-windows/CryptoGuard.Tests.dll artifacts/test-state
dotnet tests/Contract/bin/Release/net10.0/CryptoGuard.Unified.Tests.dll --report validation/unified-windows-tests.json --fixtures contracts/replay
dotnet restore tests/WindowsMigration --locked-mode --configfile NuGet.Config
dotnet run --project tests/WindowsMigration -c Release --no-restore -- validation/migration-windows-tests.json
```

Use the signed Microsoft dotnet host for the Windows legacy harness: an Authenticode regression inspects its host, so an unsigned apphost does not satisfy that test premise. This does not mean CryptoGuard itself is signed. `Publish.ps1` runs these suites before packaging.

## Schema validation

Install `jsonschema` in your Python environment (the Ubuntu workflow uses `python3-jsonschema`). From the repository root, Bash:

```bash
for fixture in contracts/replay/*.jsonl; do
  python scripts/validate_contract.py schemas/telemetry.schema.json "$fixture"
done
```

PowerShell equivalent:

```powershell
Get-ChildItem contracts/replay/*.jsonl | ForEach-Object {
  python scripts/validate_contract.py schemas/telemetry.schema.json $_.FullName
  if ($LASTEXITCODE) { throw 'Schema validation failed' }
}
```

Use Draft 2020-12 with format checking for complete sample-event JSON documents. `risk.schema.json` validates individual risk objects (unwrap native replay's arrays). `ml-features.schema.json` validates feature records; do not pass the feature definition document as though it were a record. Preserve schema differences and contracts; do not simplify them for a test.

## Native replay parity

On Windows, produce one UTF-8 JSONL output per fixture with the built EXE:

```powershell
New-Item -ItemType Directory -Force artifacts/parity-windows | Out-Null
foreach ($f in Get-ChildItem contracts/replay/*.jsonl) {
  $lines = & artifacts/win-x64/CryptoGuardAgent.exe replay --input $f.FullName --data "$env:TEMP/CryptoGuard-Replay"
  if ($LASTEXITCODE) { throw 'Replay failed' }
  [IO.File]::WriteAllLines((Join-Path "$pwd/artifacts/parity-windows" $f.Name), [string[]]$lines)
}
```

On Linux:

```bash
mkdir -p artifacts/parity-linux
for fixture in contracts/replay/*.jsonl; do
  artifacts/linux-x64/cryptoguard-agent replay "$fixture" > "artifacts/parity-linux/$(basename "$fixture")"
done
```

Bring both result directories to a comparison host, then:

```bash
python scripts/compare-parity.py artifacts/parity-windows artifacts/parity-linux --output validation/parity-tests.json
```

Use identical inputs/policy. Optional shadow parity additionally needs the same reviewed research model/hash and OS-specific ML config, writing to separate directories. The original evidence has 5 heuristic + 5 shadow cases. Single-platform repeated replay is determinism, **not native cross-platform parity**.

## ML methodology

Bash:

```bash
python -m venv .training-venv
.training-venv/bin/python -m pip install -r research/training/requirements.txt
.training-venv/bin/python scripts/run-training-tests.py --output validation/training-tests.json
```

On Windows substitute `.training-venv/Scripts/python.exe`. Record installed versions; results with different dependencies must be disclosed, not described as a pinned reproduction. Six tests cover host/session split, train-only imputation, metrics, model export and input rejection. They use synthetic data, not production accuracy evidence.

## Packaging, lifecycle and resources

`tests/Packaging/` contains current Windows/Linux clean-install and lifecycle harnesses. Review their arguments/code and use disposable test hosts with reviewed state backups. They can install/uninstall/purge software and deliberately exercise recovery. They are not safe general-purpose checks on a shared production host.

`scripts/soak.py`, `scripts/soak-windows.ps1` and `scripts/reboot-check.py` support duration/resource/reboot evidence. Approximately 20-second runner smoke tests and three-minute resource measurements in the original bundle do not satisfy 72-hour gates. Physical disk exhaustion, sensors, desktop idle and broad real workloads need separate environments. `tests/CryptoGuard.SpoolBenchmark` is a resource benchmark outside the principal solutions; do not count it as executed merely because sources exist.

## Publication checks

```bash
python scripts/check-repository.py
python -m unittest discover -s tests/Repository -v
python scripts/secret-scan.py
git add .
python scripts/secret-scan.py --staged
```

These static checks do not build packages. The Debian regression catches the verified case-sensitive `docs/ACCEPTANCE.md` input defect. See [PUBLICATION-CHECKS.md](PUBLICATION-CHECKS.md) for scanner scope and synthetic exceptions.

Original Windows 139, Linux 122, native parity 10 and methodology 6 PASS counts remain in their historical report. New checks belong in a separate report and must not silently change that acceptance matrix.
