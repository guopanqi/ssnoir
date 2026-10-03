"""R37正式公开状态校准；只读Observation，不Eval私有变量、不读未来seed。"""
from pathlib import Path
import runpy,json,re,gzip,tempfile,subprocess,hashlib,gc
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
HERE=Path(__file__).resolve().parent;OUT=HERE/'正式对照'
H=runpy.run_path(str(ROOT/'tools/content-validator/experiments/mechanism-study.py'))
K=runpy.run_path(str(HERE.parent/'R36-资源余量与选择增量/压力模型.py'))
def player(o):return next(a for a in o['Actors'] if a['Id']=='player')
def health(o):
 k=int(re.search(r'(\d+)/7',o['Injury'])[1]);return k,int(1<=k<=3 and o['Injury'].startswith('头 '))
def state(o):
 cs={c['Label']:c for c in o['Clocks']};p=cs['主进度']['Current'];a=2-cs['修复甲']['Current'] if p>=3 else 0;b=2-cs['修复乙']['Current'] if p>=6 else 0
 k,q=health(o);return (p,a,b,cs['压力']['Current'],player(o)['Composure'],k,q,3-cs['已过回合']['Current'],tuple(sorted(d['Value'] for d in player(o)['Dice'])))
def result(o):
 match=re.fullmatch(r"\('(?P<status>success|timeout|pressure|collapse) (?P<p>\d+) (?P<a>\d+) (?P<b>\d+) (?P<s>\d+) (?P<elapsed>\d+) (?P<c>\d+)\)",o['EncounterResult']);assert match,o['EncounterResult']
 return {k:v if k=='status' else int(v) for k,v in match.groupdict().items()}
def operation(o,ch):
 r,act,d=ch
 if r=='结束回合':return next(x for x in o['Operations'] if x['Kind']=='end-turn')
 card=('推进','甲','乙')[r]+act
 return next(x for x in o['Operations'] if x['Kind']=='action' and x['Card']==card and x['DieValue']==d)
def finish(s,m,cover=False):
 for _ in range(16):
  o=s.observation
  if o['EncounterResult'] is not None:return
  st=state(o)
  if cover:
   p,a,b,pressure,c,k,q,t,h=st
   if h:
    r=1 if a else 2 if b else 0
    ch=(r,'快做',min(h))
   else:ch=('结束回合',None,None)
  else:
   m.value(*st);ch=m.decisions[st+(-1,)]
  s.act(operation(o,ch),'R37冻结公开策略：主进度、修复条、压力、回合、冷静、公开伤势与余骰；不读取脚本私有状态。')
 assert s.observation['EncounterResult'] is not None,'未在三回合合法步骤界限内结束'
def check(path,instant):
 raw=json.loads(gzip.decompress(path.read_bytes()));counts={k:0 for k in ('instant_bad','repair','repair_complete','end_pressure','pressure_failure','timeout','success','collapse','head_action','heavy_next_round','born_a','born_b','repaired_kept')};grades=set();finished_a=finished_b=False
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];p,a,b,s,c,k,q,t,h=state(before);op=next(x for x in before['Operations'] if x['Id']==step['OperationId']);ended=after['EncounterResult'] is not None
  aa,bb,pp,ss,elapsed=a,b,p,s,3-t;gain=0;grade=None
  if op['Kind']=='action':
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));grades.add(grade);j={'Fail':0,'Neutral':1,'Success':2}[grade]
   act=op['Card'][-2:];r=0 if op['Card'].startswith('推进') else 1 if op['Card'].startswith('甲') else 2
   gain,dc=K['PAY'][act][j];expected=K['ODDS'](op['DieValue'],1-q);public=H['odds'](op['Odds'])
   assert op['Prepared']==op['DieValue']+1-q and all(abs(public[label]-v)<1e-12 for label,v in zip(('坏','中','好'),expected))
   counts['head_action']+=q
  else:r=None;dc=-1
  possible=K['Model'].spend(c,k,q,-dc)
  f=result(after) if ended else None;collapsed=ended and f['status']=='collapse'
  if collapsed:
   assert any(nk>=7 for w,nc,nk,nq in possible);assert player(after)['Composure']==0
   nc=0;counts['collapse']+=1
  else:
   nc=player(after)['Composure'];nk,nq=health(after)
   assert any((nc,nk,nq)==(x,y,z) for w,x,y,z in possible),(path,op,before['Injury'],after['Injury'],c,nc)
   if op['Kind']=='action':
    if r==0:
     ss+=int(instant and j==0);counts['instant_bad']+=int(instant and j==0)
     if ss<5:
      pp=min(12,p+gain)
      if p<3<=pp:aa=2;counts['born_a']+=1
      if p<6<=pp:bb=2;counts['born_b']+=1
    else:
     if r==1:aa=max(0,a-gain)
     else:bb=max(0,b-gain)
     counts['repair']+=1;counts['repair_complete']+=int((aa if r==1 else bb)==0 and gain>0)
   else:
    ss=min(5,s+int(a>0)+int(b>0));counts['end_pressure']+=int(a>0 or b>0)
    if ss<5:elapsed+=1
   ss=min(5,ss)
  if finished_a:assert aa==0;counts['repaired_kept']+=1
  if finished_b:assert bb==0;counts['repaired_kept']+=1
  finished_a|=pp>=3 and aa==0;finished_b|=pp>=6 and bb==0
  status='collapse' if collapsed else 'pressure' if ss>=5 else 'success' if pp==12 else 'timeout' if elapsed==3 else None
  assert ended==(status is not None),(path,op,ss,elapsed,pp,after['EncounterResult'])
  if ended:
   assert (f['status'],f['p'],f['a'],f['b'],f['s'],f['elapsed'],f['c'])==(status,pp,aa,bb,ss,elapsed,nc),(path,f,(status,pp,aa,bb,ss,elapsed,nc))
   if not collapsed:counts[status if status!='pressure' else 'pressure_failure']+=1
   assert not after['IsInEncounter']
  else:
   assert state(after)[:5]==(pp,aa,bb,ss,nc) and state(after)[7]==3-elapsed
   cards={x['Card'] for x in after['Operations'] if x['Kind']=='action'}
   if op['Kind']=='action':
    tail=list(h);tail.remove(op['DieValue']);assert state(after)[8]==tuple(sorted(tail))
   else:
    assert len(state(after)[8])==(3 if nk>=4 else 4);counts['heavy_next_round']+=int(nk>=4)
   if state(after)[8]:
    assert ('甲快做' in cards)==(aa>0) and ('乙快做' in cards)==(bb>0)
 return dict(path=path.name,revision=raw['ContentRevision'],steps=len(raw['Steps']),result=result(raw['Steps'][-1]['After']),grades=sorted(grades),counts=counts)
def replay(path):
 with tempfile.TemporaryDirectory(prefix='r37-replay-') as tmp:
  p=Path(tmp)/'record.json';p.write_bytes(gzip.decompress(path.read_bytes()));r=subprocess.run(['dotnet',str(H['DLL']),'--replay',str(p)],cwd=ROOT,capture_output=True,text=True);assert r.returncode==0,(path,r.stdout,r.stderr)
 return dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),passed=True)
def main():
 OUT.mkdir(exist_ok=True);assert not (OUT/'结果.json').exists(),'已有完成元数据，勿重复样本'
 rows=[]
 for instant,obj,seeds in [(True,'early',range(8101,8109)),(False,'early',range(8111,8115)),(True,'completion',range(8121,8125))]:
  m=K['Model'](obj,'自由',instant)
  for seed in seeds:
   path=OUT/f'{obj}-{int(instant)}-{seed}.json.gz'
   if not path.exists():
    session=H['Session']('研究·压力预算' if instant else '研究·回合加压对照',seed)
    try:finish(session,m);session.save(path)
    finally:session.close()
   row=check(path,instant);row.update(seed=seed,instant=instant,objective=obj,cohort=True);rows.append(row)
  del m;gc.collect()
 needed={'instant_bad','repair_complete','pressure_failure','timeout','success','collapse','head_action','heavy_next_round','born_a','born_b','repaired_kept'}
 missing={key for key in needed if not any(row['counts'][key] for row in rows)};attempts=[]
 for seed in range(8131,8149):
  if not missing:break
  session=H['Session']('研究·压力预算',seed)
  try:
   finish(session,None,cover=True)
   with tempfile.TemporaryDirectory(prefix='r37-cover-') as tmp:
    path=Path(tmp)/f'覆盖-{seed}.json.gz';session.save(path);row=check(path,True);hits={key for key in missing if row['counts'][key]};attempts.append(dict(seed=seed,completed=True,hits=sorted(hits),result=row['result']))
    if hits:(OUT/path.name).write_bytes(path.read_bytes());row.update(seed=seed,instant=True,objective='覆盖最低骰风险修复优先',cohort=False);rows.append(row);missing-=hits
  finally:session.close()
 replays=[replay(path) for path in sorted(OUT.glob('*.json.gz'))];revisions={row['revision'] for row in rows};assert len(revisions)==1
 grades={g for row in rows for g in row['grades']};assert grades=={'Fail','Neutral','Success'}
 out=dict(results=rows,coverage_attempts=attempts,coverage_gaps=sorted(missing),scope='固定16局公开策略转移校准＋至多18次缺口搜索；原生样本不用于估计策略排名')
 (OUT/'结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
 val=dict(saved_native_games=len(rows),cohort_games=16,checked_steps=sum(row['steps'] for row in rows),strict_replays=replays,ContentRevision=next(iter(revisions)),covered_grades=sorted(grades),coverage_search_completed=len(attempts),coverage_gaps=sorted(missing),boundary_counts={key:sum(row['counts'][key] for row in rows) for key in rows[0]['counts']},human_games=0,unity_visual_checked=False)
 (OUT/'验证.json').write_text(json.dumps(val,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in val.items() if k!='strict_replays'},ensure_ascii=False))
if __name__=='__main__':main()
