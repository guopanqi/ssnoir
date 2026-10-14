#!/usr/bin/env python3
"""CUST01-04: proof original must remain in custody for terminal A; its
one-time handover completes C. Copy spends one action yet preserves original.
An optional independent B job can bypass this entire conflict.
Original is non-reproducible; subsequent A actions cannot recreate it.
4 player turns, high/low demand revealed after turn1, Fraction exact DP
plus exhaustive unconditional scripts. No player-experience inference.
"""
from fractions import Fraction as Q
from functools import lru_cache
from itertools import product

ACTS=('A','B','SIDE','SHOW','COPY')
KINDS=('original','copy','matched_preserve','reusable')
# A progress, B progress, side, original held, original ever made, copy, C shown
INIT=(0,0,0,False,False,False,False)

def legal(kind,st,act,b_enabled):
    a,b,s,orig,ever,cp,shown=st
    if act=='B':return b_enabled
    if act=='COPY':return kind in ('copy','matched_preserve') and orig and not cp
    if act=='SHOW':return not shown and (orig or cp)
    return act in ('A','SIDE')

def advance(kind,st,act):
    a,b,s,orig,ever,cp,shown=st
    if act=='A':
        a=min(a+1,2)
        if not ever:orig=True;ever=True
    elif act=='B':b=min(b+1,2)
    elif act=='SIDE':s+=1
    elif act=='COPY':cp=True
    elif act=='SHOW':
        shown=True
        if kind!='reusable':
            if cp:cp=False
            else:orig=False
    return a,b,s,orig,ever,cp,shown

def outcome(st,high):
    a,b,s,orig,ever,cp,shown=st
    return Q((a==2 and orig)*8+(b==2)*9+s+shown*(12 if high else 0))

def solve(kind,b_enabled):
    @lru_cache(None)
    def dp(t,st,sig):
        if t==4:return outcome(st,sig)
        if t==1 and sig is None:return (dp(t,st,True)+dp(t,st,False))/2
        return max(dp(t+1,advance(kind,st,ac),sig)
                   for ac in ACTS if legal(kind,st,ac,b_enabled))
    root=dp(0,INIT,None)
    first={ac:dp(1,advance(kind,INIT,ac),None)
           for ac in ACTS if legal(kind,INIT,ac,b_enabled)}
    fixed=[]
    for plan in product(ACTS,repeat=4):
        st=INIT
        for ac in plan:
            if not legal(kind,st,ac,b_enabled):break
            st=advance(kind,st,ac)
        else:fixed.append((sum(outcome(st,s) for s in (True,False))/2,plan))
    best=max(v for v,p in fixed)
    return root,first,best,[p for v,p in fixed if v==best]

def main():
    for b_enabled in (False,True):
        result={}
        for kind in KINDS:
            root,first,best,plans=solve(kind,b_enabled)
            result[kind]=(root,best)
            print('B?',b_enabled,'kind',kind,'dynamic',root,
                  'first',first,'fixed',best,'fixed_paths',plans[:3])
        assert result['copy']==result['matched_preserve']
        if not b_enabled:
            assert result=={
                'original':(Q(12),Q(10)),
                'copy':(Q(15),Q(14)),
                'matched_preserve':(Q(15),Q(14)),
                'reusable':(Q(31,2),Q(15))
            },result
        else:
            assert all(v==(Q(19),Q(17)) for v in result.values()),result
    print('PASS irreplaceable-original custody, copy-cost and B bypass')
if __name__=='__main__':main()
