"""Three-target investigative leads: signals unlock targeted verification."""
from fractions import Fraction as F
from functools import lru_cache
T=range(3)
def solve(mode='gated',steps=3):
 @lru_cache(None)
 def dp(n,belief,opened,verified):
  b=list(belief)
  opts=[(F(3),'SAFE')]+[(18*b[i]-6,'CLAIM'+str(i)) for i in T]
  if n:
   for i in T:
    z=F(0)
    for pos in (0,1):
     likelihood=[(F(3,4) if j==i else F(1,4)) if pos else
                 (F(1,4) if j==i else F(3,4)) for j in T]
     prob=sum(b[j]*likelihood[j] for j in T)
     if prob:
      post=tuple(b[j]*likelihood[j]/prob for j in T)
      z+=prob*dp(n-1,post,opened|(1<<i),verified)[0]
    opts.append((z-1,'SCAN'+str(i)))
   if mode!='no_confirm':
    for i in T:
     if not ((opened>>i)&1 or mode=='open') or ((verified>>i)&1):continue
     prob=b[i]
     val=F(0)
     if prob:
      val+=prob*dp(n-1,tuple(F(j==i) for j in T),opened,verified|(1<<i))[0]
     if prob<1:
      val+=(1-prob)*dp(n-1,tuple(F(0) if j==i else b[j]/(1-prob) for j in T),opened,verified|(1<<i))[0]
     opts.append((val-1,'CONFIRM'+str(i)))
  return max(opts,key=lambda item:item[0])
 return dp(steps,(F(1,3),)*3,0,0),dp
if __name__=='__main__':
 expected={'no_confirm':F(251,64),'gated':F(7),'open':F(31,3)}
 for mode,value in expected.items():
  got,_=solve(mode)
  assert got[0]==value,(mode,got)
  print(mode,got)
 _,d=solve('gated')
 assert d(2,(F(3,5),F(1,5),F(1,5)),1,0)[1]=='CONFIRM0'
 assert d(2,(F(1,7),F(3,7),F(3,7)),1,0)[1]=='SCAN1'
 print('PASS five exact regressions')
