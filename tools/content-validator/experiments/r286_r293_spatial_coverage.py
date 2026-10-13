#!/usr/bin/env python3
"""R286-R293: positional option value in a tiny observation network.

A target among four cells moves with a public Markov kernel. Player
chooses one of three overlapping observation sites before the new target
is realized, sees actual target AFTER choice, and may move at most 1 site
per step. Exact backward DP vs independent forward policy distributions
and every legal precommitted path.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

COVER=((0,1),(1,2),(2,3))
T=5
P=F(1,2)
def trans(last,jump):
 if last==-1:return {e:F(1,4) for e in range(4)}
 assert P+jump<=1
 neigh=(1,) if last==0 else (2,) if last==3 else (last-1,last+1)
 out={last:P,3-last:jump}
 for x in neigh:out[x]=out.get(x,F(0))+(1-P-jump)/len(neigh)
 return {x:p for x,p in out.items() if p}
def moves(site,free=False):
 return tuple(range(3)) if free else tuple(x for x in range(3) if abs(x-site)<=1)
def model(jump=F(1,4),free=False):
 @lru_cache(None)
 def V(t,site,last):
  if t==T:return F(0)
  return max(Q(t,site,last,new) for new in moves(site,free))
 def Q(t,site,last,new):
  return sum(prob*(int(e in COVER[new])+V(t+1,new,e)) for e,prob in trans(last,jump).items())
 witnesses=[]
 mass={(1,-1):F(1)}
 for t in range(T):
  following={}
  for (site,last),weight in mass.items():
   vals={str(new):Q(t,site,last,new) for new in moves(site,free)}
   best=max(vals.values())
   strict=[new for new in moves(site,free) if vals[str(new)]==best]
   if len(strict)==1:
    witnesses.append({"t":t,"site":site,"last":last,"mass":weight,"Q":vals,"best":strict[0]})
   chosen=strict[0]
   for e,p in trans(last,jump).items():
    key=(chosen,e)
    following[key]=following.get(key,F(0))+weight*p
  mass=following
 assert sum(mass.values())==1
 return V(0,1,-1),witnesses
def rollout(jump,rule,free=False):
 # The forward algorithm uses no DP and never peeks at current event before
 # choosing the site. Only LAST event may drive a public policy.
 mass={(1,-1):F(1)}
 total=F(0)
 for t in range(T):
  after={}
  for (site,last),weight in mass.items():
   to=rule(t,site,last,jump)
   assert to in moves(site,free),(t,site,last,to)
   for e,p in trans(last,jump).items():
    prob=weight*p
    total+=prob*int(e in COVER[to])
    after[(to,e)]=after.get((to,e),F(0))+prob
  mass=after
 assert sum(mass.values())==1
 return total
def fixed(jump):
 out={}
 for path in product(range(3),repeat=T):
  if path[0] not in moves(1) or any(path[t] not in moves(path[t-1]) for t in range(1,T)):continue
  out[path]=rollout(jump,lambda t,s,last,j,path=path:path[t])
 best=max(out.values())
 return best,sorted([path for path,val in out.items() if val==best]),len(out)
def myopic(t,site,last,jump):
 dist=trans(last,jump)
 return max(moves(site),key=lambda new:sum(prob*int(e in COVER[new]) for e,prob in dist.items()))
def center_tracking(t,site,last,jump):
 destination=1 if last in (-1,1,2) else 0 if last==0 else 2
 return min(moves(site),key=lambda new:(abs(destination-new),abs(new-1)))
def encode(x):
 if isinstance(x,F):return str(x)
 if isinstance(x,dict):return {str(k):encode(v) for k,v in x.items()}
 if isinstance(x,(tuple,list)):return [encode(y) for y in x]
 return x
def audit():
 data={}
 for name,jump in (("no_jump",F(0)),("rare_jump",F(1,8)),("quarter_jump",F(1,4)),("strong_jump",F(2,5))):
  opt,witness=model(jump)
  static,plans,n=fixed(jump)
  greedy=rollout(jump,myopic)
  center=rollout(jump,center_tracking)
  teleport,_=model(jump,True)
  assert opt>=greedy and opt>=center and teleport>=opt>=static
  data[name]={"jump":jump,"full":opt,"best_fixed_path":static,"fixed_witnesses":plans,
              "number_fixed_paths":n,"myopic":greedy,"center_track":center,
              "teleport":teleport,"full_strict_reachable":witness}
 d=data["quarter_jump"]
 assert (d["full"],d["best_fixed_path"],d["myopic"],d["center_track"],d["teleport"])==(
  F(29857,8192),F(24013,8192),F(28033,8192),F(29857,8192),F(31017,8192))
 assert d["number_fixed_paths"]==99
 assert data["no_jump"]["full"]==data["no_jump"]["teleport"]==F(1991,512)
 assert data["strong_jump"]["full"]==F(1127983,320000)
 assert data["strong_jump"]["center_track"]==F(1095583,320000)
 assert data["strong_jump"]["teleport"]==F(1167183,320000)
 return encode({"schema":"ssnoir.r286-r293.location-options.v1",
 "scope":"exact paper Markov decision on public prior target; not a live game scene or human experiment",
 "rules":"5 turns, 4 target cells, 3 lookout sites cover {0,1},{1,2},{2,3}. Player starts at center, may stay or move one site BEFORE event; observes actual target AFTER move; target has known 1/2 persistence, jump to reflected cell with public probability q, remainder neighboring. Each successful coverage +1.",
 "R286_R289_four_environment_tests":data,
 "R290_independent_all_99_fixed_routes":{k:v["best_fixed_path"] for k,v in data.items()},
 "R291_greedy_and_simple_tracking":{k:{"greedy":v["myopic"],"tracking":v["center_track"]} for k,v in data.items()},
 "R292_teleport_ablation":{k:v["teleport"] for k,v in data.items()},
 "R293_judgement":"A real location/option-value decision is present, but in the main q=1/4 case a simple center-tracking rule is optimal. Movement restriction has zero cost with q=0, becomes relevant with nonlocal jumps. More interesting structural distinction than extra progress clocks, yet remains standard spatial control / prediction, not certified new gameplay."})
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
