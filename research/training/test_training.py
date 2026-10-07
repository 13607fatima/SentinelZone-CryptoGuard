"""Tests methodology on explicitly synthetic records; not detection quality evidence."""
import json,tempfile,unittest
from pathlib import Path
import numpy as np
import train

class TrainingTests(unittest.TestCase):
    def records(self):
        return [{'host_id':f'fixture-host-{h}','session_id':f'session-{h}-{s}','label':s%2,'workload_label':'fixture','feature_contract_version':'test','features':{'x':{'value':h+s,'status':'ok'}}} for h in range(8) for s in range(4) for _ in range(8)]
    def test_host_and_session_disjoint(self):
        records=self.records();y=np.array([r['label'] for r in records]);a,b=train.split(records,y)
        self.assertFalse({records[i]['host_id'] for i in a}&{records[i]['host_id'] for i in b})
        self.assertFalse({(records[i]['host_id'],records[i]['session_id']) for i in a}&{(records[i]['host_id'],records[i]['session_id']) for i in b})
    def test_single_host_rejected(self):
        records=[r for r in self.records() if r['host_id']=='fixture-host-0']
        with self.assertRaises(ValueError):train.split(records,np.array([r['label'] for r in records]))
    def test_train_only_imputation(self):
        pipe=train.pipeline().fit(np.array([[1.],[2.],[np.nan],[3.]]),[0,1,0,1]);before=pipe.named_steps['imputer'].statistics_.copy()
        pipe.predict(np.array([[99999.],[np.nan]]));np.testing.assert_array_equal(before,pipe.named_steps['imputer'].statistics_);self.assertEqual(before[0],2)
    def test_confusion_and_fpr(self):
        report=train.metrics([0,0,1,1],[0,1,1,0]);self.assertEqual(report['confusion_matrix'],[[1,1],[1,1]]);self.assertEqual(report['false_positive_rate'],.5)
    def test_portable_export_matches_sklearn(self):
        x=np.array([[0.,0.],[1.,1.],[2.,2.],[3.,3.],[4.,np.nan],[5.,5.]]);pipe=train.pipeline().fit(x,[0,0,0,1,1,1]);m=train.portable(pipe,['a','b'],'test','test-only')
        transformed=pipe.named_steps['imputer'].transform(x).astype(np.float32);expected=pipe.predict_proba(x)[:,1]
        actual=[]
        for row in transformed:
            total=0
            for tree in m['trees']:
                index=0
                while tree[index]['suspicious_fraction'] is None:
                    n=tree[index];index=n['left'] if row[n['feature']]<=n['threshold'] else n['right']
                total+=tree[index]['suspicious_fraction']
            actual.append(total/len(m['trees']))
        np.testing.assert_allclose(actual,expected,atol=1e-12,rtol=0)
    def test_inconsistent_session_label_rejected(self):
        rows=self.records();rows[1]['label']=1
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'data.jsonl';p.write_text('\n'.join(json.dumps(r) for r in rows))
            with self.assertRaises(ValueError):train.load_dataset(p,['x'],'test')

if __name__=='__main__':unittest.main(verbosity=2)
