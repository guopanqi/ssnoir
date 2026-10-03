"""冻结后的正式会话对照：公开观察决策、核对转移、保存原生轨迹并严格回放。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import gzip,json,re,runpy,hashlib,subprocess,tempfile
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
B=runpy.run_path(str(HERE/'分段筛选.py'))
C=runpy.run_path(str(HERE/'筛选.py'))
OUT=HERE/'正式对照';assert not OUT.exists(),OUT;OUT.mkdir()
models={}
CONFIGS={'整段完成':(5,3,2,True),'分段短目标':(4,3,2,True),
         '分段保留对照':(5,3,2,False),'保留终点':(2,2,1,False)}
jobs=[]
for variant,policies,seeds in [
 ('整段完成',('求解','先小目标','先大目标'),range(1101,1117)),
 ('分段短目标',('求解',),range(1117,1125)),
 ('分段保留对照',('求解','先小目标'),range(1117,1125)),
 ('保留终点',('求解','先额外','留够容量'),range(1125,1133))]:
 for policy in policies:
  if variant=='保留终点':m=C['Model']('收尾',policy=policy)
  else:
   a,b,t,reset=CONFIGS[variant];m=B['Model']((a,b),reset,policy)
  m.summary();models[(variant,policy)]=m
  jobs.extend((variant,policy,s) for s in seeds)

def player(o):return next(x for x in o['Actors'] if x['Id']=='player')
def injury(o):return int(re.search(r'(\d+)/7',o['Injury'])[1])
def clocks(o):return {x['Label']:x for x in o['Clocks']}
def state(o):
 c=clocks(o);p=player(o)
 return c['目标A']['Max']-c['目标A']['Current'],c['目标B']['Max']-c['目标B']['Current'],p['Composure'],c['已过回合']['Max']-c['已过回合']['Current'],tuple(sorted(x['Value'] for x in p['Dice']))
def operation(o,choice):
 route,act,d=choice
 if route=='结束回合':return next(x for x in o['Operations'] if x['Kind']=='end-turn')
 name=('稳做' if act=='稳健' else '快做')+('A' if route==0 else 'B')
 return next(x for x in o['Operations'] if x['Card']==name and x['DieValue']==d)
def fallback(o):
 a,b,c,t,h=state(o)
 def score(op):
  if op['Kind']!='action':return (-100,0)
  target=a if op['Card'].endswith('A') else b
  probs=H['odds'](op['Odds']);risk=op['Card'].startswith('快做')
  gains=(0,1,2) if risk else (0,0,1);paid=(1,1,0) if risk else (1,0,0)
  return (sum(probs[k]*min(target,gains[i]) for i,k in enumerate(('坏','中','好')))-.5*sum(probs[k]*paid[i] for i,k in enumerate(('坏','中','好'))),op['DieValue'])
 actions=[x for x in o['Operations'] if x['Kind']=='action']
 return max(actions,key=score) if actions else next(x for x in o['Operations'] if x['Kind']=='end-turn')

def check_step(variant,before,op,response):
 after=response['observation'];cb=clocks(before);pc=player(before)['Composure']
 a,b=cb['目标A']['Current'],cb['目标B']['Current'];elapsed=cb['已过回合']['Current'];cost=0
 if op['Kind']=='end-turn':
  cost=1;elapsed+=1
  if CONFIGS[variant][3]:
   if a<CONFIGS[variant][0]:a=0
   if b<CONFIGS[variant][1]:b=0
 else:
  label=next(e['Text'].split('，')[0].split('：')[1] for e in response['events'] if e['Text'].startswith('判定：'))
  index={'Fail':0,'Neutral':1,'Success':2}[label]
  risk=op['Card'].startswith('快做');gain=(0,1,2)[index] if risk else (0,0,1)[index]
  cost=(1,1,0)[index] if risk else (1,0,0)[index]
  if op['Card'].endswith('A'):a=min(CONFIGS[variant][0],a+gain)
  else:b=min(CONFIGS[variant][1],b+gain)
  if injury(before)==0:
   actual=H['odds'](op['Odds']);assert all(abs(actual[k]-v)<1e-12 for k,v in zip(('坏','中','好'),C['odds'](op['DieValue']))),(variant,op)
 assert player(after)['Composure']==max(0,pc-cost),(variant,pc,cost,player(after))
 if after['EncounterResult'] is None:
  ca=clocks(after);assert (ca['目标A']['Current'],ca['目标B']['Current'],ca['已过回合']['Current'])==(a,b,elapsed)
  if op['Kind']=='action':
   rest=[d['Value'] for d in player(before)['Dice']];rest.remove(op['DieValue'])
   assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
  elif injury(after)==0:assert len(player(after)['Dice'])==4
  assert all(not x['Card'] or not (a==CONFIGS[variant][0] and x['Card'].endswith('A')) for x in after['Operations'])
  assert all(not x['Card'] or not (b==CONFIGS[variant][1] and x['Card'].endswith('B')) for x in after['Operations'])
 else:
  fields=after['EncounterResult'].strip('()').split();status=fields[0].lstrip("'")
  assert tuple(map(int,fields[1:3]))==(a,b),(variant,fields,(a,b))
  assert int(fields[3])==min(CONFIGS[variant][2],elapsed+1)
  assert int(fields[4])==max(0,pc-cost)
  if status=='success':assert a==CONFIGS[variant][0] and (variant=='保留终点' or b==CONFIGS[variant][1])
  elif status=='timeout':assert elapsed==CONFIGS[variant][2]
  else:raise AssertionError((variant,fields))
 return after

def run(job):
 variant,policy,seed=job;m=models[(variant,policy)];s=H['Session']('研究·'+variant,seed)
 seen_injury=False;steps=0
 try:
  while s.observation['EncounterResult'] is None:
   o=s.observation;a,b,c,t,h=state(o)
   if injury(o)>0:seen_injury=True
   if seen_injury:op=fallback(o);why='已超出无伤模型；按当前公开赔率与剩余目标做局部选择。'
   else:
    if variant=='保留终点':choice=m.choose(a,b,h)
    else:choice=max(m.candidates(a,b,c,t,h),key=lambda x:(round(x[0][0],12),round(x[0][1],12)))[1]
    op=operation(o,choice);why=f'{policy}：只依据当前目标、冷静、剩余轮数与手牌；不读取未来随机数。'
   response=s.act(op,why);check_step(variant,o,op,response);steps+=1
   assert steps<=16,(variant,seed)
  o=s.observation;seen_injury=seen_injury or injury(o)>0
  fields=o['EncounterResult'].strip('()').split();success=fields[0].lstrip("'")=='success'
  bonus=success and variant=='保留终点' and int(fields[2])==CONFIGS[variant][1]
  path=OUT/f'{variant}-{policy}-s{seed}.json.gz';rev=s.save(path)
  return dict(variant=variant,policy=policy,seed=seed,success=success,bonus=bonus,injury_free_success=success and not seen_injury,
              left_injury_free_model=seen_injury,composure=player(o)['Composure'],steps=steps,revision=rev,path=path.name)
 finally:s.close()
with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(run,jobs))
assert len({x['revision'] for x in rows})==1
(OUT/'结果.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
summary=[]
for key in models:
 rs=[x for x in rows if (x['variant'],x['policy'])==key]
 summary.append(dict(variant=key[0],policy=key[1],games=len(rs),success=sum(x['success'] for x in rs),
                     injury_free_success=sum(x['injury_free_success'] for x in rs),bonus=sum(x['bonus'] for x in rs),
                     out_of_model=sum(x['left_injury_free_model'] for x in rs)))
print(json.dumps(summary,ensure_ascii=False),flush=True)

# 边界1：A已有半成品、B完成；回合末只清A。使用真实骰，寻找合适起手。
variant='整段完成'
for seed in range(1201,1301):
 s=H['Session']('研究·'+variant,seed)
 hand=state(s.observation)[4]
 if hand.count(6)>=2 and any(d in (4,5) for d in hand):break
 s.close()
else:raise AssertionError('缺少边界手牌')
try:
 for choice in ((0,'风险',next(d for d in hand if d in (4,5))),(1,'风险',6),(1,'稳健',6),('结束回合',None,None)):
  o=s.observation;op=operation(o,choice);check_step(variant,o,op,s.act(op,'边界核对：未完成清零、完成保留。'))
 ca=clocks(s.observation);assert ca['目标A']['Current']==0 and ca['目标B']['Current']==3
 s.save(OUT/'边界-成果保留.json.gz')
finally:s.close()
# 边界2：空过两轮超时，时间税每次收取。
s=H['Session']('研究·整段完成',1301)
try:
 for _ in range(2):
  o=s.observation;op=operation(o,('结束回合',None,None));check_step('整段完成',o,op,s.act(op,'边界核对：两回合到期与时间税。'))
 assert s.observation['EncounterResult']=="('timeout 0 0 2 3)"
 s.save(OUT/'边界-两轮到期.json.gz')
finally:s.close()
# 边界3：必需目标先完成，B的操作随交锋立即关闭。
for seed in range(1302,1401):
 s=H['Session']('研究·保留终点',seed)
 if 6 in state(s.observation)[4]:break
 s.close()
try:
 o=s.observation;op=operation(o,(0,'风险',6));check_step('保留终点',o,op,s.act(op,'边界核对：完成A立即结束，B未完成。'))
 assert s.observation['EncounterResult']=="('success 2 0 1 5)"
 s.save(OUT/'边界-终点关闭.json.gz')
finally:s.close()

selected=[];seen=set()
for row in rows:
 key=(row['variant'],row['policy'],row['success'],row['left_injury_free_model'],row['bonus'])
 if key not in seen:seen.add(key);selected.append(OUT/row['path'])
selected.extend(sorted(OUT.glob('边界-*.json.gz')))
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r07-replay-') as tmp:
  raw=Path(tmp)/'record.json';raw.write_bytes(gzip.decompress(path.read_bytes()))
  r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(raw)],cwd=ROOT,capture_output=True,text=True)
  assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,selected))
paths=list(OUT.glob('*.json.gz'));all_steps=sum(len(json.loads(gzip.decompress(p.read_bytes()))['Steps']) for p in paths)
verification=dict(native_games=len(paths),checked_steps=all_steps,strict_replays=replays,
                  unity_visual_checked=False,summary=summary)
(OUT/'验证.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2)+'\n')
files=[ROOT/'UnityClient/Assets/Resources/Content/scripts/research/顺序选择.scm']+list((ROOT/'UnityClient/Assets/Resources/Content/scenes/encounters').glob('研究·*段*.scm'))+[ROOT/'UnityClient/Assets/Resources/Content/scenes/encounters/研究·保留终点.scm']
version=dict(ContentRevision=rows[0]['revision'],source_sha256={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
             model_files_sha256={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (HERE/'筛选.py',HERE/'分段筛选.py',Path(__file__))})
(HERE/'版本.json').write_text(json.dumps(version,ensure_ascii=False,indent=2)+'\n')
print('正式会话',len(paths),'局；核对',all_steps,'步；严格回放',len(replays),'局通过。',flush=True)
