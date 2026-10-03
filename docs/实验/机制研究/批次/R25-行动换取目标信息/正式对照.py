"""公开策略的正式转移校准与严格回放，不查询未定位时的目标真值。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,re,gzip,hashlib,tempfile,subprocess
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists());HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'));K=runpy.run_path(str(HERE/'信息投资.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def clocks(o):return {c['Label']:c for c in o['Clocks']}
def state(o):
 cs=clocks(o);note=next(c['Text'] for c in o['Cards'] if c['Name']=='标注：目标信息');target=re.search(r'目标在([甲乙])处',note)
 return cs['搜寻甲']['Current'],cs['搜寻乙']['Current'],target[1] if target else None,player(o)['Composure'],tuple(sorted(d['Value'] for d in player(o)['Dice']))
def result(o):
 match=re.fullmatch(r"\('(?P<status>success|timeout|collapse) (?P<a>\d+) (?P<b>\d+) '(?P<target>[甲乙]) (?P<known>True|False) (?P<clue>\d+) (?P<cold>\d+)\)",o['EncounterResult']);assert match,o['EncounterResult']
 return {k:int(v) if k in ('a','b','clue','cold') else v=='True' if k=='known' else v for k,v in match.groupdict().items()}
def operation(o,ch):
 r,act,d=ch
 if r=='结束回合':return next(x for x in o['Operations'] if x['Kind']=='end-turn')
 card='探查' if r=='探查' else r+act
 return next(x for x in o['Operations'] if x['Card']==card and x['DieValue']==d)
def finish(s,m):
 for _ in range(6):
  o=s.observation
  if o['EncounterResult'] is not None:return
  st=state(o);m.value(*st);s.act(operation(o,m.decisions[st]),'R25冻结策略：只读搜索进度、公开定位信息、冷静与骰；未定位时不读目标真值。')
 assert s.observation['EncounterResult'] is not None

def check(path,allowed,g):
 raw=json.loads(gzip.decompress(path.read_bytes()));final=result(raw['Steps'][-1]['After']);truth=final['target'];clue=0;grades=set();empty=repeat=direct=cap=located=0;pending=False
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];a,b,target,c,h=state(before);op=next(x for x in before['Operations'] if x['Id']==step['OperationId']);ended=after['EncounterResult'] is not None
  assert target is None or target==truth
  if pending and op['Kind']=='action':repeat+=int(op['Card']=='探查');direct+=int(op['Card']!='探查')
  if op['Kind']=='end-turn':dc=-1;assert ended;assert result(after)['status']=='timeout';pending=False
  else:
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));grades.add(grade);j={'Fail':0,'Neutral':1,'Success':2}[grade]
   act='稳做' if op['Card']=='探查' else op['Card'][1:];gain,dc=K['PAY'][act][j]
   public=H['odds'](op['Odds']);assert all(abs(public[k]-p)<1e-12 for k,p in zip(('坏','中','好'),K['ODDS'](op['DieValue'],g)))
   assert op['Prepared']==op['DieValue']+g
   pending=op['Card']=='探查' and j!=2
   if op['Card']=='探查':
    assert allowed and target is None
    if j==2:clue=1;target=truth;located+=1
   else:
    route=op['Card'][0];old=a if route=='甲' else b
    if route=='甲':a=min(3,a+gain)
    else:b=min(3,b+gain)
    cap+=int(gain>3-old)
    if (a if route=='甲' else b)==3 and route!=truth:target=truth;empty+=1
   won=(a if truth=='甲' else b)==3
   assert ended==won,(path,op,a,b,truth,after['EncounterResult'])
  assert player(after)['Composure']==c+dc
  assert re.search(r'(\d+)/7',after['Injury'])[1]=='0'
  if ended:
   f=result(after);assert (f['a'],f['b'],f['target'],f['known'],f['clue'],f['cold'])==(a,b,truth,target is not None,clue,c+dc)
   assert not after['IsInEncounter']
   if f['status']=='success':assert (a if truth=='甲' else b)==3
  else:
   assert state(after)[:4]==(a,b,target,c+dc)
   rest=list(h);rest.remove(op['DieValue']);assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
   cards=[x['Card'] for x in after['Operations'] if x['Kind']=='action']
   assert ('探查' in cards)==(allowed and target is None and bool(rest))
   if target is not None:assert all(card=='探查' or card[0]==target for card in cards)
 return dict(path=path.name,revision=raw['ContentRevision'],steps=len(raw['Steps']),allowed=allowed,growth=g,result=final,grades=sorted(grades),empty_location=empty,repeated_after_unlocated=repeat,searched_after_unlocated=direct,clamped_progress=cap,probe_located=located)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r25-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 assert not (OUT/'结果.json').exists(),'已有完成元数据，不重复运行'
 OUT.mkdir(exist_ok=True);jobs=[('自由',True,1,seed) for seed in range(7101,7109)]+[('不探查',False,1,seed) for seed in range(7111,7115)]+[('自由',True,2,seed) for seed in range(7121,7125)]
 expected={f'{p}-g{g}-{seed}.json.gz' for p,allowed,g,seed in jobs}
 assert {p.name for p in OUT.glob('*.json.gz')}<=expected,'只有冻结队列中的原始轨迹才能恢复'
 def play(job):
  policy,allowed,g,seed=job;p=OUT/f'{policy}-g{g}-{seed}.json.gz'
  if p.exists():
   r=check(p,allowed,g);r.update(seed=seed,policy=policy,cohort=True);return r
  s=H['Session']('研究·定位再搜' if allowed else '研究·直接搜寻对照',seed,g)
  try:
   finish(s,K['Model'](policy,g));p=OUT/f'{policy}-g{g}-{seed}.json.gz';s.save(p);r=check(p,allowed,g);r.update(seed=seed,policy=policy,cohort=True);return r
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(play,jobs))
 needed={'repeated_after_unlocated','searched_after_unlocated','empty_location'};missing={k for k in needed if sum(r[k] for r in rows)==0};attempts=[]
 for seed in range(7131,7161):
  if not missing:break
  s=H['Session']('研究·定位再搜',seed)
  try:
   if seed<7151:
    dice=state(s.observation)[-1];d=min(dice) if seed<7141 else max((v for v in dice if v<6),default=None)
    if d is not None:s.act(operation(s.observation,('探查','稳做',d)),'R25预先冻结定向覆盖：先探查指定公开骰，随后按公开信息自由判断。')
   finish(s,K['Model']('不探查' if seed>=7151 else '自由'))
   with tempfile.TemporaryDirectory(prefix='r25-cover-') as tmp:
    p=Path(tmp)/f'覆盖-{seed}.json.gz';s.save(p);r=check(p,True,1);hit=missing & {k for k in needed if r[k]>0}
    attempts.append(dict(seed=seed,completed=True,hits=sorted(hit),result=r['result']))
    if hit:(OUT/p.name).write_bytes(p.read_bytes());r.update(seed=seed,cohort=False);rows.append(r);missing-=hit
  finally:s.close()
 assert not missing,missing
 paths=sorted(OUT.glob('*.json.gz'))
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 revisions={r['revision'] for r in rows};assert len(revisions)==1
 grades={grade for r in rows for grade in r['grades']};assert grades=={'Fail','Neutral','Success'}
 out=dict(results=rows,coverage_attempts=attempts,scope='预定16局转移样本加必要缺口覆盖；不按少样本估计策略排名；定向搜索数另报')
 (OUT/'结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
 val=dict(saved_native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),strict_replays=replays,ContentRevision=next(iter(revisions)),covered_grades=sorted(grades),coverage_search_completed=len(attempts),boundary_counts={k:sum(r[k] for r in rows) for k in needed|{'clamped_progress','probe_located'}},human_games=0,unity_visual_checked=False)
 (OUT/'验证.json').write_text(json.dumps(val,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in val.items() if k!='strict_replays'},ensure_ascii=False))
if __name__=='__main__':main()
