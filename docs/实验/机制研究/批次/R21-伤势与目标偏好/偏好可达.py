"""前向传播最优策略的实际访问质量；从可达状态寻找偏好反转，不拿全部许可状态当玩家频率。"""
from pathlib import Path
from collections import defaultdict
import json,runpy
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'伤势模型.py'))
def reach(m,stop_states=frozenset()):
 layers=defaultdict(lambda:defaultdict(float));seen={};terminal=defaultdict(float)
 for h,w in K['HANDS'][4]:layers[(3,4)][(5,3,3,0,0,3,h)]+=w
 def push(s,w):
  a,b,c,k,q,t,h=s
  if k>=7:terminal['collapse']+=w
  elif a==b==0:terminal['success']+=w
  else:layers[t,len(h)][s]+=w
 for t in range(3,0,-1):
  for n in range(4,-1,-1):
   states=layers.pop((t,n),{})
   for s,w in states.items():
    if s in stop_states:terminal['preference-difference']+=w;continue
    seen[s]=w;a,b,c,k,q,t,h=s;m.value(*s);route,act,d=m.decisions[s]
    if route=='结束回合':
     for p,cc,kk,qq in m.spend(c,k,q,1):
      if kk>=7:terminal['collapse']+=w*p
      elif t<=1:terminal['timeout']+=w*p
      else:
       aa=5 if m.reset and a else a;bb=3 if m.reset and b else b
       for hh,weight in K['HANDS'][3 if kk>=4 else 4]:push((aa,bb,cc,kk,qq,t-1,hh),w*p*weight)
    else:
     rest=list(h);rest.remove(d);rest=tuple(rest)
     for prob,(gain,dc) in zip(K['ODDS'](d,1-q),K['PAY'][act]):
      if not prob:continue
      branches=m.spend(c,k,q,-dc) if dc<0 else [(1.,min(5,c+dc),k,q)]
      for p,cc,kk,qq in branches:
       aa,bb=(max(0,a-gain),b) if route=='甲' else (a,max(0,b-gain)) if route=='乙' else (a,b)
       push((aa,bb,cc,kk,qq,t,rest),w*prob*p)
 assert abs(sum(terminal.values())-1)<1e-10,terminal
 return seen,dict(terminal)
def main():
 models=[K['Model'](True,cost) for cost in (0.,1.)];summaries=[m.summary() for m in models]
 reached=[reach(m) for m in models];witnesses=[];changed=[0.,0.]
 for s in reached[0][0].keys() & reached[1][0].keys():
  c0=models[0].decisions[s];c1=models[1].decisions[s]
  if c0==c1:continue
  for i in (0,1):changed[i]+=reached[i][0][s]
  v0=models[0].value(*s);v1=models[1].value(*s)
  alternatives=[]
  for i,other in ((0,c1),(1,c0)):
   m=models[i];value=m.end(*s[:-1]) if other[0]=='结束回合' else m.action_value(*s,*other)
   alternatives.append(value)
  loss0=models[0].score(v0)-models[0].score(alternatives[0]);loss1=models[1].score(v1)-models[1].score(alternatives[1])
  if min(loss0,loss1)<.01:continue
  witnesses.append(dict(state=s,pure_completion=c0,health_preference=c1,pure_completion_value=v0,health_value=v1,pure_loss_if_health_action=loss0,health_loss_if_completion_action=loss1,visit_mass=[r[0][s] for r in reached]))
 witnesses.sort(key=lambda x:(x['state'][3]==0,min(x['visit_mass']),min(x['pure_loss_if_health_action'],x['health_loss_if_completion_action'])),reverse=True)
 strict={tuple(x['state'][:-1])+(tuple(x['state'][-1]),) for x in witnesses}
 event=[reach(m,strict)[1].get('preference-difference',0.) for m in models]
 out=dict(first_strict_difference_event_probability=event,summaries=summaries,terminals=[r[1] for r in reached],reachable_state_counts=[len(r[0]) for r in reached],expected_shared_changed_decision_visits=changed,strict_witnesses=len(witnesses),witnesses=witnesses[:30],scope='同一清零规则与公开状态、不同研究目标；访问质量是求解策略的模型值，未声称真人频率；反转要求双方自身效用损失均至少0.01')
 (HERE/'偏好可达.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k!='witnesses'},ensure_ascii=False),flush=True)
 if witnesses:print(json.dumps(witnesses[0],ensure_ascii=False),flush=True)
if __name__=='__main__':main()
