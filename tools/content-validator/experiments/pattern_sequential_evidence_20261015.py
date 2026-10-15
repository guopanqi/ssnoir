"""Exact finite Bayesian sample/stop policies. Run: python this_file.py."""
from fractions import Fraction as F
from functools import lru_cache
Q=F(3,4)
def posterior(a,b):return F(3**a,3**a+3**b)
def solve(cost=F(1)):
 @lru_cache(None)
 def dp(a,b,left):
  p=posterior(a,b)
  claim=10*max(p,1-p)-6*min(p,1-p)
  opts=[(claim,"CLAIM"),(F(3),"SAFE")]
  if left:
   pa=p*Q+(1-p)*(1-Q)
   x=-cost+pa*dp(a+1,b,left-1)[0]+(1-pa)*dp(a,b+1,left-1)[0]
   opts.append((x,"SAMPLE"))
  return max(opts,key=lambda x:(x[0],x[1]=="CLAIM"))
 return dp
def fixed_n(cost,n):
 @lru_cache(None)
 def f(a,b,left):
  p=posterior(a,b)
  if not left:return max(F(3),10*max(p,1-p)-6*min(p,1-p))
  pa=p*Q+(1-p)*(1-Q)
  return -cost+pa*f(a+1,b,left-1)+(1-pa)*f(a,b+1,left-1)
 return f(0,0,n)
if __name__=="__main__":
 d=solve()
 assert d(0,0,3)==(F(41,8),"SAMPLE")
 assert d(1,0,2)==(F(49,8),"SAMPLE")
 assert d(2,0,1)==(F(42,5),"CLAIM")
 assert d(1,1,1)==(F(5),"SAMPLE")
 assert [fixed_n(F(1),n) for n in range(4)]==[F(3),F(5),F(35,8),F(9,2)]
 assert solve(F(2))(0,0,3)==(F(4),"SAMPLE")
 assert solve(F(0))(0,0,3)[0]==F(15,2)
 assert solve(F(1,4))(0,0,3)[0]==F(221,32)
 print("PASS: sequential evidence DP, fixed counts and cost controls")
