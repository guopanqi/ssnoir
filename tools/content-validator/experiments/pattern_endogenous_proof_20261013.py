#!/usr/bin/env python3
"""PRF01-04: genuine-work certificate as a nonconsumable access byproduct.

Three turns; A/B each require two units and award 8/9; SHOW finishes C
for high12/low0. Demand high/low is equiprobable and revealed after turn1.
SIDE pays 1. Exact state DP plus all legal unconditional action scripts.
No human or Unity evidence.
"""
from fractions import Fraction as Q
from functools import lru_cache
from itertools import product

ACTS=('A','B','SIDE','SHOW','ENABLE')
KINDS=('proof','matched','plain','paid','always','b_bundled')

def legal(kind,st,act):
    x,y,s,proof,shown,enabled=st
    if act=='SHOW':return kind!='plain' and not shown and (kind=='always' or proof or enabled)
    if act=='ENABLE':return kind=='paid' and not enabled
    return act in ('A','B','SIDE')

def advance(kind,st,act):
    x,y,s,proof,shown,enabled=st
    if act=='A':x=min(2,x+1);proof|=kind in ('proof','matched')
    if act=='B':y=min(2,y+1);proof|=kind=='b_bundled'
    if act=='SIDE':s+=1
    if act=='SHOW':shown=True
    if act=='ENABLE':enabled=True
    return (x,y,s,proof,shown,enabled)

def terminal(st,high):
    x,y,s,proof,shown,enabled=st
    return Q(8*(x==2)+9*(y==2)+s+(12 if high else 0)*shown)

def solve(kind):
    @lru_cache(None)
    def dp(t,st,high):
        if t==3:return terminal(st,high)
        if t==1 and high is None:return (dp(t,st,True)+dp(t,st,False))/2
        return max(dp(t+1,advance(kind,st,a),high) for a in ACTS if legal(kind,st,a))
    init=(0,0,0,False,False,False)
    root=dp(0,init,None)
    first={a:dp(1,advance(kind,init,a),None) for a in ACTS if legal(kind,init,a)}
    fixed=[]
    for plan in product(ACTS,repeat=3):
        st=init
        for a in plan:
            if not legal(kind,st,a):break
            st=advance(kind,st,a)
        else:fixed.append((sum(terminal(st,h) for h in (False,True))/2,plan))
    best=max(v for v,p in fixed)
    return root,first,best,tuple(p for v,p in fixed if v==best)

def main():
    result={}
    for kind in KINDS:
        root,first,best,plans=solve(kind)
        print(kind,'root',root,'first',first,'fixed',best,'plans',plans[:3])
        result[kind]=(root,best)
    assert result=={
        'proof':(Q(29,2),Q(14)),
        'matched':(Q(29,2),Q(14)),
        'plain':(Q(10),Q(10)),
        'paid':(Q(23,2),Q(10)),
        'always':(Q(31,2),Q(15)),
        'b_bundled':(Q(31,2),Q(15))
    },result
    assert solve('proof')[1]['A']==Q(29,2)
    assert solve('always')[1]['B']==Q(31,2)
    print('PASS genuine A-proof byproduct, generic bundled permit and strong menus')
if __name__=='__main__':main()
