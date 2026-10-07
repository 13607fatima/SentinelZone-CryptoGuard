#!/usr/bin/env python3
import json, sys
schema = json.load(open(sys.argv[1]))
schema['$schema'] = 'https://json-schema.org/draft/2020-12/schema'
schema['$id'] = 'urn:sentinelzone:cryptoguard:telemetry:1.2.0'
schema['title'] = 'SentinelZone CryptoGuard telemetry 1.2.0'
def refine(node):
    if isinstance(node, list):
        for item in node: refine(item)
    if not isinstance(node, dict): return
    for item in list(node.values()): refine(item)
    if 'properties' not in node: return
    p = node['properties']
    node['required'] = list(p)
    node['additionalProperties'] = False
    if set(p) == {'value','unit','status','source'}:
        p['status'] = {'type':'string','pattern':'^[a-z_0-9]+$'}
        node['allOf'] = [{'if': {'properties': {'status': {'const':'ok'}}}, 'then': {'properties':{'value':{'type':'number'}}}, 'else':{'properties':{'value':{'type':'null'}}}}]
    for name in ['event_uid','agent_id']:
        if name in p: p[name]['format'] = 'uuid'
    if 'schema_version' in p: p['schema_version'] = {'const':'1.2.0'}
    if 'event_type' in p: p['event_type'] = {'enum':['telemetry','inventory','risk','health']}
    if 'sequence' in p: p['sequence']['minimum'] = 1
    if 'score' in p: p['score'].update(minimum=0, maximum=1 if 'mode' in p else 100)
    for name in ['security_risk','resource_impact']:
        if name in p:p[name].update(minimum=0,maximum=100)
    if 'feature_coverage' in p:p['feature_coverage'].update(minimum=0,maximum=1)
    if 'assessment_source' in p:p['assessment_source']={'const':'heuristic'}
    if 'mode' in p and 'prediction' in p:p['mode']={'const':'shadow'}
    if 'severity' in p: p['severity'] = {'enum':['low','medium','high','critical']}
    if 'lifecycle' in p: p['lifecycle'] = {'enum':['observed','suspected','confirmed','resolved']}
    for name in ['sample_duration_ms','monotonic_ms','spool_bytes','dropped_events','recovered_partial_writes']:
        if name in p: p[name]['minimum'] = 0
    for name in ['observed_at','started_at','first_seen','last_seen']:
        if name in p: p[name]['pattern'] = r'(Z|\+00:00)$'
refine(schema)
with open(sys.argv[2], 'w') as f: json.dump(schema,f,indent=2); f.write('\n')
