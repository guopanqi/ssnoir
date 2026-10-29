"""在真实延续冷静的公开最优策略上，逐轮推送概率；首伤事件吸收，不伪称游戏失败。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'余力模型.py'));S=runpy.run_path(str(HERE/'强参照.py'))
def analyze(m):
 ignored=S['IgnoreCarry'](target=m.target,turns=m.turns,growth=m.g,cold=m.cold,recovery=m.recovery);ignored.summary()
 stage={(m.target,m.cold,h,0,False):w for h,w in K['HANDS']}
 terminals={};state_mass={};events=[0.]*m.turns;heal_events=[0.]*m.turns;push_events=[0.]*m.turns;inj=timeout=success=weighted_c=0.
 for t in range(m.turns,0,-1):
  buckets=[{} for _ in range(5)];buckets[4]=stage;stage={}
  def finish(w,r,c,mask,flag,status):
   nonlocal success,inj,timeout,weighted_c
   terminals[(mask,flag)]=terminals.get((mask,flag),0.)+w
   if status=='success':success+=w;weighted_c+=w*c
   elif status=='injury':inj+=w
   else:timeout+=w
  for n in range(4,-1,-1):
   for (r,c,h,mask,flag),w in buckets[n].items():
    if r==0:finish(w,r,c,mask,flag,'success');continue
    st=(r,c,t,h);state_mass[st]=state_mass.get(st,0.)+w;m.value(*st);act,d=m.decisions[st]
    if h and c<5:
     cs=m.candidates(*st);vh=max(v[0] for v,ch in cs if ch[0]=='整顿');vp=max(v[0] for v,ch in cs if ch[0] in ('稳做','快做'))
     if vh-vp>.01:heal_events[m.turns-t]+=w
     if vp-vh>.01:push_events[m.turns-t]+=w
    if h:
     ignored.value(*st);a2,d2=ignored.decisions[st]
     alt=m.end(r,c,t) if a2=='结束回合' else m.action_value(r,c,t,h,a2,d2)
     if t<m.turns and m.value(*st)[0]-alt[0]>.01:flag=True;events[m.turns-t]+=w
    if act=='结束回合':
     if c<1:finish(w,r,c,mask,flag,'injury')
     elif t<=1:finish(w,r,c,mask,flag,'timeout')
     else:
      for hh,q in K['HANDS']:
       key=(r,c-1,hh,mask,flag);stage[key]=stage.get(key,0.)+w*q
     continue
    rest=list(h);rest.remove(d);rest=tuple(rest)
    if act=='整顿':mask|=1<<(m.turns-t)
    for q,(gain,dc) in zip(K['ODDS'](d,m.g),m.pay[act]):
     if not q:continue
     if c+dc<0:finish(w*q,r,c,mask,flag,'injury');continue
     key=(max(0,r-gain),min(5,c+dc),rest,mask,flag);buckets[n-1][key]=buckets[n-1].get(key,0.)+w*q
 assert abs(sum(terminals.values())-1)<1e-10
 assert abs(success-m.summary()['completion'])<1e-10
 assert abs(weighted_c-success*m.summary()['cold_on_success'])<1e-10
 examples=[]
 for st,w in state_mass.items():
  r,c,t,h=st
  if len(h)!=3 or c>=5 or t!=1 or w<.0002:continue
  w2=state_mass.get((r,c,2,h),0.)
  if w2==0:continue
  m.value(r,c,1,h);m.value(r,c,2,h)
  vals=[]
  for tt in (1,2):
   cs=m.candidates(r,c,tt,h);vh=max(v[0] for v,ch in cs if ch[0]=='整顿');vp=max(v[0] for v,ch in cs if ch[0] in ('稳做','快做'))
   vals.append(dict(turns=tt,heal=vh,push=vp,choice=m.decisions[(r,c,tt,h)]))
  if vals[0]['push']-vals[0]['heal']>.01 and vals[1]['heal']-vals[1]['push']>.01:examples.append(dict(state=st,visit_probability_mass=w,other_time_visit_probability_mass=w2,comparison=vals))
 return dict(success=success,injury_objective_failure=inj,timeout_objective_failure=timeout,at_least_one_heal=sum(w for (mask,flag),w in terminals.items() if mask),heal_in_each_round=[sum(w for (mask,flag),w in terminals.items() if mask&(1<<i)) for i in range(m.turns)],late_carry_mismatch_probability=sum(w for (mask,flag),w in terminals.items() if flag),expected_late_carry_mismatch_events=events,expected_strict_heal_events=heal_events,expected_strict_push_events=push_events,time_flip_examples=sorted(examples,key=lambda e:e['visit_probability_mass'],reverse=True)[:6],scope='概率是在无伤模型到首次伤势/完成/到期为止的正常起局轨迹；每轮使用整顿概率含失败判定；事件次数不是玩家犹豫率；期限例子两种期限均在该策略轨迹可达，分别给出质量')
def main():
 m=K['Model'](cold=3,recovery=2);m.summary();r=analyze(m);print(json.dumps(r,ensure_ascii=False,indent=2));(HERE/'可达分析.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
