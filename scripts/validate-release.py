#!/usr/bin/env python3
import hashlib,json,sys,zipfile,tarfile,subprocess,shutil,io
from pathlib import Path
root=Path(sys.argv[1]).resolve()
def digest(data):return hashlib.sha256(data).hexdigest()
rows=(root/'SHA256SUMS').read_text().splitlines();tested=0
for row in rows:
 expected,name=row.split('  ',1);path=(root/name).resolve()
 if not path.is_relative_to(root) or digest(path.read_bytes())!=expected:raise SystemExit('Checksum mismatch: '+name)
 tested+=1
files={f.relative_to(root).as_posix() for f in root.rglob('*') if f.is_file() and f.name!='SHA256SUMS'}
assert files=={r.split('  ',1)[1] for r in rows},'Unlisted release file'
manifest=json.loads((root/'release-manifest.json').read_text(encoding='utf-8'));v=manifest['version']
for artifact in manifest['artifacts']:assert digest((root/artifact['path']).read_bytes())==artifact['sha256']
source=zipfile.ZipFile(root/'source'/f'SentinelZone-CryptoGuard-Source-{v}.zip')
for item in json.loads((root/'source/source-tree-manifest.json').read_text()):assert digest(source.read('SentinelZone.CryptoGuard/'+item['path']))==item['sha256']
portable=zipfile.ZipFile(root/'windows'/f'SentinelZone-CryptoGuard-Portable-{v}-win-x64.zip')
assert portable.read('CryptoGuardAgent.exe')==(root/'windows'/f'SentinelZone-CryptoGuard-{v}-win-x64.exe').read_bytes()
assert 'hardware/CryptoGuardHardwareHost.exe' in portable.namelist()
binary=root/'linux'/f'cryptoguard-agent-{v}-linux-x64';assert binary.read_bytes()[:4]==b'\x7fELF'
with tarfile.open(root/'linux'/f'cryptoguard-agent-{v}-linux-x64.tar.gz') as tar:assert tar.extractfile('cryptoguard-agent').read()==binary.read_bytes();assert tar.getmember('cryptoguard-agent').mode&0o111
deb=root/'linux'/f'cryptoguard-agent_{v}_amd64.deb'
if shutil.which('dpkg-deb'):
 data=subprocess.check_output(['dpkg-deb','--fsys-tarfile',str(deb)])
 with tarfile.open(fileobj=io.BytesIO(data)) as tar:packaged=tar.extractfile('./usr/lib/cryptoguard/cryptoguard-agent').read()
else:
 members=subprocess.check_output(['tar','-tf',str(deb)]).decode().splitlines()
 data=subprocess.check_output(['tar','-xOf',str(deb),next(n for n in members if n.startswith('data.tar'))])
 packaged=subprocess.check_output(['tar','-xOf','-','./usr/lib/cryptoguard/cryptoguard-agent'],input=data)
assert packaged==binary.read_bytes(),'DEB and standalone ELF differ'
for f in root.rglob('*'):
 if f.is_file():assert f.suffix.lower() not in {'.pfx','.key','.pub','.snk'},'Unexpected key material'
print(json.dumps({'status':'PASS','checksums_verified':tested,'source_files_verified':len(source.namelist()),'portable_binary_parity':True,'linux_tar_binary_parity':True,'deb_binary_parity':True}))
