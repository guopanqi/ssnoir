#!/usr/bin/env python3
"""R261–R268: does signalling create a new bluffing game, or a bank/spend loop?

Announce A. H=actually strike A, F=feint and strike B. Defender follows
the announcement with probability determined by public credibility 0/1/2.
H rebuilds credibility; F consumes it. Exact expected success fractions.
"""
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
T=5
RELIABILITY=(F(1,2),F(3,4),F(1))
def after(c,action):return min(2,c+1) if action=='H' else max(0,c-1)
def now(c,action):return 1-RELIABILITY[c] if action=='H' else RELIABILITY[c]
@lru_cache(None)
def value(t,c):
 if t==T:return F(0)
 return max(now(c,a)+value(t+1,after(c,a)) for a in 'HF')
def plan(seq):
 c=2;total=F(0)
 for a in seq:total+=now(c,a);c=after(c,a)
 return total
def readable(which):
 c=2;total=F(0);sequence=''
 for t in range(T):
  if which=='all-feint':a='F'
  elif which=='alternate':a='F' if t%2==0 else 'H'
  elif which=='restore':a='H' if c==0 else 'F'
  elif which=='myopic':a=max('HF',key=lambda x:now(c,x))
  else:raise ValueError(which)
  sequence+=a;total+=now(c,a);c=after(c,a)
 return total,sequence
def encode(x):
 if isinstance(x,F):return str(x)
 if isinstance(x,dict):return {str(k):encode(v) for k,v in x.items()}
 if isinstance(x,(list,tuple)):return [encode(y) for y in x]
 return x
def audit():
 schedules={''.join(p):plan(p) for p in product('HF',repeat=T)}
 fixed=max(schedules.values())
 root=value(0,2)
 simple={s:readable(s) for s in ('all-feint','alternate','restore','myopic')}
 assert root==fixed==F(7,2) and simple['restore'][0]==root
 assert simple['alternate'][0]==root and simple['myopic'][0]==root
 assert simple['all-feint'][0]==F(13,4)
 q={str(t):{str(c):{'H':now(c,'H')+value(t+1,after(c,'H')),
                     'F':now(c,'F')+value(t+1,after(c,'F'))} for c in range(3)} for t in range(T)}
 return encode({'schema':'ssnoir.r261-r268.credibility.v1',
 'scope':'abstract paper and fully public credibility, not hidden NPC learning',
 'rules':'Each of 5 turns announce A; choose honest attack A or feint attack B. Guard follows A with p=(1/2,3/4,1) at public credibility 0,1,2. H raises and F lowers credibility by one. Success gains one; public results.',
 'R261_full_public_DP':root,
 'R262_exhaustive_all_fixed_plans':{'best':fixed,'winners':[k for k,v in schedules.items() if v==fixed], 'count':len(schedules)},
 'R263_independent_readable_strategies':{s:{'value':v,'sequence':seq} for s,(v,seq) in simple.items()},
 'R264_disable_credibility_cost':F(T),
 'R265_disable_signal_prediction':F(T,2),
 'R266_all_Q_values':q,
 'R267_reachable_cycle':'FFHFH reaches optimal and deliberately repairs trust after spending it; FHFHF also reaches optimum.',
 'R268_novelty':'Public credibility is simply a bank/spend resource. The strongest fixed plan and multiple short readable rules achieve full optimum; no novel hidden-inference or repeated bluff decision established.'})
if __name__=='__main__':print(json.dumps(audit(),ensure_ascii=False,indent=2))
