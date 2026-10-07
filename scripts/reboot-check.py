#!/usr/bin/env python3
"""Capture/verify reboot evidence. This tool NEVER initiates a reboot."""
import argparse,json,os,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('mode',choices=['before','after']);p.add_argument('--state',required=True);p.add_argument('--evidence',required=True);a=p.parse_args()
state=Path(a.state);evidence=Path(a.evidence);identity=json.loads((state/'identity.json').read_text(encoding='utf-8-sig'));health=json.loads((state/'health.json').read_text(encoding='utf-8-sig'))
pending={json.loads(f.read_text())['event_uid'] for f in (state/'spool').glob('*.event')}
current={'agent_id':identity['agent_id'],'sequence':identity['sequence'],'boot_id':health['snapshot']['host']['boot_id'],'observed_at':health['observed_at'],'pending_uids':sorted(pending)}
if a.mode=='before':
    with evidence.open('x') as f:json.dump(current,f,indent=2)
    print('Snapshot recorded. A separately authorized reboot is required before verification.')
else:
    before=json.loads(evidence.read_text());checks={'boot_changed':current['boot_id']!=before['boot_id'],'agent_id_unchanged':current['agent_id']==before['agent_id'],
        'sequence_advanced':current['sequence']>before['sequence'],'spool_preserved':set(before['pending_uids'])<=pending,'telemetry_resumed':current['observed_at']>before['observed_at']}
    if os.name=='nt':cmd=['powershell','-NoProfile','-Command',"(Get-Service SentinelZoneCryptoGuard).Status"]
    else:cmd=['systemctl','is-active','cryptoguard-agent','cryptoguard-collector']
    result=subprocess.run(cmd,capture_output=True,text=True);checks['service_active']=result.returncode==0 and (result.stdout.strip()=='Running' if os.name=='nt' else all(s=='active' for s in result.stdout.split()))
    report={'status':'PASS' if all(checks.values()) else 'FAIL','checks':checks};evidence.with_suffix('.result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));raise SystemExit(0 if all(checks.values()) else 1)
