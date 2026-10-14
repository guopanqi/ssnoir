from itertools import product
from fractions import Fraction
def score(s,t):
 m=a=b=c=v=0
 for i,x in enumerate(s):
  if x=="M":m=1-m
  elif x=="A":a+=1
  elif x=="S":v+=1
  elif x=="B":
   if m and i and s[i-1]=="A":c+=1
   else:b+=1
 return 6*(a>=2)+(8+6*(t=="B"))*(b>=2)+(10+6*(t=="C"))*(c>=2)+v
paths=["".join(z) for z in product("ABSM",repeat=5)]
fixed=max((Fraction(score(s,"B")+score(s,"C"),2),s) for s in paths)
adaptive=max((Fraction(max(score(s,"B") for s in paths if s[:2]==p)+max(score(s,"C") for s in paths if s[:2]==p),2),p) for p in {s[:2] for s in paths})
assert fixed[0]==adaptive[0]==19
print(fixed,adaptive)
