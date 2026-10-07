# Repository preparation manifest

## Corrected rc.2 handoff

This repository is the standalone `SentinelZone-CryptoGuard` source handoff for product version `0.22.2-rc.1`. The rc.2 handoff corrects release packaging only; the product remains **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**.

- Removed the GitHub-rendered `licenses/Mono.Posix.NETStandard-license.html` artifact.
- Preserved the complete upstream plain-text Mono notice and its attribution in the repository and Debian copyright metadata.
- Restricted Debian license installation to reviewed notice, metadata and source-archive extensions; CI and `debian/rules` reject rendered license pages.
- Updated Inno Setup preparation to the official GitHub release asset while retaining checksum and Authenticode verification.
- No application source, contract, schema, rule, telemetry or risk semantics changed.

The corrected archive and checksum are delivered beside this repository. See the handoff `FINAL_REPORT.md` for the exact validation evidence and remaining UNVERIFIED gates.
