"""冻结后的公开状态正式实验；全程调整与不可重分配的初始分区。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,json,re,gzip,subprocess,tempfile,hashlib
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
K=runpy.run_path(str(HERE/'分配模型.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def clocks(o):return {c['Label']:c for c in o['Clocks']}
def state(o):
 c=clocks(o);return (3-c['工作甲']['Current'],3-c['工作乙']['Current'],tuple(sorted(d['Value'] for d in player(o)['Dice'])))
def result(o):
 m=re.fullmatch(r"\('(?P<status>success|timeout|collapse) (?P<a>\d+) (?P<b>\d+) (?P<cold>\d+)\)",o['EncounterResult']);assert m,o['EncounterResult'];return {k:v if k=='status' else int(v) for k,v in m.groupdict().items()}
def operation(o,ch):
 route,act,d=ch
 if route=='结束回合':return next(op for op in o['Operations'] if op['Kind']=='end-turn')
 name=('甲' if route==0 else '乙')+('稳做' if act=='稳健' else '快做')
 return next(op for op in o['Operations'] if op['Card']==name and op['DieValue']==d)
def single_choice(model,r,h,g):
 values=[((0.,0.),('结束回合',None))]
 for d in sorted(set(h)):
  rest=list(h);rest.remove(d);rest=tuple(rest)
  for act in K['PAY']:
   p=harm=0.
   for w,(gain,cost) in zip(K['ODDS'](d,g),K['PAY'][act]):
    pp,hh=model.single(max(0,r-gain),rest,g);p+=w*pp;harm+=w*(hh+cost*pp)
   values.append(((p,harm),(act,d)))
 return max(values,key=lambda x:K['key'](x[0]))[1]
def check(path,growth=1,bonus=1):
 raw=json.loads(gzip.decompress(path.read_bytes()));grades=set();counterstates=[]
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];cb=clocks(before);aa,bb=cb['工作甲']['Current'],cb['工作乙']['Current'];pc=player(before)['Composure']
  op=next(x for x in before['Operations'] if x['Id']==step['OperationId']);ended=after['EncounterResult'] is not None
  if op['Kind']=='end-turn':cost=1;assert ended and result(after)['status']=='timeout'
  else:
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));grades.add(grade);j={'Fail':0,'Neutral':1,'Success':2}[grade]
   act='稳健' if op['Card'].endswith('稳做') else '风险';gain,cost=K['PAY'][act][j];route=0 if op['Card'].startswith('甲') else 1
   actual=H['odds'](op['Odds']);expected=K['ODDS'](op['DieValue'],growth+(bonus if route else 0));assert all(abs(actual[k]-v)<1e-12 for k,v in zip(('坏','中','好'),expected)),(path,op)
   if route==0:aa=min(3,aa+gain)
   else:bb=min(3,bb+gain)
   assert ended==(aa==bb==3),(path,op,after)
   if ended:assert result(after)['status']=='success'
   if state(before)[:2]==(1,2) and state(before)[2]==(2,5):counterstates.append(dict(card=op['Card'],die=op['DieValue'],cold=pc))
  assert player(after)['Composure']==pc-cost and pc-cost>=0
  assert int(re.search(r'(\d+)/7',before['Injury'])[1])==0 and int(re.search(r'(\d+)/7',after['Injury'])[1])==0
  if ended:
   f=result(after);assert (f['a'],f['b'],f['cold'])==(aa,bb,pc-cost);assert not after['IsInEncounter']
  else:
   ca=clocks(after);assert (ca['工作甲']['Current'],ca['工作乙']['Current'])==(aa,bb)
   rest=[d['Value'] for d in player(before)['Dice']];rest.remove(op['DieValue']);assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
   cards=[x['Card'] for x in after['Operations'] if x['Kind']=='action']
   if aa==3:assert all(not x.startswith('甲') for x in cards)
   if bb==3:assert all(not x.startswith('乙') for x in cards)
 assert raw['Steps'][-1]['After']['EncounterResult'] is not None
 return dict(path=path.name,growth=growth,bonus=bonus,revision=raw['ContentRevision'],steps=len(raw['Steps']),result=result(raw['Steps'][-1]['After']),grades=sorted(grades),counterstates=counterstates)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r15-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 assert not OUT.exists(),OUT;OUT.mkdir();models={};jobs=[]
 for name,policy,g,bonus,seeds in [('全程调整','全过程调整',1,1,range(4201,4213)),('最优初始分区','最优分区',1,1,range(4201,4213)),('高骰弱项','高骰弱项',1,1,range(4201,4209)),('同等判定','全过程调整',1,0,range(4213,4219)),('成长2','全过程调整',2,1,range(4221,4225))]:
  m=K['Model'](skills=(g,g+bonus));m.summary();models[name]=m;jobs.extend((name,policy,g,bonus,seed) for seed in seeds)
 def play(job):
  name,policy,g,bonus,seed=job;m=models[name];s=H['Session']('研究·按需用骰' if bonus else '研究·同等判定对照',seed,g)
  groups=None
  if policy!='全过程调整':groups=[list(h) for h in m.partitions(state(s.observation)[2],policy)[1]]
  try:
   for _ in range(8):
    o=s.observation
    if o['EncounterResult'] is not None:break
    st=state(o)
    if groups is None:m.value(*st);choice=m.decisions[st]
    else:
     route=0 if st[0]>0 else 1
     act,d=single_choice(m,st[route],tuple(groups[route]),m.skills[route])
     choice=('结束回合',None,None) if act=='结束回合' else (route,act,d)
     if act!='结束回合':groups[route].remove(d)
    op=operation(o,choice);s.act(op,'R15冻结条件新种子：公开手牌与剩余目标；固定分区只在开局分配，组内根据结果优化。')
   assert s.observation['EncounterResult'] is not None
   p=OUT/f'{name}-{seed}.json.gz';s.save(p);row=check(p,g,bonus);row.update(policy=name,seed=seed);return row
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(play,jobs))
 for bonus,seed in ((1,4230),(0,4231)):
  s=H['Session']('研究·按需用骰' if bonus else '研究·同等判定对照',seed)
  try:
   s.act(operation(s.observation,('结束回合',None,None)),'边界：单回合空过立即到期。')
   assert s.observation['EncounterResult']=="('timeout 0 0 4)"
   p=OUT/f'边界-空过-{bonus}.json.gz';s.save(p);rows.append(check(p,1,bonus))
  finally:s.close()
 paths=sorted(OUT.glob('*.json.gz'))
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 revisions={r['revision'] for r in rows};assert len(revisions)==1
 summary={name:dict(games=sum(r.get('policy')==name for r in rows),success=sum(r.get('policy')==name and r['result']['status']=='success' for r in rows)) for name in models}
 (OUT/'结果.json').write_text(json.dumps(dict(results=rows,summary=summary,scope='有限正式行为样本，不用种子估计策略概率排名；角色均健康，无恢复品'),ensure_ascii=False,indent=2)+'\n')
 (OUT/'验证.json').write_text(json.dumps(dict(native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),strict_replays=replays,ContentRevision=next(iter(revisions)),human_games=0,unity_visual_checked=False,covered_grades=sorted(set(g for r in rows for g in r['grades']))),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(games=len(rows),steps=sum(r['steps'] for r in rows),replays=len(replays),summary=summary,counterstate_visits=sum(len(r['counterstates']) for r in rows)),ensure_ascii=False))
if __name__=='__main__':main()
