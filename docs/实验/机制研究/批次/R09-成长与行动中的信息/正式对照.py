"""冻结R09：策略仅读公开状态；验证固定真值、揭示、收益、终止并严格回放。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,re,gzip,hashlib,subprocess,tempfile
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
K=runpy.run_path(str(HERE/'行动信息.py'))
OUT=HERE/'正式对照'
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def state(o):
 clocks={c['Label']:c for c in o['Clocks']};p=player(o);a,b=clocks['路程A'],clocks['路程B']
 return a['Current'],a['Max'] if a['Max']>1 else -1,b['Current'],b['Max'] if b['Max']>1 else -1,p['Composure'],tuple(sorted(d['Value'] for d in p['Dice']))
def operation(o,ch):
 i,act,d=ch;name=('稳做' if act=='稳健' else '快做')+('A' if i==0 else 'B')
 return next(op for op in o['Operations'] if op['Card']==name and op['DieValue']==d)
def result(o):
 match=re.fullmatch(r"\('(?P<status>success|timeout|collapse) (?P<a>\d+) (?P<la>\d+) (?P<ka>True|False) (?P<b>\d+) (?P<lb>\d+) (?P<kb>True|False) (?P<c>\d+)\)",o['EncounterResult'])
 assert match,o['EncounterResult']
 return {k:int(v) if k in ('a','la','b','lb','c') else v=='True' if k in ('ka','kb') else v for k,v in match.groupdict().items()}
def check_record(path,known=False,growth=1):
 raw=json.loads(gzip.decompress(path.read_bytes()));final=result(raw['Steps'][-1]['After']);lens=final['la'],final['lb'];assert all(x in (3,6) for x in lens)
 expected=[0,lens[0] if known else -1,0,lens[1] if known else -1,5]
 disclosure=0
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];s=state(before)
  assert s[:5]==tuple(expected),(path,s,expected)
  op=next(x for x in before['Operations'] if x['Id']==step['OperationId'])
  if op['Kind']=='end-turn':
   expected[4]-=1;assert final['status']=='timeout';assert after['EncounterResult'] is not None
  else:
   probs=H['odds'](op['Odds']);official=K['K']['probabilities'](op['DieValue'],growth)
   assert all(abs(probs[k]-v)<1e-12 for k,v in zip(('坏','中','好'),official))
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'))
   j={'Fail':0,'Neutral':1,'Success':2}[grade];risk=op['Card'].startswith('快做');i=0 if op['Card'].endswith('A') else 1
   gain=((0,1,2) if risk else (0,0,1))[j];cost=((1,1,0) if risk else (1,0,0))[j]
   expected[i*2]=min(lens[i],expected[i*2]+gain);expected[4]-=cost
   if expected[i*2]>=1 and expected[i*2+1]<0:
    expected[i*2+1]=lens[i];disclosure+=1
    assert any('已摸清' in e['Text'] for e in step['Events'])
   if after['EncounterResult'] is None:
    assert state(after)[:5]==tuple(expected)
    rest=list(s[-1]);rest.remove(op['DieValue']);assert state(after)[-1]==tuple(rest)
   else:
    assert final['status']=='success';assert expected[0]>=lens[0] or expected[2]>=lens[1]
    assert not after['IsInEncounter']
  assert player(after)['Composure']==expected[4]
  assert int(re.search(r'(\d+)/7',after['Injury'])[1])==0
 assert (final['a'],final['b'],final['c'])==(expected[0],expected[2],expected[4])
 assert (final['ka'],final['kb'])==(expected[1]>0,expected[3]>0)
 return dict(path=path.name,content_revision=raw['ContentRevision'],steps=len(raw['Steps']),status=final['status'],disclosures=disclosure,lens=lens)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r09-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()))
  r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True)
  assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 assert not OUT.exists(),OUT;OUT.mkdir();models={};jobs=[]
 for label,cls,policy,known,seeds in [
  ('求解',K['Model'],'求解',False,range(1601,1613)),
  ('固定A',K['Model'],'固定A',False,range(1601,1613)),
  ('遇长必换',K['Model'],'遇长必换',False,range(1601,1613)),
  ('滚动比较',K['Rolling'],'求解',False,range(1601,1613)),
  ('明示求解',K['Model'],'求解',True,range(1613,1621))]:
  m=cls(reveal=1,policy=policy,known=known);m.summary();models[label]=m
  jobs.extend((label,known,seed) for seed in seeds)
 def play(job):
  label,known,seed=job;s=H['Session']('研究·路线明示对照' if known else '研究·边做边看',seed)
  try:
   initial=state(s.observation);switches=0;last=None
   for _ in range(5):
    o=s.observation
    if o['EncounterResult'] is not None:break
    st=state(o)
    if st[-1]:
     m=models[label];m.value(st);ch=m.decisions[st]
     if last is not None and last!=ch[0]:switches+=1
     last=ch[0];op=operation(o,ch)
    else:op=next(x for x in o['Operations'] if x['Kind']=='end-turn')
    s.act(op,'R09冻结后新种子：依据公开路长、当前进度、冷静与骰子选择。')
   assert s.observation['EncounterResult'] is not None
   p=OUT/f'{label}-{seed}.json.gz';s.save(p)
   row=check_record(p,known);row.update(policy=label,seed=seed,switches=switches,initial=initial)
   return row
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(play,jobs))
 # 空过一回合：时间税和到期路径。固定种子不要求造出好手牌。
 s=H['Session']('研究·边做边看',1621)
 try:
  op=next(x for x in s.observation['Operations'] if x['Kind']=='end-turn');s.act(op,'边界核对：一回合到期与时间税。')
  p=OUT/'边界-空过.json.gz';s.save(p);rows.append(check_record(p))
 finally:s.close()
 # 成长概率只收集原生公开观察；不称为完整试玩样本。
 calibration=[]
 for g in range(1,5):
  seen=set()
  for seed in range(1701,1765):
   s=H['Session']('研究·整段完成',seed,g)
   try:
    evidence=[]
    for op in s.observation['Operations']:
     if op['Card']!='快做A':continue
     d=op['DieValue'];v=H['odds'](op['Odds']);expected=K['K']['probabilities'](d,g)
     assert all(abs(v[k]-x)<1e-12 for k,x in zip(('坏','中','好'),expected))
     seen.add(d);evidence.append({k:op[k] for k in ('DieValue','Prepared','Odds')})
    calibration.append(dict(growth=g,seed=seed,operations=evidence))
   finally:s.close()
   if len(seen)==6:break
  assert len(seen)==6,(g,seen)
 (OUT/'公开概率校准.json').write_text(json.dumps(dict(starts=len(calibration),observations=calibration),ensure_ascii=False,indent=2)+'\n')
 # 每一轨迹完整严格回放；数量小，省去分组选择的额外复杂度。
 paths=sorted(OUT.glob('*.json.gz'))
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 revisions={r['content_revision'] for r in rows};assert len(revisions)==1,revisions
 summary={p:dict(games=sum(r.get('policy')==p for r in rows),success=sum(r.get('policy')==p and r['status']=='success' for r in rows),changed_route_games=sum(r.get('policy')==p and r.get('switches',0)>0 for r in rows)) for p in models}
 (OUT/'结果.json').write_text(json.dumps(dict(results=rows,summary=summary,scope='冻结后新种子小样本；用于正式行为核对，不能估计全局策略差距'),ensure_ascii=False,indent=2)+'\n')
 (OUT/'验证.json').write_text(json.dumps(dict(native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),strict_replays=replays,content_revision=next(iter(revisions)),calibration_starts=len(calibration),unity_visual_checked=False,human_games=0),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(summary=summary,games=len(rows),steps=sum(r['steps'] for r in rows),replays=len(replays),calibration_starts=len(calibration)),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
