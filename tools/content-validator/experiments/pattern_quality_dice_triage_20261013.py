#!/usr/bin/env python3
"""DICE01-04: actual dice quality changes whether to repair or progress.

Exactly four public deterministic dice H2,H2,L1,L1, freely ordered.
A needs 3 work and is worth9, B needs2 worth6, SIDE gives1.
On first A progress, safe or repair-2 crisis is revealed (equiprobable).
Unless repaired before the end, A cannot score. REPAIR reduces severity
by quality, consuming the die; q2 is scarce because it is also efficient
at making progress. Compare adaptive DP, all 6*4**4 precommitted scripts,
no H repair, and quality-independent repair. This is NOT the original R37
or Citizen Sleeper 2 dice/outcome implementation; no human testing.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product,permutations

HAND=(2,2,1,1)
ACTS=('A','B','FIX','SIDE')
INIT=(2,2,0,0,-1,0)  # H dice,L dice,A progress,B progress,hazard,side
A_NEED,B_NEED=3,2
A_REWARD,B_REWARD=9,6

def payoff(st):
    h,l,a,b,hazard,side=st
    return F(A_REWARD*(a>=A_NEED and hazard==0)+B_REWARD*(b>=B_NEED)+side)

def legal(st,act,q):
    assert act in ACTS and q in (1,2)
    available=st[0] if q==2 else st[1]
    return available>0 and (act!='FIX' or st[4]>0)

def step(st,act,q,issue=None,repair='quality'):
    assert legal(st,act,q)
    h,l,a,b,hazard,side=st
    if q==2:h-=1
    else:l-=1
    if act=='A':
        a=min(A_NEED,a+q)
        if hazard==-1:
            assert issue in (0,2)
            hazard=issue
    elif act=='B':b=min(B_NEED,b+q)
    elif act=='FIX':
        hazard=max(0,hazard-(q if repair=='quality' else 1))
    else:side+=1
    return h,l,a,b,hazard,side

def optimal(restrict_high_fix=False,repair='quality'):
    @lru_cache(None)
    def dp(st):
        if st[0]+st[1]==0:return payoff(st),()
        options=[]
        for q in (2,1):
            for act in ACTS:
                if not legal(st,act,q):continue
                if restrict_high_fix and act=='FIX' and q==2:continue
                if act=='A' and st[4]==-1:
                    safe=step(st,act,q,0,repair)
                    risk=step(st,act,q,2,repair)
                    value=(dp(safe)[0]+dp(risk)[0])/2
                else:value=dp(step(st,act,q,repair=repair))[0]
                options.append((value,(act,q)))
        maximum=max(v for v,a in options)
        return maximum,tuple(a for v,a in options if v==maximum)
    return dp(INIT),dp

def fixed(repair='quality'):
    # An illegal prewritten FIX becomes SIDE; this generous comparator
    # doesn't lose the die if the risk has not appeared or is already gone.
    best=None
    count=0
    for dice_order in set(permutations(HAND)):
        for action_order in product(ACTS,repeat=4):
            expected=F(0)
            for hazard in (0,2):
                st=INIT
                for q,act in zip(dice_order,action_order):
                    actual=act if legal(st,act,q) else 'SIDE'
                    issue=hazard if actual=='A' and st[4]==-1 else None
                    st=step(st,actual,q,issue,repair)
                expected+=payoff(st)/2
            count+=1
            if best is None or expected>best[0]:
                best=(expected,(dice_order,action_order))
    assert count==6*4**4
    return best,count

def public_short_policy():
    branches=[]
    for hazard in (0,2):
        st=step(INIT,'A',2,hazard)
        rest=(('B',2),('A',1),('SIDE',1)) if hazard==0 else (
            ('FIX',2),('A',1),('SIDE',1))
        for act,q in rest:st=step(st,act,q)
        branches.append((hazard,payoff(st),rest,st))
    return sum(item[1] for item in branches)/2,branches

def main():
    (adaptive,actions),dp=optimal()
    (no_high_fix,_),_=optimal(restrict_high_fix=True)
    (flat_fix,_),_=optimal(repair='flat')
    fixed_best,n=fixed()
    witness,branches=public_short_policy()
    assert adaptive==witness==13
    assert no_high_fix==flat_fix==F(25,2)
    assert fixed_best[0]==F(23,2)
    assert branches[0][1]==16 and branches[1][1]==10
    assert dp(step(INIT,'A',2,2))[0]==10
    assert dp(step(INIT,'A',2,0))[0]==16
    print('adaptive',adaptive,'opening actions',actions)
    print('fixed',fixed_best,'scripts',n)
    print('no high quality repair',no_high_fix,'quality-independent repair',flat_fix)
    print('SAFE/RISK concrete attainable policies',branches)
    print('PASS finite quality triage, signal contingencies and strong fixed checks')

if __name__=='__main__':main()
