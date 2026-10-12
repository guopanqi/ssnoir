#!/usr/bin/env python3
"""R253–R260 exact irreversible intermediate checkpoint; paper only."""
import json
from fractions import Fraction as F
from itertools import permutations
OPS=("+1","-1","M")
def outputs(p,k):
 x=2; hist=[]
 for a in p:
  x=x+1 if a=="+1" else x-1 if a=="-1" else x*k
  hist.append(x)
 return hist[1],hist[2]
def payoff(p,k,cp=3,fin=4,checkpoint=True):
 mid,end=outputs(p,k)
 return int(checkpoint and mid==cp)+int(end==fin)
def full(cp=3,fin=4,checkpoint=True):
 choices={}
 for first in OPS:
  suffixes=list(permutations(x for x in OPS if x!=first))
  choices[first]=sum(F(max(payoff((first,)+s,k,cp,fin,checkpoint) for s in suffixes),2) for k in (2,3))
 return max(choices.values()),choices
def fixed(cp=3,fin=4,checkpoint=True):
 vals={p:sum(F(payoff(p,k,cp,fin,checkpoint),2) for k in (2,3)) for p in permutations(OPS)}
 best=max(vals.values())
 return best,[list(k) for k,v in vals.items() if v==best]
def independently_enumerate(cp=3,fin=4,checkpoint=True):
 # Independently check all 3*2*2 contingent policies.
 vals=[]
 for first in OPS:
  rest=[x for x in OPS if x!=first]
  for p2 in permutations(rest):
   for p3 in permutations(rest):
    vals.append((F(payoff((first,)+p2,2,cp,fin,checkpoint)+payoff((first,)+p3,3,cp,fin,checkpoint),2),
                 (first,)+p2,(first,)+p3))
 best=max(v for v,*_ in vals)
 return best,[{'mode2':a,'mode3':b} for v,a,b in vals if v==best]
def encoded(x):
 if isinstance(x,F):return str(x)
 if isinstance(x,dict):return {str(k):encoded(v) for k,v in x.items()}
 if isinstance(x,(list,tuple)):return [encoded(y) for y in x]
 return x
def audit():
 adaptive,first=full()
 blind,seq=fixed()
 independent,winners=independently_enumerate()
 no_cp,no_first=full(checkpoint=False)
 no_cp_fixed,_=fixed(checkpoint=False)
 oracle=sum(F(max(payoff(p,k) for p in permutations(OPS)),2) for k in (2,3))
 assert (adaptive,blind,independent,no_cp,no_cp_fixed,oracle)==(F(3,2),1,F(3,2),1,F(1,2),2)
 assert first=={'+1':F(1,2),'-1':F(3,2),'M':F(1)}
 assert all(x['mode2'][0]=='-1' and x['mode3'][0]=='-1' for x in winners)
 variants={}
 for cp,fin in ((2,3),(3,4),(5,4),(5,6)):
  a,_=full(cp,fin);b,_=fixed(cp,fin);e,_=independently_enumerate(cp,fin)
  assert (a,b,e)==(F(3,2),1,F(3,2))
  variants[f"{cp}:{fin}"]={'feedback':a,'fixed':b}
 truth={str(k):{"/".join(p):{'intermediate':outputs(p,k)[0],'final':outputs(p,k)[1],'score':payoff(p,k)} for p in permutations(OPS)} for k in (2,3)}
 return encoded({'schema':'ssnoir.r253-r260.irreversible-checkpoint.v1','scope':'exact paper only',
 'rules':'x0=2; +1,-1,M each exactly once. M=2/3 equal, revealed after first operation; checkpoint after second scores 1 if x=3, immutable; final scores 1 if x=4.',
 'R253_optimum':adaptive,'R254_best_fixed':{'value':blind,'plans':seq},
 'R255_all_contingent_strategies':{'value':independent,'winners':winners},
 'R256_real_first_step_values':first,
 'R257_disable_midpoint_reward':{'full':no_cp,'best_fixed':no_cp_fixed},
 'R258_oracle_reveal_before_first':oracle,
 'R259_transfer_distinct_checkpoints':variants,
 'R260_all_trajectories':truth,
 'judgement':'Trajectory objective matters but no continuous novel activity is proven: begin -1 then use the known M to rearrange. Combine R228 timely information with milestone targets; do not promote.'})
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
