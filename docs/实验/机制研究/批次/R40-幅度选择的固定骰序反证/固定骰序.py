"""R40仅加固定顺序／保留最高骰约束；精确数学，无正式内容。"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction as F
from itertools import permutations
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R39-目标区间与行动幅度/幅度模型.py'))
STRIPS=K['STRIPS']
@lru_cache(None)
def ordered(x,h):
 if x==3:return F(1)
 if not h:return F(0)
 sign=1 if x<3 else -1;d=h[0]
 return max(sum((p*ordered(x+sign*(g if risky else int(g==2)),h[1:]) for g,p in enumerate(STRIPS[d])),F(0)) for risky in (False,True))
@lru_cache(None)
def reserve(x,h):
 if x==3:return F(1)
 if not h:return F(0)
 sign=1 if x<3 else -1;high=max(h);cs=[]
 for d in set(h):
  tail=list(h);tail.remove(d);tail=tuple(tail)
  for risky in (False,True):
   guarantee=all(x+sign*(g if risky else int(g==2))==3 for g,p in enumerate(STRIPS[d]) if p)
   if d==high and h.count(high)==1 and len(h)>1 and not guarantee:continue
   cs.append(sum((p*reserve(x+sign*(g if risky else int(g==2)),tail) for g,p in enumerate(STRIPS[d])),F(0)))
 assert cs
 return max(cs)
def main():
 source=json.loads((HERE.parent/'R39-目标区间与行动幅度/幅度结果.json').read_text())['results'];free=next(r for r in source if r['target']==[3,3] and r['policy']=='free');highest=next(r for r in source if r['target']==[3,3] and r['policy']=='highest');toward=next(r for r in source if r['target']==[3,3] and r['policy']=='toward')
 rows=[];mean_fixed=mean_reserve=F(0);sequence_count=0;strict=[]
 for i,row in enumerate(free['perhand']):
  h=tuple(row['hand']);assert F(row['exact'])==F(toward['perhand'][i]['exact'])
  candidates=[(ordered(0,order),order) for order in sorted(set(permutations(h)))];sequence_count+=len(candidates)
  v,order=max(candidates,key=lambda x:x[0]);r=reserve(0,h);full=F(row['exact']);w=F(row['weight']);mean_fixed+=w*v;mean_reserve+=w*r
  assert F(highest['perhand'][i]['exact'])<=v<=full and r<=full
  if full>v:strict.append(dict(hand=h,weight=str(w),full=str(full),fixed=str(v),order=order))
  rows.append(dict(hand=h,weight=str(w),full=str(full),fixed=str(v),initial_order=order,reserve=str(r),gap_fixed=str(full-v),gap_reserve=str(full-r)))
 out=dict(batch='R40',full_exact=free['exact_probability'],full=free['probability'],fixed_exact=str(mean_fixed),fixed=float(mean_fixed),reserve_exact=str(mean_reserve),reserve=float(mean_reserve),fixed_gap=float(F(free['exact_probability'])-mean_fixed),reserve_gap=float(F(free['exact_probability'])-mean_reserve),initial_hands=126,initial_unique_sequences=sequence_count,ordered_states=ordered.cache_info().currsize,reserve_states=reserve.cache_info().currsize,strict_initial_hands=len(strict),strict_hand_mass=float(sum((F(r['weight']) for r in strict),F(0))),strict_examples=strict[:8],perhand=rows,scope='固定序仍优化稳险/停止；保留最高使用公开结果支持判断，不能把总体差距称全部动态骰序深度；共享R39概率，非独立四骰数学复算；正式0真人0')
 (HERE/'固定骰序结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k not in ('perhand','strict_examples')},ensure_ascii=False))
if __name__=='__main__':main()
