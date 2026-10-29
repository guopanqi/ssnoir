"""沿有限模型最优公开策略精确传递概率；频率是模型路径质量，不是真人行为。"""
from pathlib import Path
from collections import defaultdict
import runpy,json
HERE=Path(__file__).resolve().parent;P=runpy.run_path(str(HERE/'跨回合信息.py'));HANDS=P['HANDS'];ODDS=P['ODDS'];PAY=P['PAY']
def analyze(reset):
 m=P['Model'](reset);summary=m.summary();layers=defaultdict(lambda:defaultdict(float));parents={};states=set();witnesses=[];eventmass=defaultdict(float);terminal=defaultdict(float);max_injury_after_tax=0
 for h,w in HANDS[4]:
  s=(0,0,-1,5,0,0,2,0,h);layers[(2,4)][(s,0)]+=w;parents[s]=None
 for t in (2,1):
  for n in range(4,-1,-1):
   for (s,flags),weight in list(layers[(t,n)].items()):
    a,b,target,c,k,q,_,u,h=s;states.add(s)
    if k>=7 or target==0 and a==3 or target==1 and b==3:
     terminal[flags]+=weight;continue
    value,ch=m.choose(m.candidates(*s));route,act,d=ch
    impossible=target<0 and min(3-a,3-b)>2*len(h)
    weak=route==2 and t==2 and impossible and d<=3
    if route==2 and t==2:eventmass['first_round_probe_expected']+=weight
    if weak:eventmass['weak_impossible_probe_expected']+=weight
    if route==2 and t==2 and impossible:
     search=m.choose([(v,x) for v,x in m.candidates(*s) if x[0] in (0,1)])[0]
     end=m.end(a,b,target,c,k,q,t,u)
     if value[0]-max(search[0],end[0])>1e-10 and len(witnesses)<15:
      path=[];cursor=s
      while parents[cursor] is not None:
       prior,action,outcome=parents[cursor];path.append(dict(state=prior,action=action,outcome=outcome));cursor=prior
      path.reverse();witnesses.append(dict(state=s,state_weight=weight,choice=ch,completion_gap_vs_search_or_end=value[0]-max(search[0],end[0]),value=value,search=search,end=end,weak_die=d<=3,path=path))
    if route=='结束回合':
     branches=[]
     for p,cc,kk,qq in m.spend(c,k,q,1):
      max_injury_after_tax=max(max_injury_after_tax,kk)
      if kk>=7 or t==1:terminal[flags]+=weight*p;continue
      aa=0 if reset and a<3 else a;bb=0 if reset and b<3 else b
      nf=flags|4 if flags&2 else flags
      for hh,w in HANDS[3 if kk>=4 else 4]:branches.append((p*w,(aa,bb,target,cc,kk,qq,t-1,u,hh),nf,'回合税与新手牌'))
    else:
     rest=list(h);rest.remove(d);rest=tuple(rest);branches=[]
     for grade,(p,(gain,dc)) in enumerate(zip(ODDS(d,0 if 1<=k<=3 and q else 1),PAY[act])):
      if not p:continue
      aa=min(3,a+gain) if route==0 else a;bb=min(3,b+gain) if route==1 else b
      reveal=route==2 and grade==2 or aa==3 or bb==3
      worlds=[(1.,target)] if target>=0 else [(0.5,0),(0.5,1)] if reveal else [(1.,-1)]
      nf=flags|1 if weak else flags
      if weak and grade==2:nf|=2
      if t==1 and route in (0,1) and flags&4:nf|=8
      for z,cc,kk,qq in m.spend(c,k,q,-dc):
       for w,tr in worlds:branches.append((p*z*w,(aa,bb,tr,cc,kk,qq,t,u,rest),nf,('坏','中','好')[grade]))
    for probability,child,nf,outcome in branches:
     layers[(child[6],len(child[8]))][(child,nf)]+=weight*probability
     if child not in parents:parents[child]=(s,ch,outcome)
 # terminal可能也在层中处理，所有概率应合1
 total=sum(terminal.values());assert abs(total-1)<1e-10,total
 return dict(reset=reset,reachable_public_states=len(states),ever_weak_impossible_probe=sum(w for f,w in terminal.items() if f&1),weak_reveal_carried_and_used=sum(w for f,w in terminal.items() if f&8),event_expectations=dict(eventmass),terminal_mass=total,witnesses=witnesses,wounded_round_numbers=sorted(set(3-s[6] for s in states if s[4]>0)),max_reachable_injury_before_terminal=max(s[4] for s in states),max_injury_after_tax=max_injury_after_tax,scope='自由λ0策略下模型路径概率；弱骰定义为1至3；投资状态定义为剩骰即使全好也不能在当回合搜满任一处；非真人频率')
def main():
 results=[analyze(reset) for reset in (True,False)]
 (HERE/'可达投资.json').write_text(json.dumps(dict(results=results),ensure_ascii=False,indent=2)+'\n')
 for r in results:print({k:v for k,v in r.items() if k!='witnesses'},flush=True)
 for r in results:
  print('reset',r['reset'],'witnesses')
  for w in r['witnesses'][:3]:print(w)
if __name__=='__main__':main()
