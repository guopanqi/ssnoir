"""冻结后的正式转移检查。首伤后使用公开启发式，并单独标记模型外路径。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,re,gzip,subprocess,tempfile,hashlib
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists());HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'));K=runpy.run_path(str(HERE/'组合模型.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def injury(o):return int(re.search(r'(\d+)/7',o['Injury'])[1])
def clocks(o):return {c['Label']:c for c in o['Clocks']}
def state(o):
 c=clocks(o);return 5-c['工作甲']['Current'],3-c['工作乙']['Current'],player(o)['Composure'],3-c['已过回合']['Current'],tuple(sorted(d['Value'] for d in player(o)['Dice']))
def result(o):
 m=re.fullmatch(r"\('(?P<status>success|timeout|collapse) (?P<a>\d+) (?P<b>\d+) (?P<round>\d+) (?P<cold>\d+)\)",o['EncounterResult']);assert m,o['EncounterResult']
 return {k:v if k=='status' else int(v) for k,v in m.groupdict().items()}
def operation(o,ch):
 route,act,d=ch
 if route=='结束回合':return next(x for x in o['Operations'] if x['Kind']=='end-turn')
 card='整顿' if route=='整顿' else route+act
 return next(x for x in o['Operations'] if x['Card']==card and x['DieValue']==d)
def fallback(o):
 actions=[x for x in o['Operations'] if x['Kind']=='action']
 if not actions:return operation(o,('结束回合',None,None))
 a,b,c,t,h=state(o)
 def score(op):
  card=op['Card'];act='整顿' if card=='整顿' else card[1:];r=0 if act=='整顿' else a if card[0]=='甲' else b
  return sum(p*(min(r,gain)+(1.2 if c<=1 else .15)*max(-c,min(5-c,dc))) for p,(gain,dc) in zip(H['odds'](op['Odds']).values(),K['PAY'][act]))
 return max(actions,key=score)
def check(path,reset,growth):
 raw=json.loads(gzip.decompress(path.read_bytes()));covered=set();clears=retained=preserved=cap=early=0;outside=False;interactions=[]
 witnesses=json.loads((HERE/'交互结果.json').read_text())['witnesses'];index={tuple(x['state'][:-1])+(tuple(x['state'][-1]),):x for x in witnesses}
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];cb=clocks(before);pa=cb['工作甲']['Current'];pb=cb['工作乙']['Current'];elapsed=cb['已过回合']['Current'];cold=player(before)['Composure']
  op=next(x for x in before['Operations'] if x['Id']==step['OperationId']);ended=after['EncounterResult'] is not None;collapsed=ended and result(after)['status']=='collapse'
  if op['Kind']=='end-turn':
   dc=-1
   if player(before)['Dice']:early+=1
   if not collapsed:
    elapsed+=1
    if reset:
     if 0<pa<5:clears+=1
     if 0<pb<3:clears+=1
     pa=pa if pa==5 else 0;pb=pb if pb==3 else 0
    else:preserved+=int(0<pa<5)+int(0<pb<3)
    retained+=int(pa==5)+int(pb==3)
  else:
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));covered.add(grade);j={'Fail':0,'Neutral':1,'Success':2}[grade]
   act='整顿' if op['Card']=='整顿' else op['Card'][1:];gain,dc=K['PAY'][act][j]
   public=H['odds'](op['Odds']);assert public[('坏','中','好')[j]]>0,(path,op,grade)
   if injury(before)==0:
    expect=K['ODDS'](op['DieValue'],growth);assert all(abs(public[label]-v)<1e-12 for label,v in zip(('坏','中','好'),expect))
   if not collapsed:
    if op['Card'].startswith('甲'):pa=min(5,pa+gain)
    elif op['Card'].startswith('乙'):pb=min(3,pb+gain)
   if act=='整顿' and dc==2 and cold==4:cap+=1
   assert ended==((pa==5 and pb==3) or collapsed),(path,op,pa,pb,after['EncounterResult'])
   st=state(before)
   if growth==1 and injury(before)==0 and st in index:
    witness=index[st];chosen=witness['primary_choice' if reset else 'control_choice'];expected_card=None if chosen[0]=='结束回合' else '整顿' if chosen[0]=='整顿' else chosen[0]+chosen[1]
    if op['Card']==expected_card and op['DieValue']==chosen[2]:interactions.append(dict(state=st,card=op['Card'],die=op['DieValue'],reset=reset))
  assert player(after)['Composure']==min(5,max(0,cold+dc)),(path,cold,dc,player(after))
  if dc<0 and not collapsed:assert injury(after)>=injury(before)
  outside|=injury(before)>0 or injury(after)>0 or collapsed
  if ended:
   f=result(after);assert (f['a'],f['b'],f['cold'],f['round'])==(pa,pb,player(after)['Composure'],min(3,elapsed+1));assert not after['IsInEncounter']
   if f['status']=='success':assert pa==5 and pb==3
   if f['status']=='timeout':assert elapsed==3 and (pa<5 or pb<3)
  else:
   ca=clocks(after);assert (ca['工作甲']['Current'],ca['工作乙']['Current'],ca['已过回合']['Current'])==(pa,pb,elapsed)
   if op['Kind']=='action':
    rest=[d['Value'] for d in player(before)['Dice']];rest.remove(op['DieValue']);assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
   cards=[x['Card'] for x in after['Operations'] if x['Kind']=='action']
   assert ('整顿' in cards)==(player(after)['Composure']<5 and bool(player(after)['Dice']))
   if pa==5:assert all(not x.startswith('甲') for x in cards)
   if pb==3:assert all(not x.startswith('乙') for x in cards)
 assert raw['Steps'][-1]['After']['EncounterResult'] is not None
 return dict(path=path.name,revision=raw['ContentRevision'],reset=reset,growth=growth,steps=len(raw['Steps']),result=result(raw['Steps'][-1]['After']),left_injury_free_model=outside,grades=sorted(covered),cleared_partial_events=clears,retained_finished_events=retained,preserved_partial_events=preserved,healing_cap_events=cap,early_end_events=early,interaction_visits=interactions)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r19-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def scene(reset):return '研究·整段留力' if reset else '研究·进度保留对照'
def finish(s,m):
 for _ in range(18):
  o=s.observation
  if o['EncounterResult'] is not None:return
  if injury(o)>0:op=fallback(o)
  else:
   st=state(o);m.value(*st);op=operation(o,m.decisions[st])
  s.act(op,'R19冻结首试：只读公开目标、冷静、回合与剩骰；首伤后使用模型外公开启发式。')
 assert s.observation['EncounterResult'] is not None

def main():
 OUT.mkdir(exist_ok=True);rows=[];jobs=[];models={}
 for name,reset,g,policy,seeds in [('整段自由',True,1,'自由',range(6101,6113)),('保留自由',False,1,'自由',range(6113,6121)),('每轮先整顿',True,1,'每轮先整顿',range(6121,6129)),('成长2',True,2,'自由',range(6131,6135))]:
  m=K['Model'](reset,policy,g=g);m.summary();models[name]=m;jobs.extend((name,reset,g,seed) for seed in seeds)
 def play(job):
  name,reset,g,seed=job;p=OUT/f'{name}-{seed}.json.gz'
  if p.exists():
   r=check(p,reset,g);r.update(policy=name,seed=seed);return r
  s=H['Session'](scene(reset),seed,g)
  try:
   finish(s,models[name]);p=OUT/f'{name}-{seed}.json.gz';s.save(p);r=check(p,reset,g);r.update(policy=name,seed=seed);return r
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows.extend(pool.map(play,jobs))
 starts=0
 for reset in (True,False):
  s=H['Session'](scene(reset),6200)
  try:
   for _ in range(3):s.act(operation(s.observation,('结束回合',None,None)),'边界：三轮空过，仅花回合税。')
   assert s.observation['EncounterResult']=="('timeout 0 0 3 0)"
   p=OUT/f'边界-空过-{reset}.json.gz';s.save(p);rows.append(check(p,reset,1))
  finally:s.close()
  for seed in range(6201,6251):
   s=H['Session'](scene(reset),seed);starts+=1
   try:
    if 6 not in state(s.observation)[-1]:continue
    s.act(operation(s.observation,('甲','快做',6)),'边界：保证推进2格后主动换回合。')
    s.act(operation(s.observation,('结束回合',None,None)),'边界：清零/保留只改变进度，冷静同样从3降至2。')
    assert clocks(s.observation)['工作甲']['Current']==(0 if reset else 2)
    finish(s,K['Model'](reset));p=OUT/f'边界-半成品-{reset}-{seed}.json.gz';s.save(p);rows.append(check(p,reset,1));break
   finally:s.close()
  else:raise AssertionError('半成品覆盖未找到')
 for seed in range(6251,6301):
  s=H['Session'](scene(True),seed);starts+=1
  try:
   if 6 not in state(s.observation)[-1]:continue
   s.act(operation(s.observation,('整顿','整顿',6)),'边界：恢复2从3达到5。')
   assert player(s.observation)['Composure']==5
   s.act(operation(s.observation,('结束回合',None,None)),'边界：结束税使冷静降为4。')
   if 6 not in state(s.observation)[-1]:continue
   s.act(operation(s.observation,('整顿','整顿',6)),'边界：恢复2实际只增加1，到上限5。')
   assert player(s.observation)['Composure']==5
   finish(s,K['Model']());p=OUT/f'边界-恢复封顶-{seed}.json.gz';s.save(p);rows.append(check(p,True,1));break
  finally:s.close()
 else:raise AssertionError('恢复封顶覆盖未找到')
 paths=sorted(OUT.glob('*.json.gz'))
 assert {p.name for p in paths}=={r['path'] for r in rows},'已有额外轨迹时应复核对应覆盖脚本，不得覆盖元数据'
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 covered=set(g for r in rows for g in r['grades']);assert covered=={'Fail','Neutral','Success'}
 assert all(sum(r[k] for r in rows)>0 for k in ('cleared_partial_events','retained_finished_events','preserved_partial_events','healing_cap_events'))
 revisions={r['revision'] for r in rows};assert len(revisions)==1
 summary={name:dict(games=sum(r.get('policy')==name for r in rows),actual_success=sum(r.get('policy')==name and r['result']['status']=='success' for r in rows),left_model=sum(r.get('policy')==name and r['left_injury_free_model'] for r in rows)) for name in models}
 (OUT/'结果.json').write_text(json.dumps(dict(results=rows,summary=summary,scope='有限正式行为样本，不用样本排策略胜率；首伤后启发式，不与无伤模型混算'),ensure_ascii=False,indent=2)+'\n')
 (OUT/'验证.json').write_text(json.dumps(dict(native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),ContentRevision=next(iter(revisions)),strict_replays=replays,covered_grades=sorted(covered),boundary_search_starts=starts,interaction_visits=sum(len(r['interaction_visits']) for r in rows),human_games=0,unity_visual_checked=False),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(games=len(rows),steps=sum(r['steps'] for r in rows),replays=len(replays),summary=summary,interaction_visits=sum(len(r['interaction_visits']) for r in rows)),ensure_ascii=False))
if __name__=='__main__':main()
