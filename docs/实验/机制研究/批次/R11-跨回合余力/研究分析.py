"""冻结模型的边界与独立单回合Fraction核对；保存复算条件，不包含真人评价。"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'余力模型.py'));Model=K['Model']
def exact_one_round(growth):
 @lru_cache(None)
 def value(r,c,h):
  if not r:return Fraction(1),Fraction(c)
  if not h:return Fraction(0),Fraction(0)
  possibilities=[(Fraction(0),Fraction(0))]
  for kind in ('稳做','快做','整顿'):
   if kind=='整顿' and c==5:continue
   effects={'稳做':[(0,-1),(0,0),(1,0)],'快做':[(0,-1),(1,-1),(2,0)],'整顿':[(0,-1),(0,0),(0,2)]}[kind]
   for die in set(h):
    remaining=list(h);remaining.remove(die);remaining=tuple(remaining);p=keep=Fraction(0)
    counts=[round(q*6) for q in K['ODDS'](die,growth)]
    for count,(gain,delta) in zip(counts,effects):
     if not count or c+delta<0:continue
     v=value(max(0,r-gain),min(5,c+delta),remaining);p+=Fraction(count,6)*v[0];keep+=Fraction(count,6)*v[1]
    possibilities.append((p,keep))
  return max(possibilities)
 return value

def main():
 growth=[]
 for g in (1,2,3,4):
  for policy in ('求解','只推进'):
   m=Model(growth=g,cold=3,recovery=2,policy=policy);growth.append(m.summary())
 reset_rows=[]
 for reset in (False,True):
  m=Model(cold=3,recovery=2,reset=reset);s=(5,1,2,(1,4,5));m.value(*s);cs=m.candidates(*s)
  reset_rows.append(dict(reset=reset,state=s,choice=m.decisions[s],heal=max(v[0] for v,ch in cs if ch[0]=='整顿'),push=max(v[0] for v,ch in cs if ch[0] in ('稳做','快做'))))
 (HERE/'成长与延续.json').write_text(json.dumps(dict(results=growth,planning_ablation=reset_rows,scope='成长不增大目标；reset只作规划消融，正式游戏冷静照常延续'),ensure_ascii=False,indent=2)+'\n')
 checks=[]
 for g in (1,2):
  exact=exact_one_round(g);m=Model(cold=3,recovery=2,growth=g)
  for r in range(1,7):
   for c in (0,1,3,5):
    for h in ((1,2),(1,2,5),(1,3,5),(4,6,6)):
     truth=exact(r,c,h);v=m.value(r,c,1,h);assert all(abs(float(x)-y)<1e-12 for x,y in zip(truth,v)),(r,c,h,truth,v)
     checks.append(dict(growth=g,remaining=r,cold=c,hand=h,exact_completion=str(truth[0]),passed=True))
 (HERE/'独立核对.json').write_text(json.dumps(dict(cases=len(checks),checks=checks,scope='独立单回合递推与Fraction；输入概率仍使用已校准官方表。不是完整伤后游戏模型。'),ensure_ascii=False,indent=2)+'\n')
 # 复算邻域的明确入口，不通过改全局概率或收益表换条件。
 neighbors=[]
 for target in (8,10,12):
  for policy in ('求解','只推进'):neighbors.append(Model(target=target,recovery=2,policy=policy).summary())
 for policy in ('求解','只推进'):neighbors.append(Model(target=10,recovery=2,cold=3,policy=policy).summary())
 for k in (0,1,2,3,4):neighbors.append(Model(recovery=2,policy='冷静阈值',threshold=k).summary())
 (HERE/'恢复邻域.json').write_text(json.dumps(dict(reason='恢复1机会成本过高；只检查封顶5的恢复2、8/10/12容量与冷静3边界',results=neighbors),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(independent_one_round_cases=len(checks),planning_ablation=reset_rows),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
