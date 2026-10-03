import json, subprocess, gzip, tempfile, sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def run(scene,policy,seed,growth,tag,money=15):
 p=subprocess.Popen(['dotnet','run','--no-build','--project','tools/content-validator/SSNoir.ContentValidator.csproj','--','--session','encounters/实验·'+scene,'--seed',str(seed),'--growth',str(growth),'--item','金钱='+str(money)],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
 def read():
  r=json.loads(p.stdout.readline());assert r['type']!='error',r;return r
 def send(c):
  p.stdin.write(json.dumps(c,ensure_ascii=False)+'\n');p.stdin.flush();return read()
 o=read()['observation'];actions=[]
 try:
  for _ in range(60):
   if o['EncounterResult'] is not None:break
   clocks={c['Label']:c['Current'] for c in o['Clocks']};ops=o['Operations']
   def pick(card,high=True):
    xs=[a for a in ops if a['Card']==card]
    return (max if high else min)(xs,key=lambda a:a['DieValue'] or 0) if xs else None
   a=None
   if policy=='等待到期':pass
   elif scene=='枪口认路':
    route='冲下栈桥' if policy=='栈桥直冲' else '穿过货棚'
    if policy in ('压制再跑','临近硬冲','进度优先') and clocks['火力']>0:
     can_spare=clocks['封锁']==0 or 3*(len(o['Actors'][0]['Dice'])-1)>=14-clocks['脱身']
     if policy!='进度优先' or can_spare:a=pick('打落枪口',clocks['火力']>1)
    a=a or pick(route)
   else:
    if policy=='省钱控压':
     strong=pick('谈交人条件')
     if strong and strong['Prepared']>=7 and clocks['交人']>=6:a=strong
     elif clocks['警戒']>=2:a=pick('让枪口放低')
     a=a or strong
    elif policy=='纯谈判':a=pick('谈交人条件')
    elif policy=='纯强抢':a=pick('冲上去抢人')
    else:
     low=pick('谈交人条件',False)
     if low and (low['DieValue']<=2 or clocks['警戒']>=3):a=pick('递出信封',False)
     if not a and clocks['警戒']>=3:a=pick('让枪口放低')
     a=a or pick('谈交人条件')
   a=a or next(a for a in ops if a['Kind']=='end-turn')
   actions.append(a['Card'] or a['Kind'])
   o=send({'command':'act','version':o['Version'],'operationId':a['Id'],'reason':'固定反例／成长对照：'+policy})['observation']
  else:raise RuntimeError('60步未结束')
  directory=ROOT/'docs/实验/第二轮'/tag/'对照';directory.mkdir(parents=True,exist_ok=True)
  with tempfile.TemporaryDirectory() as scratch:
   raw=Path(scratch)/'session.json';assert send({'command':'save','path':str(raw)})['type']=='saved'
   with gzip.open(directory/f'{scene}-{policy}-{seed}-g{growth}.json.gz','wb') as f:f.write(raw.read_bytes())
  send({'command':'quit'})
  r=dict(scene=scene,policy=policy,seed=seed,growth=growth,result=o['EncounterResult'],calm=o['Actors'][0]['Composure'],injury=o['Injury'],money=o['Inventory']['金钱'],actions=actions)
  print(json.dumps(r,ensure_ascii=False),flush=True);return r
 finally:
  if p.poll() is None:p.stdin.close()
  p.wait(timeout=10)
if __name__=='__main__':
 tag=sys.argv[1]
 if (ROOT/'docs/实验/第二轮'/tag).exists():raise FileExistsError('请使用新的记录目录名，不覆盖既有实验')
 rows=[]
 for seed in (11,41,53):
  for policy in ('压制再跑','进度优先'):rows.append(run('枪口认路',policy,seed,1,tag))
 for growth in (1,2):
  for seed in (7,19,31):rows.append(run('信封换人','省钱控压',seed,growth,tag,0))
 for scene in ('枪口认路','信封换人'):rows.append(run(scene,'等待到期',19,1,tag))
 rows.append(run('信封换人','纯谈判',19,1,tag))
 (ROOT/'docs/实验/第二轮'/tag/'结果.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
