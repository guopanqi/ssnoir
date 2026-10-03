"""压制解除首回合甲期限；两目标仍须完成。只求无伤事件。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R09-成长与行动中的信息'/'成长边界.py'))
HANDS,ODDS,PAY=K['HANDS'],K['probabilities'],K['PAY']
def key(v):return round(v[0],12),round(v[1],12)
class Model:
 def __init__(self,targets=(3,5),g=1,policy='自由'):
  self.targets,self.g,self.policy=targets,g,policy;self.decisions={}
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 def _end(self,a,b,c,t,q):
  if c<1 or t<=1 or (t==2 and a>0 and not q):return 0.,0.
  out=[0.,0.]
  for h,w in HANDS:
   v=self.value(a,b,c-1,t-1,q,h);out[0]+=w*v[0];out[1]+=w*v[1]
  return tuple(out)
 def actions(self,a,b,t,q,h):
  choices=[]
  if a>0:choices.extend(('甲',act) for act in PAY)
  if b>0:choices.extend(('乙',act) for act in PAY)
  if t==2 and a>0 and not q and self.policy!='取消压制' and (self.policy!='末骰压制' or len(h)==1):choices.append(('压制','风险'))
  if self.policy=='先完成甲' and a>0:choices=[x for x in choices if x[0]=='甲']
  if self.policy=='先压制' and t==2 and a>0 and not q:choices=[x for x in choices if x[0]=='压制']
  return choices
 def action_value(self,a,b,c,t,q,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.]
  for w,(gain,cost) in zip(ODDS(d,self.g),PAY[act]):
   if not w or c<cost:continue
   aa,bb,qq=(max(0,a-gain),b,q) if route=='甲' else (a,max(0,b-gain),q) if route=='乙' else (a,b,q or gain>0)
   v=self.value(aa,bb,c-cost,t,int(qq),rest);out[0]+=w*v[0];out[1]+=w*v[1]
  return tuple(out)
 def candidates(self,a,b,c,t,q,h):
  return [(self.end(a,b,c,t,q),('结束回合',None,None))]+[(self.action_value(a,b,c,t,q,h,route,act,d),(route,act,d)) for route,act in self.actions(a,b,t,q,h) for d in sorted(set(h))]
 def _value(self,a,b,c,t,q,h):
  if a==b==0:return 1.,float(c)
  if (a+1)//2+(b+1)//2>len(h)+4*(t-1):self.decisions[(a,b,c,t,q,h)]=('结束回合',None,None);return 0.,0.
  v,ch=max(self.candidates(a,b,c,t,q,h),key=lambda x:key(x[0]));self.decisions[(a,b,c,t,q,h)]=ch;return v
 def summary(self):
  start=time.perf_counter();p=keep=0.
  for h,w in HANDS:
   v=self.value(*self.targets,5,2,0,h);p+=w*v[0];keep+=w*v[1]
  return dict(targets=self.targets,skill=self.g,policy=self.policy,no_injury_completion=p,cold_on_success=keep/p if p else 0.,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for targets,g in [((3,5),1),((3,5),2),((2,5),1),((3,4),1)]:
  for policy in ('自由','取消压制','先完成甲','先压制','末骰压制'):
   row=Model(targets,g,policy).summary();rows.append(row);print(row,flush=True)
 (HERE/'期限结果.json').write_text(json.dumps(dict(results=rows,scope='仅无伤完成模型；压制1格取消首回合甲期限，两回合全部完成3/5；四骰均匀独立，不是正式游戏胜率或体验'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
