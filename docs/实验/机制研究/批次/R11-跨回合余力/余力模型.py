"""三个行动、一个目标、现有冷静；仅计算从正常起局无伤完成事件。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R09-成长与行动中的信息'/'成长边界.py'))
HANDS,ODDS=K['HANDS'],K['probabilities']
PAY={'稳做':((0,-1),(0,0),(1,0)), '快做':((0,-1),(1,-1),(2,0)), '整顿':((0,-1),(0,0),(0,1))}
class Model:
 def __init__(self,target=10,turns=3,growth=1,policy='求解',threshold=2,reset=False,recovery=1,cold=5):
  self.target,self.turns,self.g,self.policy,self.threshold,self.reset=target,turns,growth,policy,threshold,reset
  self.recovery,self.cold=recovery,cold;self.pay=PAY | {'整顿':((0,-1),(0,0),(0,recovery))}
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end);self.decisions={}
 def _end(self,r,c,t):
  if c<1 or t<=1:return (0.,0.)
  p=keep=0.
  for h,w in HANDS:
   v=self.value(r,5 if self.reset else c-1,t-1,h);p+=w*v[0];keep+=w*v[1]
  return p,keep
 def action_value(self,r,c,t,h,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);p=keep=0.
  for w,(gain,dc) in zip(ODDS(d,self.g),self.pay[act]):
   if not w or c+dc<0:continue
   v=self.value(max(0,r-gain),min(5,c+dc),t,rest);p+=w*v[0];keep+=w*v[1]
  return p,keep
 def allowed(self,r,c,t,h):
  if self.policy=='求解':return tuple(self.pay)
  if self.policy=='只推进':return ('稳做','快做')
  if self.policy=='冷静阈值':
   # 在本手存在立即保证完成的动作时，放宽固定阈值；基准也知道终点。
   if any(all(not w or (c+dc>=0 and gain>=r) for w,(gain,dc) in zip(ODDS(d,self.g),self.pay[a])) for a in ('稳做','快做') for d in set(h)):return ('稳做','快做')
   return ('整顿',) if c<=self.threshold else ('稳做','快做')
  if self.policy=='低骰整顿':
   return ('整顿',) if c<5 and h and min(h)<=2 else ('稳做','快做')
  raise AssertionError(self.policy)
 def candidates(self,r,c,t,h):
  out=[(self.end(r,c,t),('结束回合',None))]
  for a in self.allowed(r,c,t,h):
   if a=='整顿' and c>=5:continue
   for d in sorted(set(h)):
    if self.policy=='低骰整顿' and a=='整顿' and d!=min(h):continue
    out.append((self.action_value(r,c,t,h,a,d),(a,d)))
  return out
 def _value(self,r,c,t,h):
  if r==0:return 1.,float(c)
  v,ch=max(self.candidates(r,c,t,h),key=lambda x:(round(x[0][0],12),round(x[0][1],12)))
  self.decisions[(r,c,t,h)]=ch
  return v
 def summary(self):
  start=time.perf_counter();p=keep=0.
  for h,w in HANDS:
   v=self.value(self.target,self.cold,self.turns,h);p+=w*v[0];keep+=w*v[1]
  return dict(target=self.target,turns=self.turns,growth=self.g,policy=self.policy,threshold=self.threshold,reset=self.reset,recovery=self.recovery,cold=self.cold,completion=p,cold_on_success=keep/p if p else 0.,states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for policy,k in [('求解',2),('只推进',2),('冷静阈值',0),('冷静阈值',1),('冷静阈值',2),('冷静阈值',3),('冷静阈值',4),('低骰整顿',2)]:
  m=Model(policy=policy,threshold=k);r=m.summary();rows.append(r);print(r,flush=True)
 (HERE/'初筛.json').write_text(json.dumps(dict(results=rows,scope='纯模型，无伤完成；低冷静受伤后续局未覆盖；公平四d6、正常起局冷静5、无物品'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
