"""Generate synthetic records ONLY to exercise training/export. Never production training data."""
import argparse,json,random,uuid
from pathlib import Path
from datetime import datetime,timezone,timedelta
p=argparse.ArgumentParser();p.add_argument('--catalog',required=True);p.add_argument('--output',required=True);a=p.parse_args()
catalog=json.loads(Path(a.catalog).read_text());rng=random.Random(42);epoch=datetime(2026,1,1,tzinfo=timezone.utc)
with open(a.output,'x',encoding='utf-8') as out:
    for host in range(8):
        for session in range(4):
            positive=session%2;session_id=str(uuid.uuid5(uuid.NAMESPACE_URL,f'pipeline-fixture/{host}/{session}'))
            for sample in range(20):
                features={}
                for f in catalog['features']:
                    name=f['name'];value=rng.uniform(0,100)
                    if name in ['device_gpu_percent','cpu_temperature_celsius','gpu_temperature_celsius','process_gpu_percent']:value=None
                    if name in ['persistence_present','mining_argument_combination','suspicious_path']:value=positive
                    if name=='executable_trusted':value=1-positive
                    if name=='logical_cpu_count':value=4
                    features[name]={'value':value,'status':'unsupported' if value is None else 'ok','unit':f['unit']}
                record={'session_id':session_id,'host_id':f'explicit-fixture-host-{host}','platform':'windows' if host%2 else 'linux',
                    'hardware_profile':{'logical_cpu_count':4,'source':'synthetic_fixture'},'workload_label':'controlled_suspicious' if positive else 'normal_cpu_stress',
                    'started_at':epoch.isoformat(),'observed_at':(epoch+timedelta(seconds=sample*5)).isoformat(),'features':features,'label':positive,
                    'feature_contract_version':catalog['version'],'record_source':'fixture'}
                out.write(json.dumps(record)+'\n')
