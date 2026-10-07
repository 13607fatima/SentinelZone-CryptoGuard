# Contributing

CryptoGuard is a standalone component repository. Start with [README.md](README.md), [ARCHITECTURE.md](ARCHITECTURE.md) and [docs/TESTING.md](docs/TESTING.md). The project license is undecided; see [LICENSE-TODO.md](LICENSE-TODO.md) and obtain the owner's terms before assuming contribution or reuse permissions.

## Changes

Use a focused branch and explain the verified problem, affected behavior and evidence. Avoid broad rewrites for packaging. A functional fix needs a preserved baseline, the smallest safe correction, a regression test, affected-suite results and a changelog entry. Do not change telemetry/risk semantics to satisfy documentation or tests.

Preserve these invariants:

- High CPU/GPU alone does not establish malware; security risk and resource impact are independent.
- Missing measurements remain null/unavailable/unknown.
- Process identity survives PID reuse correctly.
- Persistence collection is read-only; no automatic response to workloads.
- Explain evidence and uncertainty without unsupported compromise claims.
- Heuristics remain authoritative; ML is shadow-only.
- Synthetic results are not real-workload accuracy, and risk scores are not probabilities.
- Production backend/enrollment/remote ingestion is a future Phase 23 boundary.

## Validation

Use .NET SDK 10.0.401 and locked NuGet restores. Run relevant executable C# suites, schema validation, Python methodology tests and publication checks. Run native parity when both platforms are available. Test package lifecycle only on disposable hosts with reviewed backups. Keep original acceptance evidence immutable; add a separate dated/hash-bound report for new measurements.

Use PASS, FAIL, PARTIAL, UNVERIFIED and NOT APPLICABLE accurately. Explain missing hardware/tooling instead of claiming success. Hosted CI requires a real Actions run URL and source commit.

Before committing, run `python scripts/check-repository.py`, `python -m unittest discover -s tests/Repository -v`, then `git add .` and `python scripts/secret-scan.py --staged`. Review the staged diff. Do not commit credentials, runtime state, captures, caches or CryptoGuard release binaries. Preserve notices, corresponding source, contracts and lock files.
