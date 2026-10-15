"""CROSS-SCALE09: claimed first-stage work has a second-stage maintenance action."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
ACTIONS=('A','B','C','PAY','SIDE')
def solve(maintenance=True):
 def legal(seq):
  return all((t<2 or a!='A') and
   (a!='PAY' or (t==2 and seq[:2].count('A')==2))
   for t,a in enumerate(seq))
 def score(seq,kind):
  a,b,c,s=map(seq.count,('A','B','C','SIDE'))
  realized_a=9*(a>=2)*(not maintenance or seq[2]=='PAY')
  return realized_a+7*(b>=2)+(14 if kind=='H' else 2)*(c>=2)+s
 fixed=[(F(score(s,'H')+score(s,'L'),2),s)
       for s in product(ACTIONS,repeat=5) if legal(s)]
 @lru_cache(None)
 def best(prefix,kind):
  if len(prefix)==5:return score(prefix,kind),prefix
  return max(best(prefix+(a,),kind) for a in ACTIONS if legal(prefix+(a,)))
 contingent=max((F(best(pre,'H')[0]+best(pre,'L')[0],2),
                 pre,best(pre,'H'),best(pre,'L'))
                for pre in product(ACTIONS,repeat=2))
 return max(fixed),contingent,len(fixed)
if __name__=='__main__':
 for maintenance,expected in ((True,(F(17),F(39,2))),
                              (False,(F(18),F(41,2)))):
  fixed,contingent,n=solve(maintenance)
  assert (fixed[0],contingent[0])==expected
  assert contingent[1]==('A','A')
  print('maintenance',maintenance,'fixed',fixed,'conditional',contingent,'valid',n)
 print('CROSS-SCALE09 PASS')
