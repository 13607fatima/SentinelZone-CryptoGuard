#!/usr/bin/env python3
"""Offline publication screening. --staged inspects indexed bytes, not worktree bytes.
No scan proves absence of every possible secret. Matches never print secret values.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import re
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PATTERNS = {
    'private-key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |DSA |ENCRYPTED )?PRIVATE KEY-----'),
    'github-token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{30,})\b'),
    'aws-access-key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'slack-token': re.compile(r'\bxox[baprs]-[A-Za-z0-9-]{15,}\b'),
    'jwt': re.compile(r'\beyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\b'),
    'bearer-literal': re.compile(r'(?i)\bBearer\s+([A-Za-z0-9_./+=-]{6,})'),
    'credential-literal': re.compile(r'''(?i)\b(?:password|passwd|api[_-]?key|access[_-]?token|session[_-]?token)\s*[=:]\s*['"]?([A-Za-z0-9_+/.=-]{4,})'''),
}
# Exact files and exact, visibly synthetic matches; no directory-wide exemptions.
FIXTURES = {
    'tests/Contract/Program.cs': {'password=Secret123', 'Password=test', 'Bearer SuperSecret', 'session_token=TopSecret'},
    'tests/CryptoGuard.Tests/Program.cs': {'password=super-secret', 'Password=secret', 'Bearer secret'},
    'tests/Windows/Program.cs': {'Password=CONNECTION'},
    'docs/PUBLICATION-CHECKS.md': {'Bearer SuperSecret', 'password=Secret123'},
}
REVIEWED_MATCHES = {'PRIVACY.md': {'07fa4de38d71afdc8155ac553ecabf0bbef845c71cf86d2cdabee20e4fb546cf': 'Documentation language, not a credential value.'}, 'README.md': {'07fa4de38d71afdc8155ac553ecabf0bbef845c71cf86d2cdabee20e4fb546cf': 'Documentation language, not a credential value.'}, 'packaging/windows/Sign-CI.ps1': {'bb9156d02580f3ea42726c0b4c291b347a8fa4279d73932e145467c53396af96': 'PowerShell command name ConvertTo-SecureString, not a credential literal.'}, 'scripts/live-integration.py': {'8a3af65116a42509994f6a5739abb4e42b12ebc26fa978563f9eb40e4739d8c1': 'Explicit CG_SECRET_CONNECTION fixture in bounded loopback test.'}, 'tests/Contract/Program.cs': {'09cdea31a4f6a360e9568c79cd01e3ed6d942ed29e9d5015b28d1b3be6b121d0': 'JWT has literal FIXTURE_SECRET signature and only synthetic lab payload; redaction regression.'}, 'licenses/windows-alpha3/LibreHardwareMonitor-v0.9.6-source.zip!LibreHardwareMonitor-0.9.6/LibreHardwareMonitor/Resources/Web/js/jquery-1.7.2.js': {'4dcb92ccc07f7bd9e06310cb8a1971b0ff7a0644ef6a45758b8b6631b2b684e7': 'Preserved upstream source expression (function/null/variable reference), not a credential value.', '77d1fce46c9fa256c9270016c8e59a6440e48483840b47a103104d869cafb8af': 'Preserved upstream source expression (function/null/variable reference), not a credential value.'}, 'licenses/windows-alpha3/LibreHardwareMonitor-v0.9.6-source.zip!LibreHardwareMonitor-0.9.6/LibreHardwareMonitor/Resources/Web/js/jquery-1.7.2.min.js': {'10322cb7688472f9bc0d88d3e5e897e4f18b22dafd471db36bc20c6f543019ec': 'Preserved upstream source expression (function/null/variable reference), not a credential value.'}, 'licenses/windows-alpha3/LibreHardwareMonitor-v0.9.6-source.zip!LibreHardwareMonitor-0.9.6/LibreHardwareMonitor/UI/AuthForm.cs': {'716de9259ecb2efb30048e36eade9a170e44018839a71e7b9f14308a521b9dca': 'Preserved upstream source expression (function/null/variable reference), not a credential value.'}, 'licenses/windows-alpha3/LibreHardwareMonitor-v0.9.6-source.zip!LibreHardwareMonitor-0.9.6/LibreHardwareMonitor/Utilities/HttpServer.cs': {'d5ca92ce5ed2673768c18d5eb207d83b54066f755043bc5f34578557ddc6f9d1': 'Preserved upstream source expression (function/null/variable reference), not a credential value.'}}

SKIP = {'.git', 'bin', 'obj', 'artifacts', 'test-results', 'validation', '.nuget', '.dotnet', '.training-venv', '.venv', 'venv', '__pycache__'}
PRIVATE_SUFFIXES = {'.pfx', '.p12', '.key', '.pem', '.snk', '.jks', '.keystore'}

def candidates(staged):
    if staged:
        names = subprocess.check_output(['git', 'diff', '--cached', '--name-only', '--diff-filter=ACMR', '-z'], cwd=ROOT).decode().split('\0')
        for name in filter(None, names):
            yield name, subprocess.check_output(['git', 'show', ':' + name], cwd=ROOT)
    else:
        for path in sorted(ROOT.rglob('*')):
            rel = path.relative_to(ROOT)
            # Historical validation reports are source evidence, not local output.
            if not path.is_file() or any(p in SKIP for p in rel.parts[:-1] if not (p == 'validation' and rel.parts[0] == 'docs')):
                continue
            yield rel.as_posix(), path.read_bytes()

def inspect(name, data, findings, exceptions, depth=0):
    path = Path(name)
    if path.suffix.lower() in PRIVATE_SUFFIXES or path.name in {'id_rsa', 'id_ed25519'} or (path.name.startswith('.env') and path.name not in {'.env.example', '.env.template'}):
        findings.append({'path': name, 'rule': 'private-material-filename'})
    if path.suffix.lower() == '.zip':
        if depth >= 3:
            findings.append({'path': name, 'rule': 'nested-archive-depth-review-required'})
            return
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            for item in archive.infolist():
                if not item.is_dir():
                    inspect(name + '!' + item.filename, archive.read(item), findings, exceptions, depth + 1)
        return
    if b'\x00' in data:
        return
    try:
        content = data.decode('utf-8-sig')
    except UnicodeDecodeError:
        return
    for line_number, line in enumerate(content.splitlines(), 1):
        for rule, pattern in PATTERNS.items():
            for match in pattern.finditer(line):
                if match.group(0) in FIXTURES.get(name, set()):
                    exceptions.append({'path': name, 'line': line_number, 'rule': rule, 'reason': 'exact synthetic redaction fixture'})
                # The scanner's exact allowlist entries are documentation of the same fake strings.
                elif name == 'scripts/secret-scan.py' and line.lstrip().startswith(("'tests/", "'docs/PUBLICATION-CHECKS.md'")) and match.group(0) in set().union(*FIXTURES.values()):
                    exceptions.append({'path': name, 'line': line_number, 'rule': rule, 'reason': 'exact fixture allowlist definition'})
                elif hashlib.sha256(match.group(0).encode()).hexdigest() in REVIEWED_MATCHES.get(name, {}):
                    exceptions.append({'path': name, 'line': line_number, 'rule': rule, 'reason': REVIEWED_MATCHES[name][hashlib.sha256(match.group(0).encode()).hexdigest()]})
                else:
                    findings.append({'path': name, 'line': line_number, 'rule': rule})

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staged', action='store_true')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    findings, exceptions = [], []
    inventory = []
    for name, data in candidates(args.staged):
        inventory.append({'path': name, 'sha256': hashlib.sha256(data).hexdigest()})
        inspect(name, data, findings, exceptions)
    report = {'status': 'FAIL' if findings else 'PASS', 'scope': 'staged added/modified blobs' if args.staged else 'source tree, excluding build/runtime output directories',
              'files_scanned': len(inventory), 'rules': list(PATTERNS), 'findings': findings, 'reviewed_exceptions': exceptions,
              'limitations': 'Pattern screening and private filename checks, including nested source ZIPs; not a general entropy scanner, full Git-history scan, or guarantee of no secrets.',
              'inventory_sha256': hashlib.sha256(json.dumps(inventory, sort_keys=True).encode()).hexdigest()}
    output = json.dumps(report, indent=2) + '\n'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output)
    print(output, end='')
    return 1 if findings else 0

if __name__ == '__main__':
    raise SystemExit(main())
