# Current 0.22.2-rc.1 validation

Use tests/Packaging/windows-lifecycle.ps1 and linux-lifecycle.py for current service tests, and their clean-install companions only with reviewed backups. scripts/lifecycle-test.sh is a preserved 0.22.1 disposable-host harness; it is not the current upgrade acceptance script. Current commands and measured evidence are in docs/TESTING.md and release/validation.

# Test isolation and interpretation

`lifecycle-test.sh PACKAGE` is destructive to the installed cryptoguard-agent
package and its state. Run only as root in an isolated disposable test container
or VM. It tests install, graceful stop/start, systemd manager reexec (reboot-like,
not an actual OS reboot), metadata-only version upgrade, conffile/identity
preservation, invalid-configuration failure, manual config recovery/downgrade,
export, remove and purge. It does not test binary/schema migration or automatic
rollback. Do not run this destructive suite on the user's Splunk server.

The container recipe deliberately contains no .NET SDK/runtime. Runtime package
dependencies must be satisfiable through apt on each tested Ubuntu version.
Container root is mapped to the unprivileged VM account; no host PID/network
namespace or writable host state is shared. Hardware coverage in a container
does not replace actual VM or physical-device tests.

`soak.py` can run for 72 hours and accounts for the core and collector together.
Report actual elapsed duration, sample count and hardware. Short tests do not
establish 72-hour stability or dataset-level accuracy. Its SIGTERM is directed
only to its own spawned agent child as a shutdown test.

The local Maintainer address build@cryptoguard.invalid is a deliberately reserved
placeholder, not a user's address or a monitored support mailbox. Set real owner
metadata and select the new-code license before public distribution.

The 0.22.1 server evidence comes from install-and-test-0221.py, live-integration.py,
the managed regression suite, and CryptoGuard.SpoolBenchmark. The prepared
installation test is tied to a package SHA-256 and the explicitly requested host;
edit/review those values before using it elsewhere. It restores temporary config
and removes its benign cron fixture. It leaves the agent installed and active.
record_server_validation.py records these actual results; release.py collects
them without requiring or implying the previous release's OS matrix results.
