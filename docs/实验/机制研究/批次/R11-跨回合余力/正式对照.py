"""R11冻结后公开策略：正常局模型、首伤后标记模型外，正式事件核对与回放。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,re,gzip,subprocess,tempfile,hashlib
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
K=runpy.run_path(str(HERE/'余力模型.py'));S=runpy.run_path(str(HERE/'强参照.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def injury(o):return int(re.search(r'(\d+)/7',o['Injury'])[1])
def clocks(o):return {c['Label']:c for c in o['Clocks']}
def state(o):
 c=clocks(o);p=player(o)
 return c['目标']['Max']-c['目标']['Current'],p['Composure'],c['已过回合']['Max']-c['已过回合']['Current'],tuple(sorted(d['Value'] for d in p['Dice']))
def result(o):
 m=re.fullmatch(r"\('(?P<status>success|timeout|collapse) (?P<progress>\d+) (?P<round>\d+) (?P<cold>\d+)\)",o['EncounterResult']);assert m,o['EncounterResult']
 return {k:v if k=='status' else int(v) for k,v in m.groupdict().items()}
def operation(o,choice):
 act,d=choice
 if act=='结束回合':return next(x for x in o['Operations'] if x['Kind']=='end-turn')
 return next(x for x in o['Operations'] if x['Card']==act and x['DieValue']==d)
def fallback(o,no_heal=False):
 ops=[x for x in o['Operations'] if x['Kind']=='action' and (not no_heal or x['Card']!='整顿')]
 if not ops:return operation(o,('结束回合',None))
 p=player(o);target=state(o)[0]
 def score(op):
  probs=H['odds'](op['Odds']);effects=K['PAY'][op['Card']] if op['Card']!='整顿' else ((0,-1),(0,0),(0,2))
  return sum(probs[k]*(min(target,gain)+(.6 if p['Composure']<=1 else .2)*max(-p['Composure'],min(p['MaxComposure']-p['Composure'],dc))) for k,(gain,dc) in zip(('坏','中','好'),effects))
 return max(ops,key=score)
def check(path,growth=1):
 raw=json.loads(gzip.decompress(path.read_bytes()));initial=player(raw['Steps'][0]['Before'])['Composure'];previous=0;heals=[];left=False
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];cb=clocks(before);pc=player(before)['Composure'];progress=cb['目标']['Current'];elapsed=cb['已过回合']['Current']
  op=next(x for x in before['Operations'] if x['Id']==step['OperationId'])
  collapsed=after['EncounterResult'] is not None and result(after)['status']=='collapse'
  if op['Kind']=='end-turn':dc=-1;elapsed+=0 if collapsed else 1
  else:
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));j={'Fail':0,'Neutral':1,'Success':2}[grade]
   effects=K['PAY'][op['Card']] if op['Card']!='整顿' else ((0,-1),(0,0),(0,2));gain,dc=effects[j]
   if op['Card']=='整顿':heals.append(dict(round=elapsed+1,die=op['DieValue'],before=pc,after=player(after)['Composure'],grade=grade))
   # 花冷静若直接触发住院，源码会跳过后续推进。
   if not collapsed:progress=min(10,progress+gain)
   if injury(before)==0:
    actual=H['odds'](op['Odds']);expected=K['ODDS'](op['DieValue'],growth)
    assert all(abs(actual[k]-v)<1e-12 for k,v in zip(('坏','中','好'),expected)),(path,op)
  assert player(after)['Composure']==min(player(before)['MaxComposure'],max(0,pc+dc)),(path,pc,dc,player(after))
  left|=injury(after)>0 or collapsed
  if after['EncounterResult'] is None:
   ca=clocks(after);assert (ca['目标']['Current'],ca['已过回合']['Current'])==(progress,elapsed),(path,ca,progress,elapsed)
   if op['Kind']=='action':
    rest=[d['Value'] for d in player(before)['Dice']];rest.remove(op['DieValue']);assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
   if player(after)['Composure']==5:assert not any(x['Card']=='整顿' for x in after['Operations'])
  else:
   final=result(after);assert final['progress']==progress;assert final['cold']==player(after)['Composure'];assert final['round']==min(3,elapsed+1)
   if final['status']=='success':assert progress==10
   if final['status']=='timeout':assert elapsed==3 and progress<10
   assert not after['IsInEncounter']
 final=result(raw['Steps'][-1]['After']);return dict(path=path.name,revision=raw['ContentRevision'],steps=len(raw['Steps']),result=final,initial_cold=initial,left_injury_free_model=left,injury_free_completion=final['status']=='success' and not left,heals=heals)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r11-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 assert not OUT.exists(),OUT;OUT.mkdir();models={};jobs=[]
 for name,cls,policy,k,cold,g,seeds in [
  ('求解',K['Model'],'求解',2,3,1,range(2201,2213)),
  ('只推进',K['Model'],'只推进',2,3,1,range(2201,2213)),
  ('滚动',S['Rolling'],'求解',2,3,1,range(2201,2213)),
  ('零冷静才整顿',K['Model'],'冷静阈值',0,3,1,range(2201,2209)),
  ('充足余力求解',K['Model'],'求解',2,5,1,range(2213,2221)),
  ('成长2求解',K['Model'],'求解',2,3,2,range(2221,2225))]:
  m=cls(policy=policy,threshold=k,cold=cold,growth=g,recovery=2);m.summary();models[name]=m;jobs.extend((name,cold,g,seed) for seed in seeds)
 def play(job):
  name,cold,g,seed=job;s=H['Session']('研究·留力推进' if cold==3 else '研究·充足余力对照',seed,g);left=False
  try:
   for _ in range(30):
    o=s.observation
    if o['EncounterResult'] is not None:break
    left|=injury(o)>0
    if not left:
     st=state(o);models[name].value(*st);op=operation(o,models[name].decisions[st])
    else:op=fallback(o,name=='只推进')
    s.act(op,'R11冻结后新种子：按公开目标距离、冷静、期限和手牌选择；首伤后明确使用模型外公开启发式。')
   assert s.observation['EncounterResult'] is not None
   p=OUT/f'{name}-{seed}.json.gz';s.save(p);r=check(p,g);r.update(policy=name,growth=g,seed=seed);return r
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(play,jobs))
 # 边界：三轮空过，初始3冷静只支付三次时间税。
 s=H['Session']('研究·留力推进',2300)
 try:
  for _ in range(3):s.act(operation(s.observation,('结束回合',None)),'边界：三轮到期与时间税。')
  assert s.observation['EncounterResult']=="('timeout 0 3 0)"
  p=OUT/'边界-三轮空过.json.gz';s.save(p);rows.append(check(p))
 finally:s.close()
 # 边界：先恢复至5、跨回合到4，再恢复2只实际增加1；种子搜索只用于覆盖边界。
 for seed in range(2301,2401):
  s=H['Session']('研究·留力推进',seed)
  try:
   if 6 not in state(s.observation)[-1]:continue
   s.act(operation(s.observation,('整顿',6)),'边界：恢复2到上限5。');assert player(s.observation)['Composure']==5
   s.act(operation(s.observation,('结束回合',None)),'边界：冷静跨回合5到4。')
   if 6 not in state(s.observation)[-1]:continue
   s.act(operation(s.observation,('整顿',6)),'边界：恢复2受上限截断，只增加1。');assert player(s.observation)['Composure']==5
   for _ in range(2):s.act(operation(s.observation,('结束回合',None)),'边界：关闭到期路径。')
   p=OUT/'边界-上限与延续.json.gz';s.save(p);rows.append(check(p));break
  finally:s.close()
 else:raise AssertionError('上限边界未覆盖')
 paths=sorted(OUT.glob('*.json.gz'))
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 revisions={r['revision'] for r in rows};assert len(revisions)==1,revisions
 summary={name:dict(games=sum(r.get('policy')==name for r in rows),success=sum(r.get('policy')==name and r['result']['status']=='success' for r in rows),injury_free_completion=sum(r.get('policy')==name and r['injury_free_completion'] for r in rows),model_out_games=sum(r.get('policy')==name and r['left_injury_free_model'] for r in rows)) for name in models}
 (OUT/'结果.json').write_text(json.dumps(dict(results=rows,summary=summary,scope='有限正式轨迹检验；首伤后为模型外公开策略；不得用小样本估计全球完成率'),ensure_ascii=False,indent=2)+'\n')
 (OUT/'验证.json').write_text(json.dumps(dict(native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),strict_replays=replays,ContentRevision=next(iter(revisions)),unity_visual_checked=False,human_games=0),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(summary=summary,games=len(rows),steps=sum(r['steps'] for r in rows),replays=len(replays)),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
