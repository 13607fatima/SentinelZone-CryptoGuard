# Privacy and secret handling

Raw passwords, tokens, Authorization/Bearer secrets, connection-string secrets and wallet values must not be persisted. Argument values are omitted/redacted instead of relying only on a list of secret names: Linux retains safe flag names and redacted placeholders; Windows retains mining-related semantic flags.

Health output excludes raw process arguments and detailed process/connection/persistence inventory. Logs use exception types/status rather than messages that may contain raw arguments. Research datasets use numeric features, availability and pseudonymous metadata; they are not unrestricted endpoint exports.

Paths, process IDs, hashes, network endpoints and persistence locations can still disclose sensitive inventory. Linux hostname inclusion defaults off; the Windows adapter retains native hostname behavior. Service state is restricted to the service identity/administrators. Review exports before sharing and keep them out of Git.

The source includes obvious fake strings for redaction regression tests. They are not credentials; narrow scanner exceptions are documented in [PUBLICATION-CHECKS.md](docs/PUBLICATION-CHECKS.md). Never generalize those exceptions to real secrets or entire test directories.

No remote transport exists in this release. Phase 23 must separately validate authentication, TLS, redaction and durable ingestion. See [original implementation privacy notes](docs/PRIVACY.md) and [SECURITY.md](SECURITY.md).
