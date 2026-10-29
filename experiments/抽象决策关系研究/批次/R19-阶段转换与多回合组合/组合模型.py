"""半成品回合末清零、冷静延续与整顿；无伤完成目标，不把首伤视为游戏失败。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R09-成长与行动中的信息'/'成长边界.py'))
HANDS,ODDS=K['HANDS'],K['probabilities']
PAY={'稳做':((0,-1),(0,0),(1,0)),'快做':((0,-1),(1,-1),(2,0)),'整顿':((0,-1),(0,0),(0,2))}
def key(v):return v[0],v[1]
def choose(candidates):
 # 概率舍入边界可能把数值平手拆开，先相对最大值选择容差带。
 top=max(v[0] for v,ch in candidates)
 return max(((v,ch) for v,ch in candidates if top-v[0]<=1e-12),key=lambda x:x[0][1])
class Model:
 def __init__(self,reset=True,policy='自由',threshold=2,g=1,cold=3):
  self.reset,self.policy,self.threshold,self.g,self.cold=reset,policy,threshold,g,cold;self.decisions={}
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 def _end(self,a,b,c,t):
  if c<1 or t<=1:return 0.,0.
  if self.reset:a=5 if a else 0;b=3 if b else 0
  out=[0.,0.]
  for h,w in HANDS:
   v=self.value(a,b,c-1,t-1,h);out[0]+=w*v[0];out[1]+=w*v[1]
  return tuple(out)
 def progress_actions(self,a,b):
  routes=[r for r,v in [('甲',a),('乙',b)] if v>0]
  if self.policy=='先甲' and a>0:routes=['甲']
  if self.policy=='先乙' and b>0:routes=['乙']
  return [(r,act) for r in routes for act in ('稳做','快做')]
 def can_finish_now(self,a,b,c,h):
  if a and b:return False
  r=a or b
  return any(all(not w or (c+dc>=0 and gain>=r) for w,(gain,dc) in zip(ODDS(d,self.g),PAY[act])) for d in set(h) for act in ('稳做','快做'))
 def actions(self,a,b,c,h):
  progress=self.progress_actions(a,b)
  if self.policy=='不用整顿':return progress
  if c>=5:return progress
  if self.policy=='冷静阈值' and c<=self.threshold and not self.can_finish_now(a,b,c,h):return [('整顿','整顿')]
  if self.policy=='每轮先整顿' and len(h)==4 and not self.can_finish_now(a,b,c,h):return [('整顿','整顿')]
  if self.policy in ('冷静阈值','每轮先整顿'):return progress
  return progress+[('整顿','整顿')]
 def action_value(self,a,b,c,t,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.]
  for w,(gain,dc) in zip(ODDS(d,self.g),PAY[act]):
   if not w or c+dc<0:continue
   aa,bb=(max(0,a-gain),b) if route=='甲' else (a,max(0,b-gain)) if route=='乙' else (a,b)
   v=self.value(aa,bb,min(5,c+dc),t,rest);out[0]+=w*v[0];out[1]+=w*v[1]
  return tuple(out)
 def candidates(self,a,b,c,t,h):
  return [(self.end(a,b,c,t),('结束回合',None,None))]+[(self.action_value(a,b,c,t,h,r,act,d),(r,act,d)) for r,act in self.actions(a,b,c,h) for d in sorted(set(h))]
 def _value(self,a,b,c,t,h):
  if a==b==0:return 1.,float(c)
  if (a+1)//2+(b+1)//2>len(h)+4*(t-1):self.decisions[(a,b,c,t,h)]=('结束回合',None,None);return 0.,0.
  v,ch=choose(self.candidates(a,b,c,t,h));self.decisions[(a,b,c,t,h)]=ch;return v
 def summary(self):
  start=time.perf_counter();p=keep=0.
  for h,w in HANDS:
   v=self.value(5,3,self.cold,3,h);p+=w*v[0];keep+=w*v[1]
  return dict(reset=self.reset,policy=self.policy,threshold=self.threshold,skill=self.g,cold=self.cold,no_injury_completion=p,cold_on_success=keep/p if p else 0.,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for reset in (True,False):
  for policy,k in [('自由',2),('先甲',2),('先乙',2),('不用整顿',2),('每轮先整顿',2)]+[('冷静阈值',k) for k in range(5)]:
   row=Model(reset,policy,k).summary();rows.append(row);print(row,flush=True)
  left=Model(reset,'先甲');right=Model(reset,'先乙');values=[(w,max((left.value(5,3,3,3,h),right.value(5,3,3,3,h)),key=key)) for h,w in HANDS]
  p=sum(w*v[0] for w,v in values);keep=sum(w*v[1] for w,v in values)
  rows.append(dict(reset=reset,policy='看初始手牌选定目标顺序',no_injury_completion=p,cold_on_success=keep/p))
 for g,cold in [(2,3),(1,5)]:
  for policy in ('自由','先甲','先乙','每轮先整顿'):
   row=Model(True,policy,g=g,cold=cold).summary();rows.append(row);print(row,flush=True)
 (HERE/'组合结果.json').write_text(json.dumps(dict(results=rows,scope='无伤全部完成事件；甲5乙3、三回合、初始冷静3、整顿好+2上限5；不是实际通关率；每组126公平独立手牌'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
