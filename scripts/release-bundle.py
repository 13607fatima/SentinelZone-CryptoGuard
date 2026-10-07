#!/usr/bin/env python3
"""Assemble already built/tested inputs. Never invent acceptance results.
Source paths are explicit and build caches/private test state are excluded.
"""
import argparse,datetime,hashlib,json,shutil,tarfile,uuid,zipfile,xml.etree.ElementTree as ET
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
VERSION=ET.parse(ROOT/'Directory.Build.props').findtext('.//Version')
SOURCE_DIRS={'.github','src','tests','packaging','schemas','contracts','rules','research','scripts','debian','docs','licenses'}
EXCLUDE={'bin','obj','.git','__pycache__','.venv','artifacts','test-results','validation','.debhelper','cryptoguard-agent','DEBIAN'}
def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()
def source_files():
 for f in sorted(ROOT.rglob('*')):
  if not f.is_file():continue
  rel=f.relative_to(ROOT)
  if any(x in EXCLUDE for x in rel.parts):continue
  if len(rel.parts)>1 and rel.parts[0] not in SOURCE_DIRS:continue
  if len(rel.parts)==1 and not(f.suffix in {'.md','.props','.slnx','.json','.Config'} or f.name in {'.gitignore','.gitattributes','THIRD-PARTY-NOTICES.txt'}):continue
  if f.suffix.lower() in {'.pfx','.key','.pub','.snk'} or 'ssh-test-key' in f.name:raise ValueError('Unexpected key material in source')
  yield f
def save(path,obj):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def zip_tree(source,target):
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
  for f in sorted(source.rglob('*')):
   if f.is_file():archive.write(f,f.relative_to(source).as_posix())
def checksums(out):
 (out/'SHA256SUMS').write_text(''.join(sha(f)+'  '+f.relative_to(out).as_posix()+'\n' for f in sorted(out.rglob('*')) if f.is_file() and f.name!='SHA256SUMS'),encoding='ascii')
def main():
 p=argparse.ArgumentParser()
 for arg in ['windows','setup','linux','deb','validation','inventory','output']:p.add_argument('--'+arg,type=Path,required=True)
 p.add_argument('--portable',type=Path);a=p.parse_args();out=a.output.resolve()
 if out.exists() and any(out.iterdir()):raise SystemExit('Use an empty output directory')
 for d in ['windows','linux','source','validation','contracts','docs','LICENSES']: (out/d).mkdir(parents=True,exist_ok=True)
 w=out/'windows';l=out/'linux'
 shutil.copy2(a.windows/'CryptoGuardAgent.exe',w/f'SentinelZone-CryptoGuard-{VERSION}-win-x64.exe')
 shutil.copy2(a.setup,w/f'SentinelZone-CryptoGuard-Setup-{VERSION}-win-x64.exe')
 portable=w/f'SentinelZone-CryptoGuard-Portable-{VERSION}-win-x64.zip'
 if a.portable:shutil.copy2(a.portable,portable)
 else:zip_tree(a.windows,portable)
 binary=l/f'cryptoguard-agent-{VERSION}-linux-x64';shutil.copy2(a.linux,binary)
 shutil.copy2(a.deb,l/f'cryptoguard-agent_{VERSION}_amd64.deb')
 with tarfile.open(l/f'cryptoguard-agent-{VERSION}-linux-x64.tar.gz','w:gz') as tar:
  def executable(t):t.mode=0o755;return t
  tar.add(binary,arcname='cryptoguard-agent',filter=executable)
  for f in ['README.md','THIRD-PARTY-NOTICES.txt']:tar.add(ROOT/f,arcname=f)
  tar.add(ROOT/'docs/LINUX-INSTALL.md',arcname='LINUX-INSTALL.md')
 for name in ['contracts','docs','licenses']:
  shutil.copytree(ROOT/name,out/('LICENSES' if name=='licenses' else name),dirs_exist_ok=True,ignore=shutil.ignore_patterns('__pycache__'))
 for name in ['README.md','THIRD-PARTY-NOTICES.txt','SOURCES.md']:shutil.copy2(ROOT/name,out/name)
 shutil.copy2(ROOT/'docs/RESEARCH-SOURCES.md',out/'RESEARCH-SOURCES.md')
 # Only curated JSON/Markdown reports are exported; live host captures and private
 # state remain outside the release. Callers place report inputs in a curated folder.
 for f in sorted(a.validation.rglob('*')):
  if f.is_file() and f.suffix in {'.json','.md','.txt'}:
   rel=f.relative_to(a.validation)
   if any(x in {'spool','state','state-backup','artifacts','test-results'} for x in rel.parts):continue
   dest=out/'validation'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
 files=list(source_files());manifest=[{'path':f.relative_to(ROOT).as_posix(),'sha256':sha(f)} for f in files]
 save(out/'source/source-tree-manifest.json',manifest)
 with zipfile.ZipFile(out/'source'/f'SentinelZone-CryptoGuard-Source-{VERSION}.zip','w',zipfile.ZIP_DEFLATED) as archive:
  for f in files:archive.write(f,'SentinelZone.CryptoGuard/'+f.relative_to(ROOT).as_posix())
 # Rebuild payload file hashes even when release-only signing changes the bytes.
 sbom=json.loads(a.inventory.read_text(encoding='utf-8-sig'))
 sbom['name']='SentinelZone CryptoGuard '+VERSION+' Windows and Linux'
 sbom['documentNamespace']='https://spdx.org/spdxdocs/CryptoGuard-'+str(uuid.uuid4())
 sbom['creationInfo']={'created':datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),'creators':['Tool: CryptoGuard release-bundle 1']}
 sbom['files']=[];sbom['relationships']=[r for r in sbom['relationships'] if not r['relatedSpdxElement'].startswith('SPDXRef-File-')]
 for package in sbom['packages']:
  if isinstance(package.get('checksums'),dict):package['checksums']=[package['checksums']]
  elif package.get('checksums','absent') is None:package['checksums']=[]
  if package['name']=='HidSharp':package['licenseDeclared']='Apache-2.0'
  if package['name'] in {'Microsoft.DotNet.ILCompiler','runtime.linux-x64.Microsoft.DotNet.ILCompiler'}:package['licenseDeclared']='MIT'
 for name,version,license,url in [
  ('PawnIO.Modules','0.1.6','LGPL-2.1-or-later','https://github.com/namazso/PawnIO.Modules/tree/0.1.6'),
  ('Microsoft.NETCore.App.Runtime.linux-x64','10.0.12','MIT','https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.linux-x64/10.0.12')]:
  ident='SPDXRef-Component-'+name
  sbom['packages'].append({'SPDXID':ident,'name':name,'versionInfo':version,'downloadLocation':url,'filesAnalyzed':False,'licenseConcluded':'NOASSERTION','licenseDeclared':license,'copyrightText':'NOASSERTION'})
  sbom['relationships'].append({'spdxElementId':'SPDXRef-Application','relationshipType':'DEPENDS_ON','relatedSpdxElement':ident})
 payload=[(f,'./windows/portable/'+f.relative_to(a.windows).as_posix()) for f in sorted(a.windows.rglob('*')) if f.is_file()]
 payload += [(f,'./'+f.relative_to(out).as_posix()) for f in sorted(out.rglob('*')) if f.is_file() and f.parent.name in {'windows','linux'}]
 for i,(f,name) in enumerate(payload):
  ident=f'SPDXRef-File-{i+1}'
  sbom['files'].append({'SPDXID':ident,'fileName':name,'checksums':[{'algorithm':'SHA256','checksumValue':sha(f)}],'licenseConcluded':'NOASSERTION','copyrightText':'NOASSERTION'})
  sbom['relationships'].append({'spdxElementId':'SPDXRef-Application','relationshipType':'CONTAINS','relatedSpdxElement':ident})
 save(out/'sbom.spdx.json',sbom)
 acceptance=out/'validation/acceptance-matrix.json'
 matrix=json.loads(acceptance.read_text()) if acceptance.exists() else {'status':'UNVERIFIED','note':'Hosted build does not imply real VM acceptance'}
 release={'product':'SentinelZone CryptoGuard','version':VERSION,'status':'RC EVALUATION — production gates remain open','signing':'UNSIGNED EVALUATION unless separately verified organization signatures accompany these bytes',
 'schema_version':'1.2.0','ruleset_version':VERSION+'-rules.1','ml_feature_contract':VERSION+'-features.1','model':'none bundled in agent; research model shadow only',
 'phase23':'NOT IMPLEMENTED BY DESIGN','source_tree_sha256':sha(out/'source/source-tree-manifest.json'),'acceptance':matrix,
 'artifacts':[{'path':f.relative_to(out).as_posix(),'bytes':f.stat().st_size,'sha256':sha(f)} for f in sorted(out.rglob('*')) if f.is_file() and f.parent.name in {'windows','linux','source'}]}
 save(out/'release-manifest.json',release);checksums(out)
 print(json.dumps({'release':str(out),'source_files':len(files),'sbom_packages':len(sbom['packages']),'sbom_files':len(sbom['files'])}))
if __name__=='__main__':main()
