#!/usr/bin/env python3
"""Real VM install/upgrade/remove/reinstall tests. Only CryptoGuard services are changed."""
import argparse,hashlib,json,os,pwd,shutil,statistics,subprocess,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--package',required=True);p.add_argument('--sha256',required=True);p.add_argument('--output',required=True);p.add_argument('--seconds',type=int,default=180);a=p.parse_args()
if os.geteuid()!=0:raise SystemExit('Run with sudo in your own terminal.')
package=Path(a.package).resolve();out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=False)
os.umask(0o077);owner=pwd.getpwnam(os.environ.get('SUDO_USER','root'));results=[]
def write(name,value):
    path=out/name;path.write_text(json.dumps(value,indent=2) if not isinstance(value,str) else value);os.chown(path,owner.pw_uid,owner.pw_gid);os.chmod(path,0o600)
def run(args,timeout=90):
    result=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,timeout=timeout)
    if result.returncode:raise RuntimeError(f'{args[0]} failed: {result.returncode}')
    return result.stdout
def check(name,condition,detail=None):
    results.append({'name':name,'status':'PASS' if condition else 'FAIL','detail':detail});write('linux-lifecycle.json',results);print(name+': '+results[-1]['status'],flush=True)
    if not condition:raise AssertionError(name)
def prop(unit,name):return run(['systemctl','show',unit,'--property='+name,'--value']).strip()
units=['cryptoguard-agent','cryptoguard-collector'];state=Path('/var/lib/cryptoguard');config=Path('/etc/cryptoguard/agent.json')
def active():return all(prop(unit,'ActiveState')=='active' for unit in units)
def health(after):
    deadline=time.monotonic()+50
    while time.monotonic()<deadline:
        try:
            f=state/'health.json';h=json.loads(f.read_text())
            if f.stat().st_mtime>after and h['agent_version']=='0.22.2-rc.1' and active():return h
        except (OSError,ValueError):pass
        time.sleep(1)
    raise TimeoutError('fresh_health')
def identity():return json.loads((state/'identity.json').read_text())
def pending():return {json.loads(f.read_text())['event_uid'] for f in (state/'spool').glob('*.event')}
def restart():
    stamp=time.time();run(['systemctl','restart',*units]);return health(stamp)
report={'version':'0.22.2-rc.1','reboot':'UNVERIFIED','soak72h':'UNVERIFIED','package_sha256':hashlib.sha256(package.read_bytes()).hexdigest()}
original_config=None
try:
    check('package_checksum',report['package_sha256']==a.sha256)
    before=identity() if (state/'identity.json').exists() else None
    run(['systemctl','stop',*units]);shutil.copytree(state,out/'state-backup',symlinks=True) if state.exists() else None
    if config.exists():shutil.copy2(config,out/'config-backup.json')
    before_uids=pending() if state.exists() else set();stamp=time.time()
    run(['dpkg','-i',str(package)],timeout=120);run(['systemctl','start',*units]);h=health(stamp)
    check('linux_upgrade_and_systemd',active() and h['snapshot']['host']['memory_total_bytes']['value']>0)
    check('agent_id_sequence_pending_preserved',before is None or (identity()['agent_id']==before['agent_id'] and identity()['sequence']>=before['sequence'] and before_uids<=pending()),{'previous_pending_count':len(before_uids),'pending_now':len(pending())})
    binary=Path('/usr/lib/cryptoguard/cryptoguard-agent');report['binary_sha256']=hashlib.sha256(binary.read_bytes()).hexdigest()
    check('linux_native_version',run([str(binary),'version']).strip()=='0.22.2-rc.1')
    run([str(binary),'export','--output',str(out/'live-export.jsonl')]);check('export_with_service_running',(out/'live-export.jsonl').stat().st_size>0 and active())
    prior=identity();run(['systemctl','stop',*units]);check('linux_stop',all(prop(u,'ActiveState')=='inactive' for u in units));h=restart();check('linux_restart',identity()['agent_id']==prior['agent_id'] and active())
    # Recheck the previously fixed >watchdog sampling interval without changing persistent configuration.
    original_config=config.read_bytes();original_mode=config.stat().st_mode & 0o777
    altered=json.loads(original_config);altered.update(sample_seconds=90,network_seconds=90,sensor_seconds=90,export_seconds=90)
    config.write_text(json.dumps(altered));os.chmod(config,original_mode);h=restart();pid=prop(units[0],'MainPID');restarts=prop(units[0],'NRestarts')
    time.sleep(105)
    check('watchdog_with_90_second_interval',active() and prop(units[0],'MainPID')==pid and prop(units[0],'NRestarts')==restarts)
    config.write_bytes(original_config);os.chmod(config,original_mode);original_config=None;h=restart()
    ticks=os.sysconf('SC_CLK_TCK');page=os.sysconf('SC_PAGE_SIZE');cpus=os.cpu_count();previous={};rows=[];start=time.monotonic();last=start
    initial=h['health'];restart_before={u:prop(u,'NRestarts') for u in units}
    def members(pid):
        found={pid}
        try:
            for child in Path(f'/proc/{pid}/task/{pid}/children').read_text().split():found.update(members(int(child)))
        except OSError:pass
        return found
    while time.monotonic()-start<a.seconds:
        now=time.monotonic();usage=0;rss=0;current={};all_pids=set()
        for unit in units:all_pids.update(members(int(prop(unit,'MainPID'))))
        for pid in all_pids:
            try:
                text=Path(f'/proc/{pid}/stat').read_text();f=text[text.rfind(')')+1:].split();key=(pid,f[19]);cpu=int(f[11])+int(f[12]);current[key]=cpu
                if key in previous:usage+=max(0,cpu-previous[key])
                rss+=int(f[21])*page
            except (OSError,ValueError,IndexError):pass
        if previous:rows.append({'elapsed_seconds':now-start,'cpu_percent_host_capacity':100*usage/ticks/(now-last)/cpus,'rss_bytes':rss})
        previous=current;last=now;time.sleep(5)
    h=json.loads((state/'health.json').read_text());cpu=sorted(row['cpu_percent_host_capacity'] for row in rows)
    resource={'duration_seconds':time.monotonic()-start,'cpu_mean':statistics.mean(cpu),'cpu_p95':cpu[min(len(cpu)-1,int(.95*len(cpu)))],'rss_peak_bytes':max(row['rss_bytes'] for row in rows),
        'spool_growth_bytes':h['health']['spool_bytes']-initial['spool_bytes'],'dropped_events_delta':h['health']['dropped_events']-initial['dropped_events'],
        'collector_errors':h['health']['collector_errors'],'service_restarts':{u:int(prop(u,'NRestarts'))-int(restart_before[u]) for u in units},'scope':'Core, collector and live descendants sampled every 5 seconds; short-lived child CPU may be missed','soak72h':'UNVERIFIED'}
    write('linux-resource.json',resource);check('bounded_resource_measurement',active() and len(rows)>=max(1,a.seconds//5-2),resource)
    before=identity();before_uids=pending();run(['apt-get','remove','-y','cryptoguard-agent'],timeout=120)
    check('linux_remove_retains_state',identity()['agent_id']==before['agent_id'] and before_uids<=pending())
    stamp=time.time();run(['dpkg','-i',str(package)],timeout=120);run(['systemctl','start',*units]);h=health(stamp)
    check('linux_reinstall',active() and h['agent_id']==before['agent_id'])
    report.update(status='PASS',tests=results);write('linux-service-report.json',report)
except Exception as error:
    report.update(status='FAIL',error_type=type(error).__name__,tests=results);write('linux-service-report.json',report);raise
finally:
    if original_config is not None:config.write_bytes(original_config);os.chmod(config,original_mode);restart()
    os.chown(out,owner.pw_uid,owner.pw_gid);os.chmod(out,0o700)
    for name in ['live-export.jsonl']:
        file=out/name
        if file.exists():os.chown(file,owner.pw_uid,owner.pw_gid);os.chmod(file,0o600)
