# Original v0.22.2-rc.1 release evidence

These files are copied byte-for-byte from the supplied evaluation bundle. They describe the original release, not a new repository build and not tests rerun during repository preparation.

- Input: `SentinelZone-CryptoGuard-0.22.2-rc.1-FINAL.zip`.
- Input SHA-256: `66894c79a95d64f811b7453e918d42b2930aad25db654ff14e136e5f9443a925`.
- Source ZIP: `release/source/SentinelZone-CryptoGuard-Source-0.22.2-rc.1.zip`.
- Source ZIP SHA-256: `5ac89e48dfd7017cc0f4dd90b5ba7fd942d584aac4603f6d6d0555f8fc2209e9`.
- Original source root: `SentinelZone.CryptoGuard/`; its contents now form this repository root.

`validation/` retains the supplied reports and explicitly research-only numeric model. The model is not an accepted production model or agent default. No real endpoint exports, spool or identity files are included.

`release-manifest.json`, `sbom.spdx.json` and `SHA256SUMS` refer to the original bundle. Their path references remain relative to that bundle's `release/` root, so a checksum command must run there, not here. `source-tree-manifest.json` describes the original 245-file source ZIP, not the modified handoff. Historical report references are preserved rather than rewritten.

Original acceptance remains 55 PASS / 0 FAIL / 17 UNVERIFIED / 2 NOT APPLICABLE. New publication/build-source checks are separate in [RELEASE_MANIFEST.md](../../../RELEASE_MANIFEST.md). Rebuilt binaries need new checksums and new evidence; these reports must not be relabeled as their acceptance.
