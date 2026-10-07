import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('windows',type=Path);p.add_argument('linux',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
def read(f):return [json.loads(line) for line in f.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
names=sorted({x.name for x in a.windows.glob('*.jsonl')}|{x.name for x in a.linux.glob('*.jsonl')})
tests=[]
for name in names:
    win=a.windows/name;lin=a.linux/name
    ok=win.is_file() and lin.is_file() and read(win)==read(lin)
    tests.append({'name':name,'status':'PASS' if ok else 'FAIL','rows':len(read(win)) if win.is_file() else 0})
report={'passed':sum(t['status']=='PASS' for t in tests),'failed':sum(t['status']=='FAIL' for t in tests),'tests':tests,'scope':'Entire normalized risk result; evidence, ML, coverage and lifecycle included'}
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(report,indent=2))
if not tests or report['failed']:raise SystemExit(1)
print(json.dumps(report,indent=2))
