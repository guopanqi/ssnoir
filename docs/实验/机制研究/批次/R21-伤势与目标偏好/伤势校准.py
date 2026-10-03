"""使用R19原始正式轨迹核对新增伤势状态，不生成新游戏样本。"""
from pathlib import Path
import runpy,json,gzip,re
HERE=Path(__file__).resolve().parent;OLD=HERE.parent/'R19-阶段转换与多回合组合';M=runpy.run_path(str(HERE/'伤势模型.py'));H=runpy.run_path(str(OLD/'正式对照.py'))
def health(o):
 k=H['injury'](o);return k,int(1<=k<=3 and o['Injury'].startswith('头 '))
results=[];head=heavy=current_heavy=first_sites=collapsed=0;grades=set()
sources=[(OLD/'正式对照',json.loads((OLD/'正式对照/结果.json').read_text())['results'])]
new=HERE/'正式补覆盖'
if (new/'结果.json').exists():sources.append((new,json.loads((new/'结果.json').read_text())['results']))
for folder,row in [(folder,row) for folder,rows in sources for row in rows]:
 if row['growth']!=1:continue
 raw=json.loads(gzip.decompress((folder/row['path']).read_bytes()))
 for step in raw['Steps']:
  before,after=step['Before'],step['After'];c=H['player'](before)['Composure'];k,q=health(before);kk,qq=health(after);op=next(x for x in before['Operations'] if x['Id']==step['OperationId']);ended=after['EncounterResult'] is not None;collapse=ended and H['result'](after)['status']=='collapse'
  if op['Kind']=='action':
   d=op['DieValue'];public=H['H']['odds'](op['Odds']);expect=M['ODDS'](d,1-q)
   assert all(abs(public[label]-p)<1e-12 for label,p in zip(('坏','中','好'),expect)),(row['path'],before['Injury'],op)
   assert op['Prepared']==d+1-q
   head+=q;current_heavy+=int(k>=4)
   grade=next(e['Text'].split('，')[0].split('：')[1] for e in step['Events'] if e['Text'].startswith('判定：'));grades.add(grade);j={'Fail':0,'Neutral':1,'Success':2}[grade]
   act='整顿' if op['Card']=='整顿' else op['Card'][1:];gain,dc=M['PAY'][act][j]
  else:dc=-1
  possible=[(1.,min(5,c+dc),k,q)] if dc>=0 else M['Model'].spend(c,k,q,-dc)
  if collapse:
   assert any(candidate[2]>=7 for candidate in possible);assert kk==0 and H['player'](after)['Composure']==0;collapsed+=1
  else:
   assert any((nc,nk,nq)==(H['player'](after)['Composure'],kk,qq) for p,nc,nk,nq in possible)
   if k==0 and kk>0:first_sites+=1;assert after['Injury'].split()[0] in ('手','头','眼','脸')
   if op['Kind']=='end-turn' and not ended:
    assert len(H['player'](after)['Dice'])==(3 if kk>=4 else 4);heavy+=int(kk>=4)
  results.append(dict(record=str(folder.name+'/'+row['path']),step=len(results),before_injury=k,before_head_penalty=q,after_injury=kk,collapsed=collapse,passed=True))
out=dict(source_records=len({r['record'] for r in results}),checked_steps=len(results),head_penalty_action_checks=head,current_heavy_action_checks=current_heavy,heavy_next_round_three_dice_checks=heavy,first_injury_site_checks=first_sites,collapse_checks=collapsed,covered_grades=sorted(grades),results=results,scope='复用R19及预先冻结补覆盖轨迹，校准每步公开判定/溢出伤势/重伤骰数；旧局不重计，补覆盖独立报告，不视为玩家样本，不验证部位概率均匀')
out['coverage_gaps']=(["轻伤头部判定修正尚无后续行动记录"] if head==0 else [])+(["重伤后的新回合三骰尚未覆盖"] if heavy==0 else [])
assert collapsed>0
(HERE/'伤势校准.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k!='results'},ensure_ascii=False))
