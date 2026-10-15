"""CROSS-SCALE08: pending-work penalty does not force replanning."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
ACTS=('A','B','SIDE')
def solve(penalty=True,early_window=True):
 def legal(seq):
  return not early_window or all(t<3 or a!='A' for t,a in enumerate(seq))
 def score(seq,kind):
  a,b,s=seq.count('A'),seq.count('B'),seq.count('SIDE')
  ra,rb=(12,5) if kind=='A' else (5,12)
  debt=3*((a==1)+(b==1)) if penalty else 0
  return ra*(a>=2)+rb*(b>=2)+s-debt
 fixed=[(F(score(s,'A')+score(s,'B'),2),s) for s in product(ACTS,repeat=5) if legal(s)]
 @lru_cache(None)
 def best(prefix,kind):
  if len(prefix)==5:return score(prefix,kind),prefix
  return max(best(prefix+(a,),kind) for a in ACTS if legal(prefix+(a,)))
 dynamic=max((F(best(pre,'A')[0]+best(pre,'B')[0],2),pre,best(pre,'A'),best(pre,'B'))
             for pre in product(ACTS,repeat=2))
 return max(fixed),dynamic,len(fixed)
if __name__=='__main__':
 for p,d in product((True,False),repeat=2):
  fixed,dynamic,n=solve(p,d)
  assert fixed[0]==dynamic[0]==18
  print('penalty',p,'window',d,'fixed',fixed,'dynamic',dynamic,'legal',n)
 print('CROSS-SCALE08 PASS')
