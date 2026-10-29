"""在相同无伤目标下加两层强参照，不把固定路线差距当成全部策略价值。"""
import runpy,json
from pathlib import Path
HERE=Path(__file__).resolve().parent
M=runpy.run_path(str(HERE/'分段筛选.py'))
class HandRule(M['Model']):
 def __init__(self):super().__init__((5,3),True,'求解')
 def candidates(self,a,b,c,t,h):
  available=[i for i,r in enumerate((a,b)) if r>0]
  if len(available)==1:route=available[0]
  elif a<5 or b<3:route=0 if a<5 else 1
  else:route=0 if sum(d>=4 for d in h)>=2 else 1
  return [(self.next(a,b,c,t),('结束回合',None,None))]+[(self.action_value(a,b,c,t,h,route,act,d),(route,act,d)) for d in sorted(set(h)) for act in M['PAY']]
class Rolling(M['Model']):
 def __init__(self):
  self.oracles=[M['Model']((5,3),True,p) for p in ('先小目标','先大目标')]
  super().__init__((5,3),True,'求解')
 def approx_choice(self,a,b,c,t,h):
  cs=[(max((m.next(a,b,c,t) for m in self.oracles)),('结束回合',None,None))]
  for i in self.routes(a,b):
   for d in sorted(set(h)):
    for act in M['PAY']:
     v=max((m.action_value(a,b,c,t,h,i,act,d) for m in self.oracles))
     cs.append((v,(i,act,d)))
  return max(cs,key=lambda x:(round(x[0][0],12),round(x[0][1],12)))[1]
 def _value(self,a,b,c,t,h):
  self.calls+=1
  if a==b==0:return 1.,float(c)
  i,act,d=self.approx_choice(a,b,c,t,h)
  if i=='结束回合':return self.next(a,b,c,t)
  return self.action_value(a,b,c,t,h,i,act,d)

def main():
 optimum=M['Model']((5,3),True,'求解').summary()['injury_free_completion']
 rows=[]
 for cls,label in ((HandRule,'两张高骰先大，否则先小；开工后继续'),(Rolling,'两条固定路线滚动比较')):
  m=cls();r=m.summary();r['policy']=label
  assert r['injury_free_completion']<=optimum+1e-12
  rows.append(r);print(r,flush=True)
 (HERE/'强基准数学.json').write_text(json.dumps(dict(results=rows,
  scope='纯模型；额外强基准未作正式会话对照；各节点共同优化骰子和行动类型'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
