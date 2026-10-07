# GitHub release assets — v0.22.2-rc.1

**PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**

The files below come from the supplied original `SentinelZone-CryptoGuard-0.22.2-rc.1-FINAL.zip`, whose only top-level directory is `release/`. They are uploaded to GitHub Releases, **not committed to the source repository**. The repository root must remain `SentinelZone-CryptoGuard/` with `src/` immediately inside it.

## Exact original asset map

Paths are relative to the extracted original bundle's `release/` directory. GitHub uploads use the basename, without these local subdirectories.

| Original bundle path | GitHub asset filename | Bytes | SHA-256 |
|---|---|---:|---|
| `windows/SentinelZone-CryptoGuard-0.22.2-rc.1-win-x64.exe` | `SentinelZone-CryptoGuard-0.22.2-rc.1-win-x64.exe` | 77152877 | `940adebd64432f948670468ac9c03660ae98140855bb0edd31a5ed018a509506` |
| `windows/SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe` | `SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe` | 57914219 | `2b8cc0dbe38f6cb075b274733daabdb444f3589975d240901a2cd485eab47b37` |
| `windows/SentinelZone-CryptoGuard-Portable-0.22.2-rc.1-win-x64.zip` | `SentinelZone-CryptoGuard-Portable-0.22.2-rc.1-win-x64.zip` | 75452309 | `5f01a36ab5cc9ca39ab3e9a8d5fc0cfdf1aa827388f2c1e31bc8e72ad7c1d0a0` |
| `linux/cryptoguard-agent-0.22.2-rc.1-linux-x64` | `cryptoguard-agent-0.22.2-rc.1-linux-x64` | 5141616 | `6391cefb0a863777af6a79dae4ce236cbd9caedf6158d8014ac27946211c2649` |
| `linux/cryptoguard-agent-0.22.2-rc.1-linux-x64.tar.gz` | `cryptoguard-agent-0.22.2-rc.1-linux-x64.tar.gz` | 2355322 | `69830fa29eae8ab98e83330678145b358f93e8ce27bb5d14f73b3ff566db3e1f` |
| `linux/cryptoguard-agent_0.22.2-rc.1_amd64.deb` | `cryptoguard-agent_0.22.2-rc.1_amd64.deb` | 5946958 | `af80b84b597a498a058a80f62c40a7c8f8d09afc3730e20b4b48046c029184ee` |
| `SHA256SUMS` | `SHA256SUMS` | 10726 | `05961bdf333d3eb8bf5b29f1b58948bd4bfb6ac75cab1c764494b92b2c326945` |
| `sbom.spdx.json` | `sbom.spdx.json` | 213329 | `d9515d11f65347e9800c659e081dc79c5c9c1b262d5e102a09022c8dd84cf3ae` |
| `THIRD-PARTY-NOTICES.txt` | `THIRD-PARTY-NOTICES.txt` | 1902 | `bd35a90d2ec8b8748d2050b4200821fc7621ede48ec98c8baf8d71c9fd418a9f` |
| `release-manifest.json` | `release-manifest.json` | 28244 | `60c1629d826de76ea1b8956bb476f8eab7959c8d5dd5cedbd1cd7efb47731aa4` |
| `source/SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip` | `SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip` | 5435564 | `5ac89e48dfd7017cc0f4dd90b5ba7fd942d584aac4603f6d6d0555f8fc2209e9` |

The six Windows/Linux assets above are the actual product packages. The original source ZIP preserves the precise binary-source baseline and required license material. Do not replace it with a renamed handoff ZIP: their contents and hashes differ.

## Additional recommended evidence

Attach the complete original `SentinelZone-CryptoGuard-0.22.2-rc.1-FINAL.zip` (SHA-256 `66894c79a95d64f811b7453e918d42b2930aad25db654ff14e136e5f9443a925`) to preserve all 108 manifest-listed files, licenses and acceptance evidence together. Also attach the new `SentinelZone-CryptoGuard-GitHub-Ready-v0.22.2-rc.1.zip`, its `.zip.sha256`, and `RELEASE_MANIFEST.md` as separately named handoff artifacts. These describe source preparation, not rebuilt binaries.

Original SHA256SUMS contains relative paths for the full original bundle. To verify all entries, extract that bundle and run `sha256sum -c SHA256SUMS` from its `release/` directory. It is not a flat-download checksum list and does not cover the new handoff ZIP. For an individual flat-downloaded asset, compare its SHA-256 with the exact row above (PowerShell: `Get-FileHash -Algorithm SHA256 -LiteralPath FILE`; Linux: `sha256sum FILE`). Checksums do not authenticate an unsigned publisher.

## Create and upload (Bash + authenticated GitHub CLI)

First follow [PUSH_TO_GITHUB.md](PUSH_TO_GITHUB.md), inspect actual branch and tag workflows, and create the prerelease from the repository root:

```bash
gh release create v0.22.2-rc.1 --verify-tag --prerelease --title "CryptoGuard v0.22.2-rc.1 — UNSIGNED EVALUATION" --notes-file docs/GITHUB-RELEASE-NOTES.md
```

Set the local paths below to your extracted original bundle and new handoff files. These are local placeholders, not server addresses. Do not use `--clobber` to silently replace published assets.

```bash
original_release=/absolute/path/to/extracted-original/release
handoff_files=/absolute/path/to/handoff-files
original_bundle=/absolute/path/to/SentinelZone-CryptoGuard-0.22.2-rc.1-FINAL.zip

gh release upload v0.22.2-rc.1 \
  "$original_release/windows/SentinelZone-CryptoGuard-0.22.2-rc.1-win-x64.exe" \
  "$original_release/windows/SentinelZone-CryptoGuard-Setup-0.22.2-rc.1-win-x64.exe" \
  "$original_release/windows/SentinelZone-CryptoGuard-Portable-0.22.2-rc.1-win-x64.zip" \
  "$original_release/linux/cryptoguard-agent-0.22.2-rc.1-linux-x64" \
  "$original_release/linux/cryptoguard-agent-0.22.2-rc.1-linux-x64.tar.gz" \
  "$original_release/linux/cryptoguard-agent_0.22.2-rc.1_amd64.deb" \
  "$original_release/SHA256SUMS" \
  "$original_release/sbom.spdx.json" \
  "$original_release/THIRD-PARTY-NOTICES.txt" \
  "$original_release/release-manifest.json" \
  "$original_release/source/SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip" \
  "$original_bundle" \
  "$handoff_files/SentinelZone-CryptoGuard-GitHub-Ready-v0.22.2-rc.1.zip" \
  "$handoff_files/SentinelZone-CryptoGuard-GitHub-Ready-v0.22.2-rc.1.zip.sha256" \
  "$handoff_files/RELEASE_MANIFEST.md"

gh release view v0.22.2-rc.1
```

In the GitHub UI, use **Set as a pre-release**, leave **Set as latest release** unchecked, paste the notes and attach the same files. Verify all uploaded names/sizes/hashes and visible evaluation warnings. Never claim hosted CI, signing, physical hardware or production readiness from asset upload alone.

No release or repository was published during this handoff task. The teammate's owner URL is intentionally unspecified.
