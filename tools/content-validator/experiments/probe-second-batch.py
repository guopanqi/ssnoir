"""第二批固定策略对照；只用于已经逐步试跑的原型复核与寻找设计反例。"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[3]


def run(scene, policy, seed):
 p=subprocess.Popen(['dotnet','run','--no-build','--project','tools/content-validator/SSNoir.ContentValidator.csproj','--','--session','encounters/实验·'+scene,'--seed',str(seed)],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 def read():
  r=json.loads(p.stdout.readline());assert r['type']!='error',r;return r
 def send(c):
  p.stdin.write(json.dumps(c,ensure_ascii=False)+'\n');p.stdin.flush();return read()
 o=read()['observation']
 for _ in range(65):
  if o['EncounterResult'] is not None:break
  ops=o['Operations'];clock={c['Label']:c['Current'] for c in o['Clocks']}
  def pick(name,high=False):
   xs=[a for a in ops if a['Card']==name]
   return (max if high else min)(xs,key=lambda a:a['DieValue'] or 0) if xs else None
  a=None
  if scene=='赃款会拖脚':
   bag=clock['钱袋']
   target={'死抱3袋':3,'保2袋':2,'保1袋':1,'全扔':0}.get(policy,3)
   if policy=='末轮求稳' and clock['追兵']==2 and pick('翻过围墙',True):
    die=pick('翻过围墙',True)['DieValue']
    # 最好骰>=4没有坏档，以中档进度确定末轮需降到的重量；低骰仍有坏档，不伪称保证。
    if die>=4:target=max(0,min(target,4-(6-clock['脱身'])))
   if bag>target:a=pick('扔下一袋')
   if not a:a=pick('翻过围墙',True)
  else:
   if policy=='撤离':a=pick('拔掉接头')
   elif policy=='不接线':pass
   elif policy=='焊接':
    a=pick('垫好焊点') or pick('焊死接头') or pick('录下交易',True)
   else:a=pick('稳住接头') or pick('录下交易',True)
  if not a:a=next(a for a in ops if a['Kind']=='end-turn')
  o=send({'command':'act','version':o['Version'],'operationId':a['Id'],'reason':'固定对照策略：'+policy})['observation']
 else:raise RuntimeError('65步内未结束')
 with tempfile.TemporaryDirectory(prefix='noir-batch2-') as scratch:
  raw=Path(scratch)/'record.json';send({'command':'save','path':str(raw)})
  directory=ROOT/'docs'/'实验'/scene/'对照记录';directory.mkdir(parents=True,exist_ok=True)
  with gzip.open(directory/f'{policy}-{seed}.json.gz','wb') as f:f.write(raw.read_bytes())
 send({'command':'quit'});p.wait()
 r={'scene':scene,'policy':policy,'seed':seed,'result':o['EncounterResult'],'calm':o['Actors'][0]['Composure'],'injury':o['Injury']}
 print(json.dumps(r,ensure_ascii=False),flush=True);return r

if __name__=='__main__':
 results=[]
 for scene,policies in [('赃款会拖脚',['死抱3袋','保2袋','保1袋','全扔','末轮求稳']),('只够一条线',['手动','焊接','不接线','撤离'])]:
  for policy in policies:
   for seed in (7,11,23,31,41):results.append(run(scene,policy,seed))
 (ROOT/'docs'/'实验'/'第二批对照结果.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
