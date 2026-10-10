#!/usr/bin/env python3
"""R83: exact paper model of a public opportunity window, not native play."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement,product
from math import factorial
import json
H=tuple(combinations_with_replacement(range(1,7),4))
def odds(d):
 b,m={2:(2,3),3:(1,3),4:(1,2),5:(0,2),6:(0,1)}.get(d+1,(0,0))
 return F(b,6),F(m,6),F(6-b-m,6)
def weight(h):
 v=F(24,6**4)
 for d in set(h):v/=factorial(h.count(d))
 return v
def signal(p):
 q=p*F(3,4)+(1-p)*F(1,4)
 return ((q,p*F(3,4)/q),(1-q,p*F(1,4)/(1-q)))
def model(close,policy=None):
 @lru_cache(None)
 def v(a,b,opened,p,h,sig):
  score=3*(p*(a>=3)+(1-p)*(b>=3))
  if not h:return score
  acts=[t for t in (0,1) if (t or opened) and (a,b)[t]<3]
  if policy is not None:
   acts=[t for t in (policy[sig],1-policy[sig]) if t in acts and 3-(a,b)[t]<=2*len(h)][:1]
  if not acts:return score
  return max(sum(prob*v(min(3,a+k) if t==0 else a,min(3,b+k) if t==1 else b,opened,p,h[:i]+h[i+1:],sig) for k,prob in enumerate(odds(d))) for t in acts for i,d in enumerate(h))
 def first(h,t):
  values=[]
  for i,d in enumerate(h):
   rest=h[:i]+h[i+1:];total=F(0)
   for k,pr in enumerate(odds(d)):
    a,b=(k,0) if t==0 else (0,k)
    for sig,(q,p) in enumerate(signal(F(1,4))):
     cont=v(a,b,True,p,rest,sig) if t==0 else (1-close)*v(a,b,True,p,rest,sig)+close*v(a,b,False,p,rest,sig)
     total+=pr*q*cont
   values.append(total)
  return max(values)
 return {h:(first(h,0),first(h,1)) for h in H}
def mean(t):return round(float(sum(weight(h)*t[h] for h in H)),9)
def main():
 t=model(F(1,2));off=model(F(0));simple=[model(F(1,2),p) for p in product((0,1),repeat=2)]
 best={h:max(t[h]) for h in H};no={h:max(off[h]) for h in H}
 s={h:max(v for tab in simple for v in tab[h]) for h in H}
 a=[h for h in H if t[h][0]>t[h][1]];b=[h for h in H if t[h][1]>t[h][0]]
 flips=[h for h in H if t[h][0]>t[h][1] and off[h][0]<=off[h][1]]
 assert len(H)==126 and sum(map(weight,H))==1 and a and b and flips
 assert all(s[h]==best[h] and no[h]>=best[h] for h in H)
 out=dict(id='R83',scope='exact paper only; 4d6, prior A 1/4, signal accuracy 3/4, first B closes A with probability 1/2; each goal 3; reward 3',withWindow=mean(best),withoutWindow=mean(no),windowLoss=mean({h:no[h]-best[h] for h in H}),alwaysA=mean({h:t[h][0] for h in H}),alwaysB=mean({h:t[h][1] for h in H}),simpleSignalPriority=mean(s),beyondSimple=mean({h:best[h]-s[h] for h in H}),strictA=len(a),strictB=len(b),weightedB=mean({h:F(h in b) for h in H}),closureFlips=len(flips),witnessA=dict(hand=flips[0],withWindow=str(t[flips[0]][0]-t[flips[0]][1]),withoutWindow=str(off[flips[0]][0]-off[flips[0]][1])),witnessB=dict(hand=b[0],BminusA=str(t[b[0]][1]-t[b[0]][0])))
 print(json.dumps(out,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
