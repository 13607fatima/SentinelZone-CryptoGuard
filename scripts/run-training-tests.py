import argparse,json,sys,unittest
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root/'research/training'))
suite=unittest.defaultTestLoader.discover(str(root/'research/training'),pattern='test_training.py')
def flatten(s):
 for t in s:
  if isinstance(t,unittest.TestSuite):yield from flatten(t)
  else:yield t.id()
ids=list(flatten(suite));result=unittest.TextTestRunner(verbosity=2).run(suite)
fail={t.id() for t,_ in result.failures+result.errors};skip={t.id() for t,_ in result.skipped}
report={'passed':result.testsRun-len(fail)-len(skip),'failed':len(fail),'skipped':len(skip),'tests':[{'name':i,'status':'FAIL' if i in fail else 'SKIP' if i in skip else 'PASS'} for i in ids],'scope':'Training methodology only, synthetic unit fixtures; not detection accuracy'}
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,indent=2))
raise SystemExit(0 if result.wasSuccessful() else 1)
