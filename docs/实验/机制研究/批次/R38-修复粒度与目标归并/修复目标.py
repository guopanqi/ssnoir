"""R38：复用冻结R36转移，仅限制修复对象的选择规则；数学证据。"""
from pathlib import Path
from functools import lru_cache
import runpy,json,time,gc
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R36-资源余量与选择增量/压力模型.py'))
HANDS=K['HANDS'];STYLES=('short','long','initial','round')
class Model(K['Model']):
 def __init__(self,objective='completion',style='short'):
  assert style in STYLES
  self.style=style
  super().__init__(objective,'每回合固定偏好' if style=='round' else '自由',True)
 def candidates(self,p,a,b,s,c,k,q,t,h,pref):
  repairs=[r for r,left in ((1,a),(2,b)) if left]
  mode=0 if self.style=='short' else 1 if self.style=='long' else pref
  assert mode in (0,1)
  if len(repairs)>1:
   target=min(a,b) if mode==0 else max(a,b)
   repairs=[r for r,left in ((1,a),(2,b)) if left==target]
  routes=[0]+repairs
  return [(self.end(p,a,b,s,c,k,q,t,pref),('结束回合',None,None))]+[(self.action_value(p,a,b,s,c,k,q,t,h,pref,r,act,d),(r,act,d)) for r in routes for act in ('稳做','快做') for d in sorted(set(h))]
 def _value(self,p,a,b,s,c,k,q,t,h,pref=-1):
  if self.style=='initial' and pref==-1 and p<12 and s<5 and k<7:
   v,ch=self.choose([(self.value(p,a,b,s,c,k,q,t,h,x),('选择修复目标规则',x,None)) for x in (0,1)])
   self.decisions[(p,a,b,s,c,k,q,t,h,pref)]=ch
   return v
  return super()._value(p,a,b,s,c,k,q,t,h,pref)
 def summary(self):
  out=super().summary();out['repair_target_style']=self.style;return out

def main():
 frozen=json.loads((HERE.parent/'R36-资源余量与选择增量/压力结果.json').read_text())
 free={r['objective']:r for r in frozen['results'] if r['policy']=='自由' and r['instant_pressure']}
 rows=[];checks=0
 for objective in ('completion','early'):
  primary=lambda v:sum(v[:3]) if objective=='completion' else 3*v[0]+2*v[1]+v[2]
  for style in STYLES:
   m=Model(objective,style);row=m.summary()
   row['primary_gap_to_free']=primary(free[objective]['value'])-primary(row['value'])
   assert row['primary_gap_to_free']>=-1e-10
   for a,b in zip(row['perhand'],free[objective]['perhand']):
    assert tuple(a['hand'])==tuple(b['hand']) and abs(a['probability']-b['probability'])<1e-12
    assert primary(b['value'])>=primary(a['value'])-1e-10;checks+=1
   rows.append(row);print(json.dumps({k:v for k,v in row.items() if k!='perhand'},ensure_ascii=False),flush=True)
   m.value.cache_clear();m.end.cache_clear();del m;gc.collect()
 out=dict(batch='R38',results=rows,initial_hand_containment_checks=checks,scope='只限制修复对象；开局/每回合可择短/长，但时点、骰、稳险、结束仍优化；共享R36转移，非独立数学优化；正式0真人0')
 (HERE/'修复目标结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
