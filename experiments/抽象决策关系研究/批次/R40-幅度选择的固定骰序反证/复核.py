"""R40独立小树控制核对及从真实初始手牌产生的严格骰序分支。"""
from pathlib import Path
from fractions import Fraction as F
from itertools import product,combinations_with_replacement
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'固定骰序.py'));R=K['K'];LITERAL={1:(2,3,1),2:(1,3,2),3:(1,2,3),4:(0,2,4),5:(0,1,5),6:(0,0,6)}
def independent(x,h,keep):
 if x==3:return F(1)
 if not h:return F(0)
 sign=1 if x<3 else -1;values=[]
 for index in (range(len(h)) if keep else (0,)):
  face=h[index];tail=list(h);tail.pop(index)
  for risky in (False,True):
   positions=[x+sign*((0,1,2)[g] if risky else (0,0,1)[g]) for g in range(3)]
   supported=[xx for xx,n in zip(positions,LITERAL[face]) if n]
   if keep and face==max(h) and h.count(face)==1 and len(h)>1 and supported!=[3]*len(supported):continue
   values.append(sum((F(n,6)*independent(xx,tail,keep) for xx,n in zip(positions,LITERAL[face])),F(0)))
 assert values
 return max(values)
def av(m,x,h,d):
 sign=1 if x<3 else -1;tail=list(h);tail.remove(d);tail=tuple(tail)
 return max(sum((p*m.solve(x+sign*(g if risky else int(g==2)),tail) for g,p in enumerate(R['STRIPS'][d])),F(0)) for risky in (False,True))
def main():
 ordered_checks=reserve_checks=0
 for x in (0,1,2,4):
  for h in product(range(1,7),repeat=2):assert K['ordered'](x,h)==independent(x,list(h),False);ordered_checks+=1
  for h in combinations_with_replacement(range(1,7),2):assert K['reserve'](x,h)==independent(x,list(h),True);reserve_checks+=1
 m=R['Model']((3,3),'toward');h=(1,1,1,5);assert m.solve(0,h)==F(293,324) and m.choices[0,h]==(1,1,True)
 branches=[]
 for grade,x,weight in ((0,0,F(1,972)),(1,1,F(1,648))):
  hh=(1,1,5);a,b=av(m,x,hh,1),av(m,x,hh,5);best=1 if a>b else 5
  assert (grade==0 and a>b) or (grade==1 and b>a)
  branches.append(dict(first_result=('坏','中')[grade],position=x,remaining=hh,next_die=best,first_die_1_value=str(a),first_die_5_value=str(b),normal_model_path_mass=str(weight),normal_mass=float(weight)))
 data=json.loads((HERE/'固定骰序结果.json').read_text());record=next(r for r in data['perhand'] if r['hand']==[1,1,1,5]);assert F(record['fixed'])==F(569,648)
 out=dict(batch='R40',independent_ordered_two_die_checks=ordered_checks,independent_reserve_two_die_checks=reserve_checks,strict_branch_initial_hand=h,initial_hand_mass='1/324',strict_branches=branches,free_exact=str(m.solve(0,h)),best_initial_fixed_exact=record['fixed'],scope='独立无缓存有序144／保留84两骰状态；两严格分支由初始手牌1,1,1,5及选定最优首手直接产生，质量只针对这两个分支，非全局反转概率；非独立四骰最优或真人样本',passed=True)
 (HERE/'复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
