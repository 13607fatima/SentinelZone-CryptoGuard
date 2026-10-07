#!/usr/bin/env python3
"""Bounded benign workload. No miner, external pool or response action."""
import datetime, json, os, pathlib, socket, subprocess, sys, time
binary=str(pathlib.Path(sys.argv[1]).resolve()); root=pathlib.Path(sys.argv[2]).resolve()
root.mkdir(parents=True,exist_ok=False)
listeners=[]
for family, addr in [(socket.AF_INET,'127.0.0.1'),(socket.AF_INET6,'::1')]:
    s=socket.socket(family,socket.SOCK_STREAM)
    try: s.bind((addr,0)); s.listen(1); s.settimeout(10); listeners.append(s)
    except OSError: s.close()
ports=[s.getsockname()[1] for s in listeners]
code='''import socket,time,sys
s=[]
for index,port in enumerate(sys.argv[1].split(',')):
 c=socket.socket(socket.AF_INET if index==0 else socket.AF_INET6,socket.SOCK_STREAM)
 c.connect(('127.0.0.1' if index==0 else '::1',int(port)));s.append(c)
deadline=time.monotonic()+35
while time.monotonic()<deadline:
 x=sum(i*i for i in range(1000))
'''
worker=subprocess.Popen([sys.executable,'-c',code,','.join(map(str,ports)),'--algo=rx/0','--url=stratum+tcp://cg-user:CG_SECRET_URL@127.0.0.1:3333','--user=CG_SECRET_WALLET','--token=CG_SECRET_TOKEN','Password=CG_SECRET_CONNECTION;User ID=admin'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
connections=[s.accept()[0] for s in listeners]
expires=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=1)).isoformat()
config={'state_directory':str(root/'state'),'collector_socket':str(root/'unused.sock'),'sample_seconds':1,'network_seconds':1,'sensor_seconds':10,'export_seconds':1,'persistence_seconds':900,'confirm_seconds':5,'iocs':[{'type':'ip','value':'127.0.0.1','source':'local-bounded-loopback-test','confidence':1,'expires_at':expires}]}
(root/'config.json').write_text(json.dumps(config))
def cpu_ticks(pid):
    text=pathlib.Path(f'/proc/{pid}/stat').read_text(); f=text[text.rfind(')')+1:].split(); return int(f[11])+int(f[12])
before=cpu_ticks(worker.pid); start=time.monotonic()
with (root/'stderr.log').open('w') as error:
    result=subprocess.run([binary,'run','--mode','standalone','--config',str(root/'config.json'),'--samples','24'],stdout=subprocess.DEVNULL,stderr=error,timeout=40)
elapsed=time.monotonic()-start; after=cpu_ticks(worker.pid)
reference=100*(after-before)/os.sysconf('SC_CLK_TCK')/elapsed/os.cpu_count()
assert result.returncode==0, 'agent exit'
export=root/'export.jsonl'
subprocess.run([binary,'export','--state',str(root/'state'),'--output',str(export)],check=True)
events=[json.loads(line) for line in export.read_text().splitlines()]
processes=[p for e in events if e['event_type'] in ['inventory','telemetry'] and e['snapshot'] for p in e['snapshot']['processes'] if p['pid']==worker.pid]
readings=[p['cpu_percent_host_capacity']['value'] for p in processes if p['cpu_percent_host_capacity']['status']=='ok']
assert readings, 'CPU coverage'
measured=sum(readings)/len(readings)
assert abs(measured-reference)<=5, 'CPU error exceeds 5 percentage points'
instances={p['process_instance_id'] for p in processes}
net=[c for e in events if e['snapshot'] for c in e['snapshot']['connections'] if c['process_instance_id'] in instances]
assert any(c['family']=='ipv4' and c['owner_status']=='ok' for c in net), 'IPv4 ownership'
if len(listeners)==2: assert any(c['family']=='ipv6' and c['owner_status']=='ok' for c in net), 'IPv6 ownership'
risks=[r for e in events for r in e['risk'] if r['process_instance_id'] in instances]
assert any(r['lifecycle']=='confirmed' for r in risks), 'risk confirmation'
for path in [export,root/'stderr.log',*list((root/'state'/'spool').glob('*.event'))]:
    text=path.read_text()
    assert 'CG_SECRET_' not in text and 'cg-user' not in text, 'privacy leak'
for c in connections: c.close()
for s in listeners: s.close()
worker.wait(timeout=20)
report={'pass':True,'duration_seconds':elapsed,'cpu_reference_percent_host_capacity':reference,'cpu_measured_percent_host_capacity':measured,'cpu_error_percentage_points':abs(measured-reference),'cpu_samples':len(readings),'ipv4_owner':True,'ipv6_owner':len(listeners)==2,'confirmed_risk':True,'redaction_disk_log_export':True,'events':len(events),'note':'Benign loopback simulation with 5-second confirmation policy; no mining code or external pool.'}
(root/'report.json').write_text(json.dumps(report,indent=2)); print(json.dumps(report,indent=2))
