#!/usr/bin/env python3
"""Runs the portable agent for an explicitly chosen duration (default 72h).
Records aggregate CPU/RSS for core and its collector; no raw command lines.
This is a measurement harness, not a claim that acceptance limits have passed.
"""
import argparse, json, os, pathlib, signal, subprocess, time, statistics
p=argparse.ArgumentParser()
p.add_argument('binary'); p.add_argument('directory'); p.add_argument('--hours',type=float,default=72)
a=p.parse_args(); root=pathlib.Path(a.directory).resolve(); root.mkdir(parents=True,exist_ok=False)
log=(root/'stderr.log').open('w')
agent=subprocess.Popen([str(pathlib.Path(a.binary).resolve()),'run','--mode','standalone','--state',str(root/'state')],stderr=log,stdout=subprocess.DEVNULL)
ticks=os.sysconf('SC_CLK_TCK'); cpus=os.cpu_count(); page=os.sysconf('SC_PAGE_SIZE')
samples=[]; previous={}; start=time.monotonic(); last=start; crashes=0; failure=None
def members(pid):
    result={pid}
    try:
        children=pathlib.Path(f'/proc/{pid}/task/{pid}/children').read_text().split()
        for child in children: result.update(members(int(child)))
    except (OSError,ValueError): pass
    return result
try:
    while time.monotonic()-start < a.hours*3600:
        if agent.poll() is not None:
            crashes+=1
            raise RuntimeError(f'agent_exited:{agent.returncode}')
        time.sleep(5); now=time.monotonic(); total=0; memory=0; current={}
        for pid in members(agent.pid):
            try:
                text=pathlib.Path(f'/proc/{pid}/stat').read_text(); f=text[text.rfind(')')+1:].split()
                key=(pid,f[19]); value=int(f[11])+int(f[12]); current[key]=value
                if key in previous: total+=max(0,value-previous[key])
                memory+=int(f[21])*page
            except (OSError,ValueError,IndexError): pass
        if previous:
            health={}
            try:health=json.loads((root/'state/health.json').read_text()).get('health',{})
            except (OSError,ValueError):pass
            row={'elapsed_seconds':now-start,'cpu_percent_host_capacity':100*total/ticks/(now-last)/cpus,'rss_bytes_all_components':memory,
                 'spool_bytes':health.get('spool_bytes'),'dropped_events':health.get('dropped_events'),'collector_errors':health.get('collector_errors',[])}
            samples.append(row)
            with (root/'samples.jsonl').open('a') as output:output.write(json.dumps(row)+'\n')
        previous=current; last=now
except Exception as error:
    failure=type(error).__name__
finally:
    if agent.poll() is None:
        agent.send_signal(signal.SIGTERM) # only this harness's own child, never a monitored workload
        agent.wait(timeout=30)
    log.close()
    cpu=sorted(s['cpu_percent_host_capacity'] for s in samples)
    report={'requested_hours':a.hours,'elapsed_hours':(time.monotonic()-start)/3600,'agent_exit':agent.returncode,'logical_cpus':cpus,'samples':len(samples),
        'cpu_mean':sum(cpu)/len(cpu) if cpu else None,'cpu_p95':cpu[min(len(cpu)-1,int(len(cpu)*.95))] if cpu else None,
        'rss_peak_bytes':max((s['rss_bytes_all_components'] for s in samples),default=0),
        'rss_growth_bytes':samples[-1]['rss_bytes_all_components']-samples[0]['rss_bytes_all_components'] if samples else None,
        'crashes':crashes,'harness_error':failure,'service_restarts':None,'mode':'portable child, no automatic restart',
        'soak72h':'MEASURED_REVIEW_REQUIRED' if time.monotonic()-start>=72*3600 and not crashes and not failure else 'UNVERIFIED',
        'reference_hardware_match':False,'note':'A short run is not a 72-hour soak. Short-lived children may fall between samples.'}
    (root/'report.json').write_text(json.dumps(report,indent=2)); print(json.dumps(report,indent=2))
if failure:raise SystemExit(1)
