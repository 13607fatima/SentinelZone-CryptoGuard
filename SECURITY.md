# Security policy

## Evaluation scope

Version 0.22.2-rc.1 is **PRE-RELEASE / UNSIGNED EVALUATION / NOT PRODUCTION READY**. No production support or patch-response SLA is claimed. Review [known limitations](docs/KNOWN-LIMITATIONS.md) and [open gates](README.md#open-production-gates).

CryptoGuard observes evidence. High utilization alone is not malware; risk is not a calibrated compromise probability. Collection does not remove persistence or terminate monitored workloads. ML remains shadow-only.

## Reporting a vulnerability

Do not put credentials, raw endpoint exports, private keys, state/spool contents or exploit details exposing a live system into public issues. Use this repository's **Security → Report a vulnerability** if the owner has enabled private vulnerability reporting. Otherwise contact the maintainer privately using a contact they have explicitly published. No security mailbox or response time is invented by this package. The owner should enable private reporting before inviting public vulnerability submissions.

Include affected version/commit, OS/build, expected/actual behavior, a minimal sanitized reproduction and impact. Use synthetic inputs when possible. Preserve original evidence privately and provide hashes rather than secret-bearing files.

## Development safeguards

Keep source and release assets separate. Honor pinned SDK/dependency locks and checksum/signature checks in build tools. Run the staged secret scan in [PUSH_TO_GITHUB.md](PUSH_TO_GITHUB.md). Redaction tests with fake values must remain; they are not an exemption for real secrets.

The release's SHA256SUMS proves file integrity against that manifest, not publisher identity. This RC is unsigned. Optional signing tooling does not establish that any delivered artifact is signed.
