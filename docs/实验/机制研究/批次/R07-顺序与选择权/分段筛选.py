"""半成品清零：两个小目标、两轮、无伤完成概率。未模拟受伤后的继续游玩。"""
from pathlib import Path
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import runpy,json,time

ROOT=next(p for p in Path(__file__).resolve().parents if (p/'AGENTS.md').exists())
# 避免导入先前脚本的顶层研究；只读取同一份官方概率映射。
import re
text=(ROOT/'Engine/Runtime/Core/FateStrip.cs').read_text()
entries=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',text);assert len(entries)==7
T={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in entries}
def odds(d):return tuple(n/6 for n in T['_' if d+1>=7 else str(d+1)])
HANDS=[]
for h in combinations_with_replacement(range(1,7),4):
 n=factorial(4)
 for d in set(h):n//=factorial(h.count(d))
 HANDS.append((h,n/1296))
PAY={'稳健':((0,1),(0,0),(1,0)),'风险':((0,1),(1,1),(2,0))}
class Model:
 def __init__(self,targets=(3,2),reset=True,policy='求解'):
  self.targets,self.reset,self.policy=targets,reset,policy
  self.decisions={};self.calls=0
  self.value=lru_cache(None)(self._value);self.next=lru_cache(None)(self._next)
 def _next(self,a,b,c,t):
  if c<1 or t<=1:return 0.,0.
  if self.reset:a=self.targets[0] if a else 0;b=self.targets[1] if b else 0
  p=keep=0.
  for h,w in HANDS:
   v=self.value(a,b,c-1,t-1,h);p+=w*v[0];keep+=w*v[1]
  return p,keep
 def routes(self,a,b):
  available=[i for i,r in enumerate((a,b)) if r>0]
  if self.policy=='求解':return available
  if self.policy=='先小目标':return [min(available,key=lambda i:(self.targets[i],i))]
  if self.policy=='先大目标':return [max(available,key=lambda i:(self.targets[i],-i))]
  if self.policy=='先近终点':return [min(available,key=lambda i:((a,b)[i],i))]
  raise AssertionError(self.policy)
 def action_value(self,a,b,c,t,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);p=keep=0.
  for w,(gain,cost) in zip(odds(d),PAY[act]):
   if not w or cost>c:continue # 首次伤势以后不属于无伤完成事件
   aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
   v=self.value(aa,bb,c-cost,t,rest);p+=w*v[0];keep+=w*v[1]
  return p,keep
 def candidates(self,a,b,c,t,h):
  return [(self.next(a,b,c,t),('结束回合',None,None))]+[(self.action_value(a,b,c,t,h,i,act,d),(i,act,d)) for i in self.routes(a,b) for d in sorted(set(h)) for act in PAY]
 def _value(self,a,b,c,t,h):
  self.calls+=1
  if a==b==0:return 1.,float(c)
  v,choice=max(self.candidates(a,b,c,t,h),key=lambda x:(round(x[0][0],12),round(x[0][1],12)))
  self.decisions[(a,b,c,t,h)]=choice
  return v
 def summary(self):
  start=time.perf_counter();p=keep=0.
  for h,w in HANDS:
   v=self.value(*self.targets,5,2,h);p+=w*v[0];keep+=w*v[1]
  return dict(targets=self.targets,reset=self.reset,policy=self.policy,injury_free_completion=p,
              composure_on_success=keep/p if p else 0,states=self.calls,seconds=time.perf_counter()-start)

def main():
 results=[];models={}
 for reset in (True,False):
  for policy in ('求解','先小目标','先大目标','先近终点'):
   m=Model(reset=reset,policy=policy);models[(reset,policy)]=m
   r=m.summary();results.append(r);print(r,flush=True)
 m=models[(True,'求解')]
 # 只收集初始四骰不同手牌下的严格路线差，并排除相同价值换标签。
 witnesses=[]
 for h,w in HANDS:
  cs=m.candidates(3,2,5,2,h);routes={}
  for v,ch in cs:
   if ch[0]=='结束回合':continue
   i=ch[0]
   if i not in routes or v>routes[i][0]:routes[i]=(v,ch)
  delta=routes[0][0][0]-routes[1][0][0]
  if abs(delta)>.01:
   witnesses.append(dict(hand=h,initial_hand_probability=w,best_route=0 if delta>0 else 1,gap=abs(delta),
                         alternatives={str(i):dict(value=v,choice=ch) for i,(v,ch) in routes.items()}))
 print('初始严格路线反例',len(witnesses),'两方向',sorted(set(w['best_route'] for w in witnesses)),flush=True)
 print(witnesses[:3],flush=True)
 Path(__file__).with_name('分段筛选结果.json').write_text(json.dumps(dict(evidence_level='model_only_injury_free_objective',
  results=results,witnesses=witnesses,assumptions=['四骰独立均匀d6；技能1；冷静5；两轮；无恢复品',
  '首伤事件视为未实现无伤完成，不等于游戏失败；不模拟伤后的判定与骰数变化',
  '目标3与2；未完成目标回合末清零；完成保留','先比较无伤完成率，平手保留成功路径上的冷静']),ensure_ascii=False,indent=2)+'\n')

if __name__=='__main__':main()
