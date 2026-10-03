"""阶段窗口和成果选择的成长边界、正常起局路径；不把事件当玩家体验。"""
from pathlib import Path
from collections import defaultdict
import runpy,json
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'数学筛选.py'));Window,Harvest=K['Window'],K['Harvest'];HANDS,ODDS,PAY=K['HANDS'],K['ODDS'],K['PAY']
def window_reach():
 m=Window();baseline=Window(policy='用完当手');m.summary();levels=defaultdict(lambda:defaultdict(float))
 for h,w in HANDS:levels[19][(3,4,5,3,h,False)]+=w
 terminal=defaultdict(float);early=0.;first_early=0.;openings=0.;instances=[]
 for rank in sorted(range(20),reverse=True):
  for state,mass in list(levels[rank].items()):
   p,q,c,t,h,ended=state
   if q==0:terminal['success']+=mass;continue
   m.value(p,q,c,t,h);act,d=m.decisions[(p,q,c,t,h)]
   if act=='结束回合':
    if h and p and t>1 and c>0:
     early+=mass
     if not ended:first_early+=mass
     alternatives=[(v,ch) for v,ch in m.candidates(p,q,c,t,h) if ch[0]!='结束回合']
     if alternatives:
      gap=m.end(p,q,c,t)[0]-max(v[0] for v,ch in alternatives)
      if gap>.01:instances.append(dict(prep_remaining=p,payload_remaining=q,cold=c,turns=t,hand=h,visit_mass=mass,end_value=m.end(p,q,c,t)[0],best_action_value=max(v[0] for v,ch in alternatives),gap=gap))
    if p==0:terminal['window_expired']+=mass
    elif c<1:terminal['first_injury']+=mass
    elif t<=1:terminal['timeout']+=mass
    else:
     for fresh,w in HANDS:levels[(t-1)*5+4][(p,q,c-1,t-1,fresh,ended or bool(h))]+=mass*w
    continue
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for w,(gain,cost) in zip(ODDS(d,m.g),PAY[act]):
    if not w:continue
    if cost>c:terminal['first_injury']+=mass*w;continue
    pp,qq=(max(0,p-gain),q) if p else (0,max(0,q-gain))
    if p>0 and pp==0:openings+=mass*w
    levels[t*5+len(rest)][(pp,qq,c-cost,t,rest,ended)]+=mass*w
 assert abs(sum(terminal.values())-1)<1e-12
 assert abs(terminal['success']-m.summary()['injury_free_completion'])<1e-12
 instances.sort(key=lambda x:x['visit_mass'],reverse=True)
 return dict(terminal=dict(terminal),probability_at_least_one_early_end=first_early,expected_early_ends=early,opening_mass=openings,early_end_examples=instances[:8],scope='无伤模型，首次受伤止；提前结束指仍有骰且前置尚未完成；正常路径概率不是犹豫率')
def harvest_reach():
 m=Harvest();row=m.summary();levels=defaultdict(lambda:defaultdict(float))
 for h,w in HANDS:levels[4][(3,6,5,h,-1,False)]+=w
 scoremass=defaultdict(float);switch_mass=0.;directions=[0.,0.];expected_switches=0.;examples=[];first=[0.,0.]
 for rank in range(4,-1,-1):
  for state,mass in list(levels[rank].items()):
   a,b,c,h,last,switched=state;m.value(a,b,c,h,-1);route,act,d=m.decisions[(a,b,c,h,-1)]
   if route=='结束回合':
    score=int(m.settle(a,b,c)[0]);scoremass[score]+=mass
    if switched:switch_mass+=mass
    continue
   if last<0:first[route]+=mass
   elif route!=last:
    expected_switches+=mass;directions[last]+=mass
    other=max([v[0] for v,ch in m.candidates(a,b,c,h,-1) if ch[0]==last] or [m.settle(a,b,c)[0]])
    chosen=m.value(a,b,c,h,-1)[0]
    if chosen-other>.01:examples.append(dict(remaining=[a,b],cold=c,hand=h,previous_route=last,chosen_route=route,visit_mass=mass,chosen_expected_score=chosen,continue_previous_expected_score=other,gap=chosen-other))
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for w,(gain,cost) in zip(ODDS(d,m.g),PAY[act]):
    if not w:continue
    aa,bb=(max(0,a-gain),b) if route==0 else (a,max(0,b-gain))
    levels[len(rest)][(aa,bb,c-cost,rest,route,switched or (last>=0 and route!=last))]+=mass*w
 assert abs(sum(scoremass.values())-1)<1e-12 and abs(sum(s*w for s,w in scoremass.items())-row['expected_score'])<1e-12
 examples.sort(key=lambda x:x['visit_mass'],reverse=True)
 return dict(score_mass=dict(scoremass),at_least_one_switch=switch_mass,expected_switches=expected_switches,switch_direction_event_mass=directions,first_route_mass=first,switch_examples=examples[:8],scope='正常冷静5单回合无受伤路径；最优政策选择不是玩家体验')
def main():
 rows=[]
 for g in range(1,5):
  for policy in ('求解','用完当手'):
   m=Window(growth=g,policy=policy);rows.append(dict(kind='window',**m.summary()))
  for policy in ('求解','固定小','固定大','开局选定'):
   m=Harvest(growth=g,policy=policy);rows.append(dict(kind='harvest',**m.summary()))
 (HERE/'成长边界.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
 result=dict(window=window_reach(),harvest=harvest_reach());(HERE/'可达分析.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
 for name,data in result.items():print(name,{k:v for k,v in data.items() if not k.endswith('examples')})
 print('window example',result['window']['early_end_examples'][:2]);print('harvest example',result['harvest']['switch_examples'][:2])
if __name__=='__main__':main()
