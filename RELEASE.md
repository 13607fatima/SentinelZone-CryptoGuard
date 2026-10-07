# Release process

## Current candidate

`v0.22.2-rc.1` is **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**. The original 55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE matrix remains unchanged. Hosted CI is UNVERIFIED until a real Actions run succeeds and its URL/commit is recorded. Do not select a project license or claim signing without an owner decision and evidence.

## Source versus assets

Git tracks source, contracts, schemas, tests, packaging definitions, documentation, locks and required license/source materials. The six large product binaries/packages belong on GitHub Releases. The original source ZIP is a historical build baseline; GitHub's automatic source archive and the GitHub-ready ZIP describe the prepared handoff instead. Keep those identities distinct.

This preparation changes documentation/hygiene/build validation, fixes a case-sensitive Debian documentation input and locks the Windows migration-test restore. It does not change application source or rebuild the supplied binaries. A future rebuild must have a new manifest/SBOM/checksums and measured validation; do not reuse old byte-parity claims for it.

## Build and validate new artifacts

1. Use a clean checkout and the exact SDK in `global.json`, with locked dependency restores. Follow [README.md](README.md#build-from-source).
2. Run the relevant suites in [docs/TESTING.md](docs/TESTING.md), source checks and staged secret scan. Record actual tool versions, commit, artifact hashes and result scope.
3. On Windows run `Prepare-Inno.ps1` and `Publish.ps1 -Iscc ...`. This runs legacy/unified/migration tests and publishes the core, hardware payload and Setup. `packaging/windows/Package-Release.ps1 -NugetCache PATH -ReleaseDirectory EMPTY_DIRECTORY` assembles Windows assets/inventory after publish. Paths must be reviewed local paths; the output directory must be empty.
4. On Linux run `dpkg-buildpackage -b -us -uc` and `lintian`; retain `artifacts/linux-x64/cryptoguard-agent` and the built `.deb`. Do not run install/purge lifecycle scripts against a production/shared host.
5. Produce canonical native replay results on both OSes and compare with `scripts/compare-parity.py`. Keep original bytes and reports; synthetic parity is not detection accuracy.
6. Curate only sanitized reports into a validation input directory. Never pass a live state/export directory to the bundler. Generate Windows inventory with `packaging/windows/Inventory.ps1`.
7. Assemble a cross-platform bundle using the existing command below, substituting reviewed actual paths. Then run `scripts/validate-release.py`. The bundler retains its existing `SentinelZone.CryptoGuard/` historical source-archive naming; that archive is a release asset and does not wrap this repository root.

```bash
python scripts/release-bundle.py \
  --windows /path/to/windows-payload \
  --setup /path/to/setup.exe \
  --linux /path/to/cryptoguard-agent \
  --deb /path/to/cryptoguard-agent.deb \
  --validation /path/to/curated-reports \
  --inventory /path/to/inventory/sbom.spdx.json \
  --output /path/to/empty-output
python scripts/validate-release.py /path/to/empty-output
```

The script assembles existing inputs, computes manifests/hashes and does not establish runtime acceptance. Optional signing scripts are present but the current assets are UNSIGNED EVALUATION. Signing changes bytes: any future signed release needs signature verification, regenerated integrity metadata and appropriate retesting.

## CI

The pinned workflow builds Linux and Windows, uses locked dependency restores, runs regression/contract/migration and Python methodology tests, validates replay schemas, packages both platforms, compares native replay, uploads CI artifacts and validates tagged release structure. Repository-only checks were added without removing any original jobs/tests. The migration suite now explicitly restores in locked mode.

The release job uses the `release` environment and produces CI artifacts. It does **not** automatically publish a GitHub Release. The owner must configure environment policy and review actual job results. Hosted Actions remains UNVERIFIED in the handoff. CI's default replay job covers heuristic fixtures; the supplied historical 10-case evidence additionally includes shadow cases. Neither is broad real-workload validation.

## Publish this original evaluation candidate

Follow [PUSH_TO_GITHUB.md](PUSH_TO_GITHUB.md) and the exact [asset map](GITHUB_RELEASE_ASSETS.md). Create tag `v0.22.2-rc.1` only after review. Use the release title and notes in [GITHUB-RELEASE-NOTES.md](docs/GITHUB-RELEASE-NOTES.md), mark the release as a prerelease, and do not mark it latest/stable. Never overwrite an existing tag or silently replace previously published bytes.

The current source tag points at the prepared handoff. State explicitly that the prebuilt evaluation assets came from the original source ZIP, whose SHA-256 is recorded in the manifest. The documentation/build-validation changes do not retroactively rebuild those assets.

All 17 original gates remain open until supported by new evidence. The final owner license decision is also outstanding.
