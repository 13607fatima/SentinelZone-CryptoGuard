#!/bin/sh
# Execute only inside a dedicated disposable test VM/container as root.
set -eu
package=$(readlink -f "$1")
test "$(id -u)" = 0
test -d /run/systemd/system
apt-get update
apt-get install -y "$package"
systemctl is-active --quiet cryptoguard-agent
systemctl is-active --quiet cryptoguard-collector
sleep 8
identity=$(python3 -c 'import json; print(json.load(open("/var/lib/cryptoguard/identity.json"))["agent_id"])')
test -n "$identity"
systemctl stop cryptoguard-agent
test "$(systemctl is-active cryptoguard-agent || true)" = inactive
test "$(systemctl is-active cryptoguard-collector || true)" = inactive
systemctl start cryptoguard-agent
sleep 8
python3 -c 'import json,sys; assert json.load(open("/var/lib/cryptoguard/identity.json"))["agent_id"] == sys.argv[1]' "$identity"
# Reboot-like service lifecycle: stop both services, reexecute manager, start enabled services.
# Does not reboot the user's desktop VM.
systemctl stop cryptoguard-agent
systemctl daemon-reexec
systemctl start cryptoguard-agent
sleep 8
python3 -c 'import json,sys; assert json.load(open("/var/lib/cryptoguard/identity.json"))["agent_id"] == sys.argv[1]' "$identity"
printf '\n' >> /etc/cryptoguard/agent.json
config_hash=$(sha256sum /etc/cryptoguard/agent.json | cut -d' ' -f1)
scratch=$(mktemp -d /tmp/cryptoguard-lifecycle.XXXXXX)
trap 'rm -rf -- "$scratch"' EXIT
dpkg-deb -R "$package" "$scratch/upgrade"
sed -i 's/^Version: .*/Version: 0.22.0-2test/' "$scratch/upgrade/DEBIAN/control"
dpkg-deb --build "$scratch/upgrade" "$scratch/upgrade.deb"
dpkg -i "$scratch/upgrade.deb"
test "$config_hash" = "$(sha256sum /etc/cryptoguard/agent.json | cut -d' ' -f1)"
systemctl is-active --quiet cryptoguard-agent
python3 -c 'import json,sys; assert json.load(open("/var/lib/cryptoguard/identity.json"))["agent_id"] == sys.argv[1]' "$identity"
cp /etc/cryptoguard/agent.json "$scratch/config-good.json"
printf '{invalid-config\n' > /etc/cryptoguard/agent.json
if systemctl restart cryptoguard-agent; then echo 'Expected invalid-config failure' >&2; exit 1; fi
cp "$scratch/config-good.json" /etc/cryptoguard/agent.json
systemctl reset-failed cryptoguard-agent cryptoguard-collector
dpkg -i "$package"
systemctl restart cryptoguard-agent
sleep 8
systemctl is-active --quiet cryptoguard-agent
python3 -c 'import json,sys; assert json.load(open("/var/lib/cryptoguard/identity.json"))["agent_id"] == sys.argv[1]' "$identity"
runuser -u cryptoguard -- /usr/bin/cryptoguard-agent export --output /var/lib/cryptoguard/lifecycle-export.jsonl
/usr/bin/cryptoguard-agent validate /var/lib/cryptoguard/lifecycle-export.jsonl
dpkg --remove cryptoguard-agent
test -f /var/lib/cryptoguard/identity.json
test -f /etc/cryptoguard/agent.json
dpkg --purge cryptoguard-agent
test ! -e /var/lib/cryptoguard
test ! -e /etc/cryptoguard/agent.json
printf 'LIFECYCLE PASS install stop start manager-reexec upgrade conffile identity invalid-config rollback export remove purge\n'
