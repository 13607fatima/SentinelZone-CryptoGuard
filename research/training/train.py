"""Grouped Random Forest baseline. Models remain research artifacts; no production promotion here."""
from __future__ import annotations
import argparse, hashlib, json, platform
from datetime import datetime, timezone
from pathlib import Path
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.impute import SimpleImputer
from sklearn.metrics import confusion_matrix, precision_score, recall_score, f1_score
from sklearn.model_selection import GroupKFold, GroupShuffleSplit, cross_validate
from sklearn.pipeline import Pipeline

def load_dataset(path, names, version):
    records=[json.loads(line) for line in Path(path).read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    if not records: raise ValueError('empty dataset')
    sessions={}
    for r in records:
        if r['feature_contract_version']!=version or r['label'] not in (0,1):raise ValueError('dataset contract')
        key=(r['host_id'],r['session_id'])
        value=(r['workload_label'],r['label'])
        if key in sessions and sessions[key]!=value:raise ValueError('session label changed')
        sessions[key]=value
    x=np.array([[r['features'][n]['value'] if r['features'][n]['status']=='ok' and r['features'][n]['value'] is not None else np.nan for n in names] for r in records],dtype=np.float64)
    if np.isinf(x).any():raise ValueError('nonfinite feature')
    y=np.array([r['label'] for r in records])
    if set(y)!={0,1}:raise ValueError('both controlled positive and diverse normal labels are required')
    return records,x,y

def split(records,y):
    # Entire hosts are held out; a session can never straddle either side.
    hosts=np.array([r['host_id'] for r in records])
    if len(set(hosts))<2:raise ValueError('at least two independent hosts required')
    for train,test in GroupShuffleSplit(n_splits=30,test_size=0.25,random_state=42).split(np.zeros(len(y)),y,hosts):
        if set(y[train])=={0,1} and set(y[test])=={0,1}:
            assert not set(hosts[train]) & set(hosts[test])
            return train,test
    raise ValueError('cannot create host-disjoint train/test with both labels; collect more sessions')

def pipeline():
    # No scaling or SMOTE is needed for this baseline. Imputation is fitted inside each train fold.
    return Pipeline([('imputer',SimpleImputer(strategy='median',keep_empty_features=True)),('forest',RandomForestClassifier(n_estimators=200,max_depth=12,min_samples_leaf=2,max_features='sqrt',class_weight='balanced',random_state=42,n_jobs=2))])

def metrics(y,pred):
    cm=confusion_matrix(y,pred,labels=[0,1]);tn,fp,fn,tp=map(int,cm.ravel())
    return {'precision':float(precision_score(y,pred,zero_division=0)),'recall':float(recall_score(y,pred,zero_division=0)),
        'f1':float(f1_score(y,pred,zero_division=0)),'confusion_matrix':cm.tolist(),'false_positive_rate':fp/(fp+tn) if fp+tn else None,
        'normal_samples':tn+fp,'positive_samples':tp+fn}

def portable(model,names,version,model_version):
    forest=model.named_steps['forest'];positive=list(forest.classes_).index(1);trees=[]
    for estimator in forest.estimators_:
        t=estimator.tree_;nodes=[]
        for i in range(t.node_count):
            if t.children_left[i]==t.children_right[i]:
                values=t.value[i][0];node={'feature':-1,'threshold':0,'left':-1,'right':-1,'suspicious_fraction':float(values[positive]/sum(values))}
            else:node={'feature':int(t.feature[i]),'threshold':float(t.threshold[i]),'left':int(t.children_left[i]),'right':int(t.children_right[i]),'suspicious_fraction':None}
            nodes.append(node)
        trees.append(nodes)
    return {'format':'sentinelzone-forest-json-1','model_version':model_version,'feature_contract_version':version,'feature_names':names,
        'training_medians':model.named_steps['imputer'].statistics_.tolist(),'trees':trees}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--dataset',required=True);parser.add_argument('--features',required=True);parser.add_argument('--output',required=True);parser.add_argument('--model-version',required=True);parser.add_argument('--training-code-commit',required=True)
    args=parser.parse_args();catalog=json.loads(Path(args.features).read_text());names=[f['name'] for f in catalog['features']];version=catalog['version']
    records,x,y=load_dataset(args.dataset,names,version);train,test=split(records,y)
    groups=np.array([r['host_id']+'/'+r['session_id'] for r in records]);train_groups=np.array([r['host_id'] for r in records])[train]
    if len(set(train_groups))<2:raise ValueError('at least two training sessions required for grouped CV')
    cv=GroupKFold(n_splits=min(5,len(set(train_groups))))
    folds=[]
    for fit,valid in cv.split(x[train],y[train],train_groups):
        assert not set(train_groups[fit]) & set(train_groups[valid])
        if set(y[train][fit])!={0,1} or set(y[train][valid])!={0,1}:raise ValueError('CV fold lacks a class; collect more independent sessions')
        folds.append((fit,valid))
    scores=cross_validate(pipeline(),x[train],y[train],cv=folds,scoring={'precision':'precision','recall':'recall','f1':'f1'},error_score='raise')
    fitted=pipeline().fit(x[train],y[train]);report=metrics(y[test],fitted.predict(x[test]));report['group_cross_validation']={k:np.asarray(v).tolist() for k,v in scores.items() if k.startswith('test_')}
    exported=portable(fitted,names,version,args.model_version)
    out=Path(args.output);out.mkdir(parents=True,exist_ok=False)
    model_path=out/'model.json';model_path.write_text(json.dumps(exported,separators=(',',':'),allow_nan=False),encoding='utf-8')
    synthetic=any(r.get('record_source')=='fixture' for r in records)
    report.update({'assessment_source':'heuristic','mode':'shadow','dataset_contains_fixtures':synthetic,'production_acceptance':'UNVERIFIED',
        'promotion_allowed':False,'train_hosts':sorted({records[i]['host_id'] for i in train}),'test_hosts':sorted({records[i]['host_id'] for i in test}),
        'train_sessions':sorted(set(groups[train])),'test_sessions':sorted(set(groups[test])),'train_rows':len(train),'test_rows':len(test),
        'score_semantics':'mean suspicious-class leaf fraction; not malware probability'})
    manifest={'model_version':args.model_version,'sha256':hashlib.sha256(model_path.read_bytes()).hexdigest(),'feature_contract_version':version,
        'training_dataset_manifest':{'sha256':hashlib.sha256(Path(args.dataset).read_bytes()).hexdigest(),'records':len(records),'contains_fixtures':synthetic},
        'training_code_commit':args.training_code_commit,'training_code_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'created_at':datetime.now(timezone.utc).isoformat(),'metrics':report,'python':platform.python_version(),'release_status':'RESEARCH_ONLY'}
    (out/'model-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');(out/'ml-evaluation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps({'status':'RESEARCH_ONLY','sha256':manifest['sha256'],'metrics':report}))

if __name__=='__main__':main()
