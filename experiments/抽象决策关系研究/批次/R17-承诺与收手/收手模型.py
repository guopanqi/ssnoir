"""一次押上未兑现成果：期望结算进度。使用Fraction，无伤四骰。"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction as F
import runpy,json,hashlib
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R07-顺序与选择权'/'分段筛选.py'))
HANDS=[(h,F(round(w*1296),1296)) for h,w in K['HANDS']]
def odds(d,g):
 b=d+g
 return tuple(F(n,6) for n in K['T']['<= 1' if b<=1 else '_' if b>=7 else str(b)])
class Model:
 def __init__(self,g=1,policy='自由',threshold=None):
  self.g,self.policy,self.threshold=g,policy,threshold;self.decisions={};self.value=lru_cache(None)(self._value)
 def continuation(self,s,h,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);bad,mid,good=odds(d,self.g)
  return mid*self.value(s+1,rest)+good*self.value(s+2,rest)
 def candidates(self,s,h):
  choices=[(F(s),'收手')]
  if h:
   ds=sorted(set(h)) if self.policy=='自由' else [max(h)]
   choices += [(self.continuation(s,h,d),d) for d in ds]
  return choices
 def _value(self,s,h):
  if not h:return F(s)
  if self.policy=='用完':ch=max(h)
  elif self.policy=='单步':
   d=max(h);bad,mid,good=odds(d,self.g)
   ch=d if mid*(s+1)+good*(s+2)>s else '收手'
  elif self.policy=='阈值':ch='收手' if s>=self.threshold else max(h)
  else:
   # 同价值优先收手；同价值骰序选高骰，避免把平手序列当作策略差异。
   ch=max(self.candidates(s,h),key=lambda x:(x[0],x[1]=='收手',x[1] if x[1]!='收手' else 0))[1]
  self.decisions[(s,h)]=ch
  return F(s) if ch=='收手' else self.continuation(s,h,ch)
 def summary(self):
  score=sum(w*self.value(0,h) for h,w in HANDS)
  return dict(skill=self.g,policy=self.policy,threshold=self.threshold,expected_score=float(score),exact=str(score),states=self.value.cache_info().currsize)

def reachable(m):
 mass={(0,h):w for h,w in HANDS};terminal={};stops=[];witnesses=[];stop_mass=F(0)
 for n in range(4,-1,-1):
  for (s,h),w in list(mass.items()):
   if len(h)!=n or not w:continue
   m.value(s,h);ch=m.decisions.get((s,h),'收手')
   if ch=='收手':
    terminal[s]=terminal.get(s,F(0))+w
    if h:
     stop_mass+=w
     stops.append(dict(progress=s,hand=h,mass=float(w),best_continue=float(max(v for v,c in m.candidates(s,h) if c!='收手'))))
    continue
   rest=list(h);rest.remove(ch);rest=tuple(rest);bad,mid,good=odds(ch,m.g)
   terminal[0]=terminal.get(0,F(0))+w*bad
   for gain,p in ((1,mid),(2,good)):
    if p:mass[(s+gain,rest)]=mass.get((s+gain,rest),F(0))+w*p
   if len(h)>1 and s>0:
    v=m.value(s,h);delta=v-s
    if delta>0:witnesses.append(dict(progress=s,hand=h,die=ch,mass=float(w),stop_score=s,continue_score=float(v)))
 assert sum(terminal.values())==1
 assert sum(F(s)*w for s,w in terminal.items())==sum(w*m.value(0,h) for h,w in HANDS)
 return dict(score_distribution={str(s):float(w) for s,w in sorted(terminal.items())},voluntary_stop_mass=float(stop_mass),voluntary_stop_exact=str(stop_mass),stops=sorted(stops,key=lambda r:-r['mass']),continue_witnesses=sorted(witnesses,key=lambda r:-r['mass']))

def main():
 rows=[];paths=[]
 for g in range(1,5):
  for policy in ('自由','高骰动态停止','单步','用完'):
   m=Model(g,policy);rows.append(m.summary())
   if policy=='自由':paths.append(dict(skill=g,**reachable(m)))
  thresholds=[Model(g,'阈值',t).summary() for t in range(0,9)]
  rows.extend(thresholds)
 controls=[dict(skill=g,expected_score=float(4*sum(sum(p*gain for p,gain in zip(odds(d,g),(0,1,2))) for d in range(1,7))/6),policy='取消全损；所有骰用完') for g in range(1,5)]
 result=dict(results=rows,paths=paths,no_loss_controls=controls,scope='数学筛选；单回合四骰、初始成果0、坏档未兑现成果全损，中+1好+2；无恢复品、无首次受伤；模型不是正式会话或玩家证据',probability_source_sha256=hashlib.sha256((K['ROOT']/'Engine/Runtime/Core/FateStrip.cs').read_bytes()).hexdigest())
 (HERE/'收手结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 for r in rows:
  if r['policy']!='阈值':print(r)
 for p in paths:print(dict(skill=p['skill'],voluntary_stop_mass=p['voluntary_stop_mass'],top_stop=p['stops'][:1]))
if __name__=='__main__':main()
