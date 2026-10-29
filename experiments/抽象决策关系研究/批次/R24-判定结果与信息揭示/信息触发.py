"""公开信念模型：无揭示时保持先验，非终止到3推知长路，不读取潜在真值。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R19-阶段转换与多回合组合/组合模型.py'))
PAY=K['PAY'];ODDS=K['ODDS'];HANDS=K['HANDS']
class Model:
 def __init__(self,trigger='好档',policy='自由',g=1):
  self.trigger,self.policy,self.g=trigger,policy,g;self.decisions={};self.value=lru_cache(None)(self._value)
 @staticmethod
 def choose(candidates):
  top=max(v[0] for v,ch in candidates);return max(((v,ch) for v,ch in candidates if top-v[0]<=1e-12),key=lambda x:x[0][1])
 def candidates(self,a,la,b,lb,c,h):
  result=[((0.,0.),('结束回合',None,None))]
  for route in ('甲','乙'):
   if self.policy=='固定甲' and route=='乙':continue
   for act in ('稳做','快做'):
    for d in sorted(set(h)):
     if self.policy=='首手最低' and len(h)==4 and d!=min(h):continue
     if self.policy=='首手最高' and len(h)==4 and d!=max(h):continue
     if self.policy=='未知必用最高' and (la if route=='甲' else lb)==0 and d!=max(h):continue
     result.append((self.action_value(a,la,b,lb,c,h,route,act,d),(route,act,d)))
  return result
 def action_value(self,a,la,b,lb,c,h,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.,0.]
  for grade,(prob,(gain,dc)) in enumerate(zip(ODDS(d,self.g),PAY[act])):
   if not prob:continue
   p,length=(a,la) if route=='甲' else (b,lb);nextp=p+gain;cc=c+dc
   assert cc>=0
   reveal=self.trigger=='明示' or (gain>0 if self.trigger=='推进' else grade==2)
   branches=[(1.,length)] if length else [(.5,3),(.5,6)] if reveal or nextp>=3 else [(1.,0)]
   for w,ll in branches:
    if ll and nextp>=ll:v=(1.,float(cc))
    else:
     aa,l1,bb,l2=(nextp,ll,b,lb) if route=='甲' else (a,la,nextp,ll)
     v=self.value(aa,l1,bb,l2,cc,rest)
    for j in range(2):out[j]+=prob*w*v[j]
  return tuple(out)
 def _value(self,a,la,b,lb,c,h):
  if (la and a>=la) or (lb and b>=lb):return 1.,float(c)
  v,ch=self.choose(self.candidates(a,la,b,lb,c,h));self.decisions[(a,la,b,lb,c,h)]=ch;return v
 def summary(self):
  start=time.perf_counter();v=[0.,0.]
  for h,w in HANDS:
   vals=[self.value(0,0,0,0,5,h)] if self.trigger!='明示' else [self.value(0,a,0,b,5,h) for a in (3,6) for b in (3,6)]
   for x in vals:
    for j in range(2):v[j]+=w*x[j]/len(vals)
  return dict(trigger=self.trigger,policy=self.policy,skill=self.g,completion=v[0],cold_on_success=v[1]/v[0],states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for g in (1,2):
  for trigger in ('推进','好档','明示'):
   for policy in ('自由','首手最低','首手最高','固定甲','未知必用最高') if trigger!='明示' else ('自由',):
    row=Model(trigger,policy,g).summary();rows.append(row);print(row,flush=True)
 (HERE/'信息触发结果.json').write_text(json.dumps(dict(results=rows,scope='公开信念有限模型；短长真值固定且独立，各半；尚未独立真值复核/正式执行/体验评价'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
