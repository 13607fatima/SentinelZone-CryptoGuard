# Publication checks and synthetic fixtures

Run `python scripts/check-repository.py` for static root/configuration, link, syntax, action-pin, package-document and hygiene checks. It does not execute .NET, PowerShell, installers or hosted CI. Run `python -m unittest discover -s tests/Repository -v` for the packaging regression.

Run `python scripts/secret-scan.py` over the source tree, then after staging run `python scripts/secret-scan.py --staged`. The staged mode reads Git's index blobs, so editing a secret out of the working file without restaging does not bypass the scan. It checks added/modified/renamed/copied files, not all past Git history. Review `git diff --cached` yourself. If updating an existing repository, separately review its history before making it public.

The scanner checks private-key headers, selected provider tokens, JWT patterns, literal bearer/credential assignments and private filenames; it also reads nested third-party ZIP content. It never prints matched values. It is a transparent pattern screen, not a general entropy scanner or guarantee of secret absence. A dedicated history/entropy scanner is additional coverage, not evidence supplied by this handoff.

## Synthetic fixture exceptions

The existing C# redaction tests deliberately use fake strings such as `Bearer SuperSecret` and `password=Secret123`, plus generic local test credentials. They exercise argument filtering and verify secrets do not appear in persisted telemetry. No real account, service or credential is associated with them.

Exceptions are exact path plus exact matched literal for only:

- `tests/Contract/Program.cs`;
- `tests/CryptoGuard.Tests/Program.cs`;
- `tests/Windows/Program.cs`;
- the scanner's own exact allowlist definitions.

The scanner lists each exception by path/line/rule without echoing values. An entire tests directory is never excluded. The same fake strings in this document also have exact-path exceptions. Separately reviewed matches use exact path plus SHA-256 of the matched text: the deliberately fake JWT regression, the CG_SECRET_CONNECTION loopback fixture, a PowerShell command name, documentation language, and preserved upstream JavaScript/C# variable or function expressions. No actual secret was found in those matches. Do not add a real secret to an allowlist.

## Ignored and retained material

`.gitignore` excludes build outputs, local dependencies, private key/certificate files, local environments, runtime directories/logs/exports, backups and product release binaries. It retains schemas, dependency locks, source manifests, SBOM/evidence and required third-party source ZIPs. Ignore rules do not remove already tracked or staged files.

The original acceptance reports contain historical test summaries, not live endpoint exports. Their GUID-like fixture values and recorded service paths are evidence, not a dependency on a private host. Generic platform paths and `/home/[user]` redaction templates are intentional. `licenses/` is preserved byte-for-byte, including upstream resource files inside corresponding-source archives.
