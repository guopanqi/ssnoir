"""ORDER06: one-use costless right to force defender's binding public move.

A separate finite-capacity / timing counterfactual. If the player spends the
right on a round, the adversary first binds HIDE or ESCAPE; player responds,
and the right is consumed. Otherwise actions remain simultaneous. Rights
never replenish; ESCAPE still usable at most once.
"""
from fractions import Fraction as F
from functools import lru_cache
from pattern_action_commitment_order_20261015 import solve,matrix_value

@lru_cache(None)
def optimal(n,escape=True,right=True):
 if n==0:return F(0),('END',)
 if not escape:return F(3*n),('PRESS',)
 if not right:return solve(n,True,'simultaneous'),('NO_RIGHT',)
 h=optimal(n-1,True,True)[0];x=optimal(n-1,False,True)[0]
 ordinary=matrix_value(((F(3)+h,F(-3)+x),(F(-2)+h,F(2)+x)))
 spent_h=solve(n-1,True,'simultaneous')
 spent_x=solve(n-1,False,'simultaneous')
 bind=min(F(3)+spent_h,F(2)+spent_x)
 best=max(ordinary,bind)
 acts=tuple(a for v,a in ((ordinary,'SAVE'),(bind,'USE')) if v==best)
 return best,acts

def immediate(n):
 return min(F(3)+solve(n-1,True,'simultaneous'),
            F(2)+F(3*(n-1)))

def regression():
 for n in range(1,9):
  val,acts=optimal(n)
  baseline=solve(n,True,'simultaneous')
  assert val==immediate(n), ('immediate never inferior',n)
  assert val>=baseline
  if n==1:assert val==2 and acts==('USE',)
  else:
   assert val==F(3*n-6)+F(3,2**(n-2))
   assert val-baseline==F(3,2**(n-1))
   if n>=3:assert acts==('SAVE','USE')  # both optimal
  print('n',n,'with_writ',val,'baseline',baseline,'gain',val-baseline,'actions',acts)
 print('ORDER06 PASS - the right is useful, its timing is not uniquely strategic')

if __name__=='__main__':regression()
