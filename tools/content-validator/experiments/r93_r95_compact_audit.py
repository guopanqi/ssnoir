#!/usr/bin/env python3
"""R93/R95 exact paper counterfactuals. Not native gameplay/human evidence.
4 dice; 126 weighted hands; fixed scripts can choose dice adaptively.
R93: P17-style testimony / check after action 3. R95: P10-style
reserve in two rounds of TWO actions (NOT original three rounds).
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product, combinations_with_replacement
from math import factorial
import json,sys
H=list(combinations_with_replacement(range(1,7),4))
def odds(d):
 b,m={1:(2,3),2:(1,3),3:(1,2),4:(0,2),5:(0,1),6:(0,0)}[d]
 return [(0,F(b,6)),(1,F(m,6)),(2,F(6-b-m,6))]
def w(hand):
 r=F(24,1296)
 for x in set(hand):r/=factorial(hand.count(x))
 return r
assert sum(map(w,H))==1
def mean(f):return float(sum(w(h)*f(h) for h in H))

def r93(check):
 def advance(s,act,die,hand):
  p,f,h=s
  for v,prob in (odds(die) if act==0 else [(0,F(1))]):
   if not prob:continue
   if act==0:t=(min(8,p+v),f,h-(v==0))
   elif act==1:t=(min(8,p+2),min(4,f+1),h)
   else:t=(max(0,p-1),f-1,h)
   if check and len(hand)==2 and t[1]>0:prob/=2
   yield prob,t
 @lru_cache(None)
 def opt(s,hand):
  p,f,h=s
  if h<=0:return F(0)
  if p>=4 and f==0:return F(1)
  if not hand:return F(0)
  return max(sum(prob*opt(t,hand[:i]+hand[i+1:]) for prob,t in advance(s,a,d,hand))
             for a in (0,1,2) if a!=2 or f>0 for i,d in enumerate(hand))
 @lru_cache(None)
 def fixed(s,hand,plan):
  p,f,h=s
  if h<=0:return F(0)
  if p>=4 and f==0:return F(1)
  if not hand:return F(0)
  a=plan[0] if plan[0]!=2 or f>0 else 0
  return max(sum(prob*fixed(t,hand[:i]+hand[i+1:],plan[1:]) for prob,t in advance(s,a,d,hand))
             for i,d in enumerate(hand))
 plans=list(product((0,1,2),repeat=4))
 initial=(0,0,2)
 best={h:opt(initial,h) for h in H}
 base={h:max(fixed(initial,h,pl) for pl in plans) for h in H}
 assert all(best[h]>=base[h] for h in H)
 return {'optimal':mean(lambda h:best[h]),'fixed':mean(lambda h:base[h]),
         'adaptive_gain':mean(lambda h:best[h]-base[h]),
         'strict_root_hands':sum(best[h]>base[h] for h in H)}

def r95(goal,nonlinear,decay):
 def reward(k):return k*(k+1)//2 if nonlinear else 2*k
 def move(s,a,d,hand):
  p,r=s
  for v,prob in (odds(d) if a=='P' else [(0,F(1))]):
   if not prob:continue
   if a=='P':ns=(p,max(0,min(5,r+{0:-2,1:1,2:2}[v])))
   elif a=='C':ns=(min(goal,p+reward(r)),0)
   else:ns=(min(goal,p+1),r)
   if len(hand)==3:ns=(ns[0],max(0,ns[1]-decay))
   yield prob,ns
 def value(s,hand,a,i,cont):
  rest=hand[:i]+hand[i+1:]
  return sum(prob*cont(t,rest) for prob,t in move(s,a,hand[i],hand))
 @lru_cache(None)
 def opt(s,hand):
  p,r=s
  if p>=goal:return F(1)
  if not hand:return F(0)
  return max(value(s,hand,a,i,opt) for a in ('G','P','C') if a!='C' or r>0
             for i in range(len(hand)))
 @lru_cache(None)
 def fixed(s,hand,plan):
  p,r=s
  if p>=goal:return F(1)
  if not hand:return F(0)
  a=plan[0] if plan[0]!='C' or r>0 else 'G'
  return max(value(s,hand,a,i,lambda t,rem:fixed(t,rem,plan[1:]))
             for i in range(len(hand)))
 plans=list(product(('P','C','G'),repeat=4))
 best={h:opt((0,0),h) for h in H}
 base={h:max(fixed((0,0),h,pl) for pl in plans) for h in H}
 assert all(best[h]>=base[h] for h in H)
 @lru_cache(None)
 def forward(s,hand):
  p,r=s
  if p>=goal or not hand:return (F(0),)*8
  vs={(a,i):value(s,hand,a,i,opt) for a in ('G','P','C') if a!='C' or r>0
      for i in range(len(hand))}
  high=max(vs.values())
  a,i=next(k for k,v in vs.items() if v==high)
  same_action_max=max((v for (other,_),v in vs.items() if other!=a),default=F(-1))
  row=[F(0)]*8
  if r>0 and high>same_action_max and a in ('C','P'):
   row[(4-len(hand))*2+(a=='P')]+=1
  rem=hand[:i]+hand[i+1:]
  for prob,t in move(s,a,hand[i],hand):
   child=forward(t,rem)
   for j in range(8):row[j]+=prob*child[j]
  return tuple(row)
 path=[mean(lambda h:forward((0,0),h)[i]) for i in range(8)]
 return {'goal':goal,'nonlinear':nonlinear,'decay':decay,
         'optimal':mean(lambda h:best[h]),'fixed':mean(lambda h:base[h]),
         'adaptive_gain':mean(lambda h:best[h]-base[h]),
         'strict_root_hands':sum(best[h]>base[h] for h in H),
         'normal_path_by_action':[{'action':i+1,'strict_cash':path[2*i],
           'strict_prepare':path[2*i+1]} for i in range(4)]}

def main():
 x={'scope':'Exact Fraction paper models; not native tests or human gameplay',
    'R93':{'without':r93(False),'public_inspection_after_third':r93(True)},
    'R95':[r95(goal,nl,dec) for goal,nl,dec in product((4,6),(True,False),(0,2))]}
 s=json.dumps(x,indent=2,ensure_ascii=False)+'\n'
 if len(sys.argv)>1:open(sys.argv[1],'w',encoding='utf8').write(s)
 else:print(s)
if __name__=='__main__':main()
