"""R30冻结非对称敌人；有限数学筛选。无恢复，不把受伤当失败。"""
from pathlib import Path
from functools import lru_cache
from itertools import permutations
import runpy,json,time,gc
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE.parent/'R21-伤势与目标偏好/伤势模型.py'))
ODDS,HANDS,PAY=K['ODDS'],K['HANDS'],K['PAY'];ORDERS=tuple(permutations(range(3)))
NAMES=('甲','乙','丙')
class Model:
 def __init__(self,mode='free',fixed_order=None):
  self.mode,self.fixed_order=mode,fixed_order
  self.value=lru_cache(None)(self._value);self.end=lru_cache(None)(self._end)
 spend=staticmethod(K['Model'].spend)
 fail=staticmethod(K['Model'].fail)
 def choose(self,cs):
  top=max(v[0] for v,ch in cs);pool=[(v,ch) for v,ch in cs if top-v[0]<=1e-12]
  for j in (1,2):
   low=min(v[j] for v,ch in pool);pool=[(v,ch) for v,ch in pool if v[j]-low<=1e-12]
  high=max(v[3] for v,ch in pool);return next((v,ch) for v,ch in pool if high-v[3]<=1e-12)
 @staticmethod
 def pressure(hp,t):
  round_number=4-t
  return 1+int(hp[0]>0)+2*int(hp[1]>0 and round_number in (2,3))+int(hp[2]>0 and round_number==1)
 def _end(self,hp,c,k,q,t,order):
  out=[0.]*4
  for p,cc,kk,qq in self.spend(c,k,q,self.pressure(hp,t)):
   if kk>=7 or t<=1:children=[(1.,self.fail(kk))]
   else:
    nextorder=-1 if self.mode=='round' else order
    children=[(w,self.value(hp,cc,kk,qq,t-1,hh,nextorder)) for hh,w in HANDS[3 if kk>=4 else 4]]
   for w,v in children:
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def action_value(self,hp,c,k,q,t,h,order,route,act,d):
  rest=list(h);rest.remove(d);rest=tuple(rest);out=[0.]*4
  for p,(gain,dc) in zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act]):
   if not p:continue
   for w,cc,kk,qq in self.spend(c,k,q,-dc):
    if kk>=7:v=self.fail(kk)
    else:
     hh=list(hp);hh[route]=max(0,hh[route]-gain)
     v=self.value(tuple(hh),cc,kk,qq,t,rest,order)
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def candidates(self,hp,c,k,q,t,h,order):
  routes=[r for r,x in enumerate(hp) if x]
  if self.mode=='fixed':routes=[next(r for r in self.fixed_order if hp[r])]
  elif self.mode=='round':
   assert order>=0
   routes=[next(r for r in ORDERS[order] if hp[r])]
  return [(self.end(hp,c,k,q,t,order),('结束回合',None,None))]+[(self.action_value(hp,c,k,q,t,h,order,r,act,d),(r,act,d)) for r in routes for act in ('稳做','快做') for d in sorted(set(h))]
 def _value(self,hp,c,k,q,t,h,order=-1):
  if k>=7:return self.fail(k)
  if not any(hp):return (1.,float(k>0),float(k),float(c))
  if self.mode=='round' and order==-1:
   seen=set();choices=[]
   for i,perm in enumerate(ORDERS):
    active=tuple(r for r in perm if hp[r])
    if active in seen:continue
    seen.add(active);choices.append((self.value(hp,c,k,q,t,h,i),('选择顺序',i,None)))
   return self.choose(choices)[0]
  return self.choose(self.candidates(hp,c,k,q,t,h,order))[0]
 def summary(self):
  begin=time.perf_counter();out=[0.]*4;perhand=[]
  for h,w in HANDS[4]:
   v=self.value((4,3,2),3,0,0,3,h,-1);perhand.append(dict(hand=h,probability=w,value=v))
   for j in range(4):out[j]+=w*v[j]
  return dict(mode=self.mode,fixed_order=self.fixed_order,value=out,actual_completion=out[0],ever_injured=out[1],injury_on_success=out[2]/out[0] if out[0] else None,cold_on_success=out[3]/out[0] if out[0] else None,states=self.value.cache_info().currsize,seconds=time.perf_counter()-begin,perhand=perhand)
def main():
 rows=[]
 for mode,order in [('free',None)]+[('fixed',order) for order in ORDERS]+[('round',None)]:
  m=Model(mode,order);r=m.summary();rows.append(r);print(json.dumps({k:v for k,v in r.items() if k!='perhand'},ensure_ascii=False),flush=True)
  (HERE/'计算中间结果.json').write_text(json.dumps(dict(results=rows,status='未完成全部条件，不用于裁决'),ensure_ascii=False,indent=2)+'\n')
  m.value.cache_clear();m.end.cache_clear();del m;gc.collect()
 fixed=[r for r in rows if r['mode']=='fixed'];out=[0.]*4;choices=[];picker=Model()
 for i,(h,w) in enumerate(HANDS[4]):
  v,ch=picker.choose([(r['perhand'][i]['value'],r['fixed_order']) for r in fixed]);choices.append(dict(hand=h,probability=w,order=ch,value=v))
  for j in range(4):out[j]+=w*v[j]
 rows.append(dict(mode='initial_best_fixed',value=out,actual_completion=out[0],ever_injured=out[1],injury_on_success=out[2]/out[0] if out[0] else None,cold_on_success=out[3]/out[0] if out[0] else None,perhand=choices))
 free=rows[0]['actual_completion']
 # 策略集合包含关系逐手核对；非仅比较初始平均。
 roundrow=next(r for r in rows if r['mode']=='round');initialrow=rows[-1]
 for i in range(len(HANDS[4])):
  fv=rows[0]['perhand'][i]['value'][0];rv=roundrow['perhand'][i]['value'][0];iv=initialrow['perhand'][i]['value'][0]
  assert fv>=rv-1e-10 and rv>=iv-1e-10,(i,fv,rv,iv)
  assert all(iv>=r['perhand'][i]['value'][0]-1e-10 for r in fixed),i
 for row in rows:row['completion_gap_pp']=100*(free-row['actual_completion']);assert row['completion_gap_pp']>=-1e-8,row
 result=dict(results=rows,policy_inclusion_checked_hands=126,scope='纯完成λ0有限数学；甲4每轮1伤、乙3第2/3轮2伤、丙2第1轮1伤；三回合c3、无恢复；完整伤势，公平独立骰/首次部位均匀为假设；本批原生0真人0')
 (HERE/'非对称结果.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps({k:v for k,v in rows[-1].items() if k!='perhand'},ensure_ascii=False),flush=True)
if __name__=='__main__':main()
