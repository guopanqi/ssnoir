from functools import lru_cache
from fractions import Fraction as F
from itertools import product
H=4
def legal(t,loc,mode):
 out=['SIDE']
 if t==0:out.append('TEST')
 if mode=='free':out+=['WORK_A','WORK_B']
 else:
  out+=['WORK_A' if loc=='A' else 'WORK_B']
  out+=['GO_B' if loc=='A' else 'GO_A']
 return out
def move(t,loc,a,b,act,mode):
 if act not in legal(t,loc,mode):raise ValueError(act)
 if act in ('TEST','WORK_A'):a+=1
 elif act=='WORK_B':b+=1
 elif act=='GO_B':loc='B'
 elif act=='GO_A':loc='A'
 return loc,min(2,a),min(2,b)
def payout(a,b,q):
 return (10 if q=='H' else 4)*int(a>=2)+8*int(b>=2)
def solve(mode):
 @lru_cache(None)
 def dp(t,loc,a,b,q):
  if t==H:
   return (F(payout(a,b,'H')+payout(a,b,'L'),2) if q=='?' else F(payout(a,b,q))),'END'
  opts=[]
  for action in legal(t,loc,mode):
   nl,na,nb=move(t,loc,a,b,action,mode)
   if action=='TEST':v=(dp(t+1,nl,na,nb,'H')[0]+dp(t+1,nl,na,nb,'L')[0])/2
   else:v=dp(t+1,nl,na,nb,q)[0]
   opts.append((v+int(action=='SIDE'),action))
  return max(opts)
 acts=['SIDE','TEST','WORK_A','WORK_B'] if mode=='free' else ['SIDE','TEST','WORK_A','WORK_B','GO_A','GO_B']
 plans=[]
 for sequence in product(acts,repeat=H):
  results=[]
  for q in 'HL':
   loc='A';a=b=s=0
   try:
    for t,action in enumerate(sequence):
     loc,a,b=move(t,loc,a,b,action,mode);s+=int(action=='SIDE')
   except ValueError:break
   results.append(payout(a,b,q)+s)
  if len(results)==2:plans.append((F(sum(results),2),sequence))
 return dp(0,'A',0,0,'?'),max(plans),dp
if __name__=='__main__':
 world,fixed,dp=solve('world')
 free,free_fixed,_=solve('free')
 assert world[0]==10 and fixed[0]==9
 assert dp(1,'A',1,0,'H')==(12,'WORK_A')
 assert dp(1,'A',1,0,'L')==(8,'GO_B')
 assert free[0]==free_fixed[0]==15
 print('world',world,'fixed',fixed)
 print('no location costs',free,'fixed',free_fixed)
 print('PASS: four-action complete DP and all fixed schedules')
