#!/usr/bin/env python3
"""Root-only clean install/purge test. Retains original endpoint state in a backup.
Touches CryptoGuard only; never reboots or changes other services.
"""
import argparse,hashlib,json,os,pwd,shutil,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--package',type=Path,required=True);p.add_argument('--sha256',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if os.geteuid()!=0:raise SystemExit('sudo required')
if hashlib.sha256(a.package.read_bytes()).hexdigest()!=a.sha256:raise SystemExit('package checksum mismatch')
out=a.output.resolve();out.mkdir(parents=True,exist_ok=False);os.chmod(out,0o700)
owner=pwd.getpwnam(os.environ.get('SUDO_USER','root'));state=Path('/var/lib/cryptoguard');cfg=Path('/etc/cryptoguard');saved=out/'original-state';savedcfg=out/'original-config';results=[]
def run(args):subprocess.run(args,check=True,timeout=120,stdout=subprocess.DEVNULL)
def report(name,ok,detail=None):
 results.append({'name':name,'status':'PASS' if ok else 'FAIL','detail':detail});f=out/'linux-clean-install.json';f.write_text(json.dumps(results,indent=2));os.chown(f,owner.pw_uid,owner.pw_gid);os.chown(out,owner.pw_uid,owner.pw_gid)
 if not ok:raise RuntimeError(name)
def health(after):
 for _ in range(60):
  f=state/'health.json'
  if f.exists() and f.stat().st_mtime>=after:
   return json.loads(f.read_text())
  time.sleep(1)
 raise RuntimeError('health_timeout')
def stop():run(['systemctl','stop','cryptoguard-agent','cryptoguard-collector'])
def start():
 stamp=time.time();run(['systemctl','start','cryptoguard-collector','cryptoguard-agent']);return health(stamp)
def pending():return {x.name for x in (state/'spool').glob('*.event')}
before=json.loads((state/'identity.json').read_text());before_pending=pending()
stop();shutil.move(state,saved);shutil.move(cfg,savedcfg)
try:
 run(['dpkg','--purge','cryptoguard-agent'])
 run(['dpkg','-i',str(a.package.resolve())]);h=start()
 report('linux_clean_install_new_identity',h['agent_id']!=before['agent_id'] and h['agent_version']=='0.22.2-rc.1')
 stop();shutil.copytree(state,out/'fresh-test-state',symlinks=True)
 run(['dpkg','--purge','cryptoguard-agent'])
 report('linux_purge_removes_only_fresh_test_state',not state.exists() and not (cfg/'agent.json').exists() and saved.is_dir() and savedcfg.is_dir())
 run(['dpkg','-i',str(a.package.resolve())]);stop()
 if state.exists():shutil.move(state,out/'reinstall-generated-state')
 if cfg.exists():shutil.move(cfg,out/'reinstall-generated-config')
 shutil.move(saved,state);shutil.move(savedcfg,cfg)
 account=pwd.getpwnam('cryptoguard')
 for root,dirs,files in os.walk(state):
  os.chown(root,account.pw_uid,account.pw_gid)
  for name in files:os.chown(Path(root)/name,account.pw_uid,account.pw_gid,follow_symlinks=False)
 h=start();report('original_linux_state_restored',h['agent_id']==before['agent_id'] and before_pending<=pending())
except Exception:
 if saved.exists():
  subprocess.run(['systemctl','stop','cryptoguard-agent','cryptoguard-collector'],stdout=subprocess.DEVNULL)
  if state.exists():shutil.move(state,out/'failed-test-state')
  if cfg.exists():shutil.move(cfg,out/'failed-test-config')
  shutil.move(saved,state);shutil.move(savedcfg,cfg)
 raise
