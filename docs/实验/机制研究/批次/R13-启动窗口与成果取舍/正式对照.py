"""只按公开Clock、手牌、Odds和冷静驱动正式会话，冻结后核对规则转移。"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import runpy,re,json,gzip,tempfile,subprocess,hashlib
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
K=runpy.run_path(str(HERE/'数学筛选.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def clocks(o):return {c['Label']:c for c in o['Clocks']}
def injury(o):return int(re.search(r'(\d+)/7',o['Injury'])[1])
def result(o,kind):
 expr=r"\('(?P<status>settled|collapse) (?P<small>\d+) (?P<large>\d+) (?P<score>\d+) (?P<cold>\d+)\)" if kind=='harvest' else r"\('(?P<status>success|timeout|window-expired|collapse) (?P<prep>\d+) (?P<payload>\d+) (?P<elapsed>\d+) (?P<cold>\d+)\)"
 m=re.fullmatch(expr,o['EncounterResult']);assert m,o['EncounterResult'];return {k:v if k=='status' else int(v) for k,v in m.groupdict().items()}
def state(o,kind,commit=-1):
 c=clocks(o);p=player(o);hand=tuple(sorted(d['Value'] for d in p['Dice']))
 if kind=='harvest':return (3-c['小成果']['Current'],6-c['大成果']['Current'],p['Composure'],hand,commit)
 return (3-c['前置']['Current'],4-c['后续']['Current'],p['Composure'],3-c['已过回合']['Current'],hand)
def endop(o):return next(op for op in o['Operations'] if op['Kind']=='end-turn')
def operation(o,kind,choice):
 if choice[0]=='结束回合':return endop(o)
 if kind=='harvest':route,act,d=choice;name=('小成果' if route==0 else '大成果')+('稳做' if act=='稳健' else '快做')
 else:
  act,d=choice;name=('前置' if clocks(o)['前置']['Current']<3 else '后续')+('稳做' if act=='稳健' else '快做')
 return next(op for op in o['Operations'] if op['Card']==name and op['DieValue']==d)
def fallback(o,kind):
 ops=[x for x in o['Operations'] if x['Kind']=='action']
 if not ops:return endop(o)
 return max(ops,key=lambda x:sum(H['odds'](x['Odds'])[k]*gain for k,(gain,cost) in zip(('坏','中','好'),K['PAY']['稳健' if x['Card'].endswith('稳做') else '风险'])))
def check(path,kind,growth=1,window=True):
 raw=json.loads(gzip.decompress(path.read_bytes()));outside=False;grades=set();opened=False;preheld=False;switched=False;last_route=None
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];cb=clocks(before);pc=player(before)['Composure'];op=next(x for x in before['Operations'] if x['Id']==step['OperationId'])
  ended=after['EncounterResult'] is not None;final=result(after,kind) if ended else None;collapsed=ended and final['status']=='collapse'
  if kind=='harvest':a,b=cb['小成果']['Current'],cb['大成果']['Current']
  else:p,q,t=cb['前置']['Current'],cb['后续']['Current'],cb['已过回合']['Current']
  if op['Kind']=='end-turn':
   dc=-1
   if kind=='window':
    preheld|=p>0 and p<3 and len(player(before)['Dice'])>0
    t+=0 if collapsed else 1
    expected_status='window-expired' if window and p==3 else 'timeout' if t==3 else None
    if not collapsed:assert ended==(expected_status is not None),(path,op,after)
    if expected_status and not collapsed:assert final['status']==expected_status
   else:assert ended and final['status']=='settled'
  else:
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));j={'Fail':0,'Neutral':1,'Success':2}[grade];grades.add(grade)
   act='稳健' if op['Card'].endswith('稳做') else '风险';gain,cost=K['PAY'][act][j];dc=-cost
   if injury(before)==0:
    actual=H['odds'](op['Odds']);assert all(abs(actual[k]-v)<1e-12 for k,v in zip(('坏','中','好'),K['ODDS'](op['DieValue'],growth)))
   if not collapsed:
    if kind=='harvest':
     route=0 if op['Card'].startswith('小成果') else 1
     if route==0:a=min(3,a+gain)
     else:b=min(6,b+gain)
     if last_route is not None and route!=last_route:switched=True
     last_route=route;assert not ended
    else:
     if p<3:
      assert op['Card'].startswith('前置');p=min(3,p+gain);opened|=p==3
     else:assert op['Card'].startswith('后续');q=min(4,q+gain)
     assert ended==(q==4),(path,op,after)
     if ended:assert final['status']=='success'
  assert player(after)['Composure']==max(0,pc+dc),(path,pc,dc,player(after))
  outside|=injury(after)>0 or collapsed
  if kind=='harvest':assert injury(before)==0 and injury(after)==0,'正式健康单人首轮不应受伤'
  if ended:
   assert not after['IsInEncounter'];assert final['cold']==player(after)['Composure']
   if kind=='harvest':assert (final['small'],final['large'],final['score'])==(a,b,(1 if a==3 else 0)+(2 if b==6 else 0))
   else:assert (final['prep'],final['payload'],final['elapsed'])==(p,q,t)
  else:
   ca=clocks(after)
   if kind=='harvest':assert (ca['小成果']['Current'],ca['大成果']['Current'])==(a,b)
   else:
    assert (ca['前置']['Current'],ca['后续']['Current'],ca['已过回合']['Current'])==(p,q,t)
    actions=[x['Card'] for x in after['Operations'] if x['Kind']=='action'];assert all(x.startswith('后续' if p==3 else '前置') for x in actions)
   if op['Kind']=='action':
    rest=[d['Value'] for d in player(before)['Dice']];rest.remove(op['DieValue']);assert sorted(rest)==sorted(d['Value'] for d in player(after)['Dice'])
 assert raw['Steps'][-1]['After']['EncounterResult'] is not None
 return dict(path=path.name,kind=kind,growth=growth,window=window,steps=len(raw['Steps']),revision=raw['ContentRevision'],result=result(raw['Steps'][-1]['After'],kind),model_out=outside,grades=sorted(grades),opened=opened,held_front=preheld,switched=switched)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r13-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 assert not OUT.exists(),OUT;OUT.mkdir();models={};jobs=[]
 for name,kind,policy,window,g,seeds in [
  ('开窗求解','window','求解',True,1,range(3101,3109)),('开窗用完','window','用完当手',True,1,range(3101,3109)),('无窗口','window','求解',False,1,range(3109,3113)),
  ('成果求解','harvest','求解',False,1,range(3201,3209)),('成果选定','harvest','开局选定',False,1,range(3201,3209)),('成果固定大','harvest','固定大',False,1,range(3209,3213)),
  ('成长2开窗','window','求解',True,2,range(3301,3303)),('成长2成果','harvest','求解',False,2,range(3303,3305))]:
  m=K['Window'](growth=g,policy=policy,window=window) if kind=='window' else K['Harvest'](growth=g,policy=policy);m.summary();models[name]=m;jobs.extend((name,kind,policy,window,g,seed) for seed in seeds)
 def play(job):
  name,kind,policy,window,g,seed=job;scene='研究·成果取舍' if kind=='harvest' else '研究·择时开窗' if window else '研究·无窗口对照'
  s=H['Session'](scene,seed,g);outside=False;commit=-1
  try:
   for _ in range(25):
    o=s.observation
    if o['EncounterResult'] is not None:break
    outside|=injury(o)>0
    if not outside:
     st=state(o,kind,commit);models[name].value(*st);choice=models[name].decisions[st];op=operation(o,kind,choice)
     if kind=='harvest' and policy=='开局选定' and commit<0 and choice[0]!='结束回合':commit=choice[0]
    else:op=fallback(o,kind)
    s.act(op,'R13冻结条件新种子：只读公开进度、冷静、回合与当前手牌；窗口首伤后标为模型外。')
   assert s.observation['EncounterResult'] is not None
   p=OUT/f'{name}-{seed}.json.gz';s.save(p);row=check(p,kind,g,window);row.update(policy=name,seed=seed);return row
  finally:s.close()
 with ThreadPoolExecutor(max_workers=4) as pool:rows=list(pool.map(play,jobs))
 # 独立边界：空过三回合，与成果单回合空结算。
 for scene,kind,seed,count,name in [('研究·择时开窗','window',3400,3,'边界-三轮空过'),('研究·成果取舍','harvest',3401,1,'边界-零成果')]:
  s=H['Session'](scene,seed)
  try:
   for _ in range(count):s.act(endop(s.observation),'边界：期限与空成果结算。')
   p=OUT/f'{name}.json.gz';s.save(p);rows.append(check(p,kind))
  finally:s.close()
 # 明确开启后立即结束，验证窗口到期，而不是一般三回合期限。
 s=H['Session']('研究·择时开窗',3402)
 try:
  for _ in range(20):
   o=s.observation
   if clocks(o)['前置']['Current']==3:break
   ops=[x for x in o['Operations'] if x['Kind']=='action' and x['Card']=='前置快做']
   s.act(max(ops,key=lambda x:x['DieValue']) if ops else endop(o),'边界：先开启窗口。')
  assert clocks(s.observation)['前置']['Current']==3
  s.act(endop(s.observation),'边界：已开启但后续未完成，结束回合应关闭窗口。')
  p=OUT/'边界-窗口关闭.json.gz';s.save(p);r=check(p,'window');assert r['result']['status']=='window-expired';rows.append(r)
 finally:s.close()
 paths=sorted(OUT.glob('*.json.gz'))
 with ThreadPoolExecutor(max_workers=4) as pool:replays=list(pool.map(replay,paths))
 revisions={r['revision'] for r in rows};assert len(revisions)==1
 summary={name:dict(games=sum(r.get('policy')==name for r in rows),results=[r['result'] for r in rows if r.get('policy')==name]) for name in models}
 (OUT/'结果.json').write_text(json.dumps(dict(results=rows,summary=summary,scope='冻结后有限原生转移核对；种子对照不作为概率排名，边界另列'),ensure_ascii=False,indent=2)+'\n')
 (OUT/'验证.json').write_text(json.dumps(dict(native_games=len(rows),checked_steps=sum(r['steps'] for r in rows),strict_replays=replays,ContentRevision=next(iter(revisions)),human_games=0,unity_visual_checked=False,covered_grades=sorted(set(g for r in rows for g in r['grades']))),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(games=len(rows),steps=sum(r['steps'] for r in rows),replays=len(replays),grades=sorted(set(g for r in rows for g in r['grades']))),ensure_ascii=False))
if __name__=='__main__':main()
