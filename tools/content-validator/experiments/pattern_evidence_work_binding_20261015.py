from fractions import Fraction as F
from functools import lru_cache
Q=F(3,4)
def posterior(a,b): return F(3**a,3**a+3**b)
def terminal(a,b,g,wa,wb):
 p=posterior(a,b)
 def claim(t):
  correct=10 if g+(wa if t=='A' else wb)>=2 else 4
  prob=p if t=='A' else 1-p
  return prob*correct+(1-prob)*(-5)
 return max((F(3),'SAFE'),(claim('A'),'CLAIM_A'),(claim('B'),'CLAIM_B'))
def solve(menu='WAI',horizon=3):
 @lru_cache(None)
 def dp(a,b,n,g,wa,wb):
  if n==0:return terminal(a,b,g,wa,wb)
  outcomes=[]
  if 'W' in menu:outcomes.append((dp(a,b,n-1,g+1,wa,wb)[0],'W'))
  for code,available in [('IA','A'),('IB','I')]:
   if available not in menu:continue
   p=posterior(a,b); pa=p*Q+(1-p)*(1-Q)
   da=int(code=='IA');db=int(code=='IB')
   gain=pa*dp(a+1,b,n-1,g,wa+da,wb+db)[0]+(1-pa)*dp(a,b+1,n-1,g,wa+da,wb+db)[0]
   outcomes.append((gain,code))
  return max(outcomes)
 return dp
if __name__=='__main__':
 roots={name:solve(name)(0,0,3,0,0,0)[0] for name in ('W','AI','WAI')}
 assert roots=={'W':F(3),'AI':F(451,64),'WAI':F(451,64)},roots
 d=solve('WAI')
 assert d(1,1,1,0,1,1)[0]==F(37,8)
 assert d(2,0,1,0,2,0)[0]==F(17,2)
 print('PASS exact posterior DP and generic-work strong comparison',roots)
