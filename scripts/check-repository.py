#!/usr/bin/env python3
"""Source-only publication checks; does not claim a build, hosted CI or product acceptance."""
import argparse
import ast
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SKIP = {'.git', 'bin', 'obj', 'artifacts', 'test-results', '.nuget', '.dotnet', '.venv', '.training-venv', '__pycache__'}
REQUIRED = ['.github/workflows/ci.yml', 'src', 'tests', 'contracts', 'schemas', 'rules', 'packaging', 'debian', 'research', 'scripts', 'docs', 'licenses',
            'global.json', 'Directory.Build.props', 'NuGet.Config', 'README.md', 'ARCHITECTURE.md', 'CHANGELOG.md', 'SECURITY.md', 'CONTRIBUTING.md',
            'LICENSE-TODO.md', 'PUSH_TO_GITHUB.md', 'RELEASE.md', 'RELEASE_MANIFEST.md', 'GITHUB_RELEASE_ASSETS.md', 'SOURCES.md', 'THIRD-PARTY-NOTICES.txt']

def check():
    results = []
    def record(name, errors):
        results.append({'check': name, 'status': 'FAIL' if errors else 'PASS', 'details': errors})
    files = [p for p in ROOT.rglob('*') if p.is_file() and not any(x in SKIP for x in p.relative_to(ROOT).parts)]
    record('standalone root', [n for n in REQUIRED if not (ROOT/n).exists()] + [n for n in ['SentinelZone.CryptoGuard', 'backend', 'agent', 'source'] if (ROOT/n).is_dir()])
    errors = []
    sdk = json.loads((ROOT/'global.json').read_text())['sdk']
    props = ET.parse(ROOT/'Directory.Build.props')
    if sdk != {'version': '10.0.401', 'rollForward': 'disable', 'allowPrerelease': False}: errors.append('Unexpected SDK pin')
    if props.findtext('.//Version') != '0.22.2-rc.1' or props.findtext('.//TargetFramework') != 'net10.0': errors.append('Unexpected product version/framework')
    for solution in ROOT.glob('*.slnx'):
        for project in ET.parse(solution).getroot():
            if 'Path' not in project.attrib: continue
            p = ROOT / project.attrib['Path']
            if not p.is_file(): errors.append(solution.name + ': missing project ' + str(p.relative_to(ROOT)))
            elif not (p.parent/'packages.portable.lock.json').is_file(): errors.append('Missing portable dependency lock: ' + str(p.relative_to(ROOT)))
    for project in ROOT.rglob('*.csproj'):
        if any(x in SKIP for x in project.relative_to(ROOT).parts): continue
        for ref in ET.parse(project).iter('ProjectReference'):
            if not (project.parent/ref.attrib['Include'].replace('\\','/')).is_file(): errors.append('Missing ProjectReference: ' + str(project.relative_to(ROOT)))
    ET.parse(ROOT/'NuGet.Config')
    record('build configuration and project references (static)', errors)
    errors=[]
    for p in files:
        try:
            if p.suffix == '.json': json.loads(p.read_text(encoding='utf-8-sig'))
            elif p.suffix == '.py': ast.parse(p.read_text(encoding='utf-8-sig'), filename=p.name)
            elif p.suffix in {'.csproj', '.props', '.slnx'}: ET.parse(p)
        except Exception as exc: errors.append(str(p.relative_to(ROOT)) + ': ' + type(exc).__name__)
    record('JSON / Python / project XML syntax', errors)
    errors=[]
    for name in (ROOT/'debian/cryptoguard-agent.docs').read_text().splitlines():
        if name and not name.startswith('#'):
            current=ROOT
            for part in Path(name).parts:
                if not current.is_dir() or part not in {p.name for p in current.iterdir()}:
                    errors.append('Missing or wrong-case package doc: ' + name); break
                current /= part
    record('Debian package documentation inputs', errors)
    errors=[]
    # Verify every repository-local Markdown link in the entry-point README.
    for target in re.findall(r'\]\(([^)]+)\)', (ROOT/'README.md').read_text()):
        if re.match(r'^[a-z]+:', target): continue
        target=target.split('#',1)[0]
        if target and (not (ROOT/target).exists() or not (ROOT/target).resolve().is_relative_to(ROOT)):
            errors.append(target)
    record('README local link targets', errors)
    workflow=(ROOT/'.github/workflows/ci.yml').read_text()
    uses=re.findall(r'uses:\s*(\S+)',workflow)
    record('GitHub Actions full-commit pins (static)', [u for u in uses if not re.fullmatch(r'[^@]+@[0-9a-f]{40}',u)])
    errors=[]
    for p in files:
        rel=p.relative_to(ROOT)
        if rel.parts[0] == 'licenses': continue  # Preserved third-party material; scanned separately by secret-scan.py.
        if p.suffix.lower() in {'.exe','.dll','.deb','.pfx','.p12','.key','.pem','.snk'}: errors.append(rel.as_posix())
        if p.name in {'.env','agent-id.json','identity.json'} or p.suffix in {'.event','.log'}: errors.append(rel.as_posix())
        if any(part in {'spool','state','state-backup','captures','exports','backups'} for part in rel.parts[:-1]): errors.append(rel.as_posix())
        if p.is_symlink(): errors.append('symlink: '+rel.as_posix())
    record('no prebuilt product binaries or private runtime material',errors)
    errors=[]
    for p in files:
        if p.relative_to(ROOT).parts[0] == 'licenses': continue
        try: content=p.read_text(encoding='utf-8-sig')
        except UnicodeDecodeError: continue
        # Generic redaction placeholders /home/[user] and platform standard paths are intentional.
        if re.search(r'\b10\.10\.\d{1,3}\.\d{1,3}\b|[A-Za-z]:[\\/]+Users[\\/]+[A-Za-z0-9_.-]+|/home/[A-Za-z0-9_.-]+/|/root/[A-Za-z0-9_.-]+',content):
            errors.append(p.relative_to(ROOT).as_posix())
    record('no first-party lab IPs or personal home paths', errors)
    original=json.loads((ROOT/'docs/release-evidence/v0.22.2-rc.1/validation/acceptance-matrix.json').read_text())
    record('original acceptance counts retained', [] if original['counts']=={'PASS':55,'FAIL':0,'UNVERIFIED':17,'NOT APPLICABLE':2} else ['Acceptance counts changed'])
    return {'status':'FAIL' if any(r['status']=='FAIL' for r in results) else 'PASS','checks':results,'source_files_considered':len(files),
            'scope':'Static source/publication checks only; no SDK build, installer execution, hosted CI or hardware gate implied.'}

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path);args=parser.parse_args()
    report=check();output=json.dumps(report,indent=2)+'\n'
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(output)
    print(output,end='');return int(report['status']=='FAIL')

if __name__=='__main__':raise SystemExit(main())
