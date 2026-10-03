"""复用R07已冻结递推，显式注入成长概率；不修改R07文件。"""
from pathlib import Path
import runpy,json,time,hashlib
HERE=Path(__file__).resolve().parent
OLD=HERE.parent/'R07-顺序与选择权'/'分段筛选.py'
K=runpy.run_path(str(OLD));PAY,HANDS,T=K['PAY'],K['HANDS'],K['T']
def probabilities(d,g):
 b=d+g
 return tuple(n/6 for n in T['<= 1' if b<=1 else '_' if b>=7 else str(b)])
class Growth(K['Model']):
 def __init__(self,g,reset=True,policy='求解'):
  self.g=g
  super().__init__((5,3),reset,policy)
 def action_value(self,a,b,c,t,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);p=keep=0.
  for w,(gain,cost) in zip(probabilities(d,self.g),PAY[act]):
   if not w or cost>c:continue
   aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
   v=self.value(aa,bb,c-cost,t,rest);p+=w*v[0];keep+=w*v[1]
  return p,keep
class HandRule(Growth):
 def routes(self,a,b):
  raise AssertionError('手牌规则直接限制candidates')
 def candidates(self,a,b,c,t,h):
  available=[i for i,r in enumerate((a,b)) if r>0]
  if len(available)==1:route=available[0]
  elif a<5 or b<3:route=0 if a<5 else 1
  else:route=0 if sum(d>=4 for d in h)>=2 else 1
  return [(self.next(a,b,c,t),('结束回合',None,None))]+[(self.action_value(a,b,c,t,h,route,act,d),(route,act,d)) for d in sorted(set(h)) for act in PAY]
def main():
 rows=[]
 for g in range(1,5):
  for cls,policy in [(Growth,'求解'),(Growth,'先小目标'),(Growth,'先大目标'),(HandRule,'手牌规则')]:
   m=cls(g,True,'求解' if cls is HandRule else policy)
   row=m.summary();row.update(growth=g,policy=policy)
   if policy=='求解':
    mass=[0.,0.];ties=0.
    for h,w in HANDS:
     values=[max(v[0] for v,ch in m.candidates(5,3,5,2,h) if ch[0]==i) for i in range(2)]
     delta=values[0]-values[1]
     if abs(delta)>.01:mass[0 if delta>0 else 1]+=w
     else:ties+=w
    row.update(initial_route_gap_gt_one_pp_mass=mass,small_gap_mass=ties)
   rows.append(row);print({k:v for k,v in row.items() if k not in ('states','seconds')},flush=True)
  m=Growth(g,False);r=m.summary();r.update(growth=g);rows.append(r)
 (HERE/'成长边界.json').write_text(json.dumps(dict(results=rows,scope='无伤完成；2轮、冷静5、目标5/3；模型，不是玩家评价',dependencies={str(OLD.relative_to(HERE.parents[3])):hashlib.sha256(OLD.read_bytes()).hexdigest()}),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
