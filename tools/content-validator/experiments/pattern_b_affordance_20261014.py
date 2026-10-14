#!/usr/bin/env python3
"""EDGE-B exact six-day A->B permission model. Author math, not a human test."""
from fractions import Fraction as F
from functools import lru_cache
from collections import defaultdict

# 0=R,1=B,2=none; one action each day; public type is Markov persistent 3/4.
# High/low public independent 1/2. NORMAL 6/3, SPECIAL 14/4 if prepared.
# PREP_R/B consumes a day and stores a single matching permit. TOKEN uses it
# up on SPECIAL; LICENSE keeps it. SIDE 1 (dominated by NORMAL).
ORDER=('SPECIAL','NORMAL','PREP_R','PREP_B','SIDE')
def solve(n=6,q=F(3,4),token=True,prep=True,retool=True,early=True,short=False):
    @lru_cache(None)
    def V(t,prev,mode):
        if t==n:return F(0)
        types=((0,F(1,2)),(1,F(1,2))) if prev==2 else ((prev,q),(1-prev,1-q))
        return sum((p*((Q(t,s,mode,0)[0]+Q(t,s,mode,1)[0])/2 if early else Q(t,s,mode,None)[0]) for s,p in types),F(0))
    def options(t,s,mode,high):
        o={'NORMAL':F(6 if high else 3)+V(t+1,s,mode),'SIDE':F(1)+V(t+1,s,mode)}
        if mode==s:o['SPECIAL']=F(14 if high else 4)+V(t+1,s,2 if token else mode)
        if prep and (retool or mode==2):
            if mode!=0:o['PREP_R']=V(t+1,s,0)
            if mode!=1:o['PREP_B']=V(t+1,s,1)
        return o
    @lru_cache(None)
    def Q(t,s,mode,high):
        if high is None:
            a,b=options(t,s,mode,0),options(t,s,mode,1)
            o={name:(v+b[name])/2 for name,v in a.items()}
        else:o=options(t,s,mode,high)
        if short:
            if mode==s and (high or t==n-1):action='SPECIAL'
            elif mode==2 and not high and t<n-1:action='PREP_R' if s==0 else 'PREP_B'
            else:action='NORMAL'
            assert action in o
            return o[action],action,o
        top=max(o.values())
        action=next(name for name in ORDER if name in o and o[name]==top)
        return top,action,o
    return V(0,2,2),Q

def reach():
    root,Q=solve();cur={(2,2):F(1)};observed=[]
    for t in range(6):
        nxt=defaultdict(F)
        for (prev,mode),mass in cur.items():
            types=((0,F(1,2)),(1,F(1,2))) if prev==2 else ((prev,F(3,4)),(1-prev,F(1,4)))
            for s,prob in types:
                for high in (0,1):
                    value,a,o=Q(t,s,mode,high);w=mass*prob/2
                    observed.append((t,mode,s,high,w,a,o))
                    new=0 if a=='PREP_R' else 1 if a=='PREP_B' else 2 if a=='SPECIAL' else mode
                    nxt[(s,new)]+=w
        assert sum(nxt.values())==1
        cur=nxt
    return root,observed

def main():
    root=solve()[0]
    assert root==F(1977459,65536)
    assert solve(prep=False)[0]==27
    assert solve(retool=False)[0]==root
    assert solve(token=False)[0]==F(37161,1024)
    assert solve(early=False)[0]==27
    assert solve(short=True)[0]==root
    actual,steps=reach();assert actual==root
    initial={(s,h):a for t,m,s,h,w,a,o in steps if t==0}
    assert initial=={(0,0):'PREP_R',(1,0):'PREP_B',(0,1):'NORMAL',(1,1):'NORMAL'}
    assert all(a=='NORMAL' for t,m,s,h,w,a,o in steps if w and m==s and h==0 and t<5)
    assert all(a=='SPECIAL' for t,m,s,h,w,a,o in steps if w and m==s and (h==1 or t==5))
    print('TOKEN',root,'no PREP',27,'LICENCE',solve(token=False)[0],
          'late severity',solve(early=False)[0], 'no retool',solve(retool=False)[0],
          'short rule',solve(short=True)[0])
    for d in range(4,9):print('days',d,'token',solve(n=d)[0],'no PREP',solve(n=d,prep=False)[0], 'LICENCE',solve(n=d,token=False)[0])
    for q in (F(1,2),F(3,4),F(7,8)):
        print('repeat',q,'token',solve(q=q)[0],'minus no-retool',solve(q=q)[0]-solve(q=q,retool=False)[0])
    for t in range(6):
        acts=defaultdict(F)
        for day,m,s,h,p,a,o in steps:
            if day==t:acts[a]+=p
        print('day',t,'optimal action mass',dict(acts))
    print('PASS: Fraction DP, strong simple rule, reachability and ablations')
if __name__=='__main__':main()
