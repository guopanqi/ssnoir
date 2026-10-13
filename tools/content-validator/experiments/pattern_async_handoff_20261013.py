#!/usr/bin/env python3
"""HAND01-08: conditional async delegation vs robot and explicit receipt.

Player A progress 3 ->12, B progress 2 ->9, SIDE ->1.
SEND costs a slot; helper good/bad equiprobable, adding 2/1 work
units automatically over the next two actions. The helper quality
may be publicly reported after 0..3 subsequent actions. Progress
before report is hidden from the player. ACCEPT in the audited mode
costs a full action after at least two subsequent actions; without
ACCEPT no helper work counts at the finish. This audit cannot be
replaced by a payout penalty, and unaccepted A work remains own work.
Exactly the same transitions also describe a generic automatic tool;
agent motivation, consent and reliability learning are NOT modeled.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('A','B','SIDE','SEND','ACCEPT')
INIT=(0,0,0,-1,'?',False) # own A, B, side, age, public quality, accepted

def available(st,ac,receipt=False,helper=True):
    if ac=='SEND':return helper and st[3]<0
    if ac=='ACCEPT':return receipt and st[3]>=2 and not st[5]
    return ac in ('A','B','SIDE')

def step(st,ac,quality=None):
    a,b,s,age,q,accepted=st
    if ac=='SEND':
        if age>=0:raise ValueError('one-use helper')
        return a,b,s,0,q,False
    if ac=='A':a+=1
    elif ac=='B':b+=1
    elif ac=='SIDE':s+=1
    elif ac=='ACCEPT':
        if age<2 or accepted:raise ValueError('no delivered work to accept')
        accepted=True
    else:raise ValueError(ac)
    if age>=0:age+=1
    return a,b,s,age,q,accepted

def payoff(st,quality,receipt):
    own_a,b,s,age,public_q,accepted=st
    received=0 if age<0 or (receipt and not accepted) else min(age,2 if quality=='good' else 1)
    return F(12*int(own_a+received>=3)+9*int(b>=2)+s)

def solve(turns=4,receipt=False,report_delay=0,helper=True):
    @lru_cache(None)
    def dp(t,st):
        if t==turns:
            if st[4]=='?':return (payoff(st,'good',receipt)+payoff(st,'bad',receipt))/2
            return payoff(st,st[4],receipt)
        values=[]
        for ac in ACT:
            if not available(st,ac,receipt,helper):continue
            nxt=step(st,ac)
            if nxt[3]>=0 and nxt[3]>=report_delay and nxt[4]=='?':
                value=sum(dp(t+1,nxt[:4]+(q,)+nxt[5:]) for q in ('good','bad'))/2
            else:value=dp(t+1,nxt)
            values.append((value,ac))
        return max(v for v,_ in values)
    return dp(0,INIT),dp

def fixed(turns=4,receipt=False,helper=True):
    # Prewritten illegal action -> useful SIDE, never a quality-dependent replacement.
    best=None
    witnesses=[]
    for seq in product(ACT,repeat=turns):
        branches=[]
        for q in ('good','bad'):
            st=INIT
            for ac in seq:
                if not available(st,ac,receipt,helper):ac='SIDE'
                st=step(st,ac)
            branches.append(payoff(st,q,receipt))
        value=sum(branches)/2
        if best is None or value>best:best=value;witnesses=[seq]
        elif value==best:witnesses.append(seq)
    return best,witnesses

def plan(plan,quality,receipt=False):
    st=INIT
    for ac in plan:
        assert available(st,ac,receipt)
        st=step(st,ac)
    return payoff(st,quality,receipt)

def main():
    no_helper=solve(helper=False)[0]
    instant=solve()[0]
    delays=[solve(report_delay=d)[0] for d in range(5)]
    fixed4=fixed()[0]
    with_receipt4=solve(receipt=True)[0]
    with_receipt5=solve(turns=5,receipt=True)[0]
    passive5=solve(turns=5)[0]
    alone5=solve(turns=5,helper=False)[0]
    assert (no_helper,instant,fixed4)==(13,17,15)
    assert delays==[17,17,F(33,2),15,15],delays
    assert (with_receipt4,with_receipt5,passive5,alone5)==(13,21,F(43,2),21)
    assert plan(('SEND','A','B','B'),'good')==21
    assert plan(('SEND','A','A','SIDE'),'bad')==13
    assert plan(('A','SEND','B','B'),'good')==21
    assert plan(('A','SEND','A','SIDE'),'bad')==13
    # When quality is known only after the second follow-up action:
    # GOOD completes both projects; BAD abandons B to finish A.
    assert plan(('SEND','A','B','B'),'good')==21
    assert plan(('SEND','A','B','A'),'bad')==12
    assert F(21+12,2)==delays[2]
    # By the third follow-up, even the strongest contingent policy
    # cannot improve the prewritten average reward of 15.
    print('HAND four turns no/helper/fixed',no_helper,instant,fixed4)
    print('HAND delivery report after k subsequent turns (k=0..4)',delays)
    print('HAND acceptance required four/five turns',with_receipt4,with_receipt5,
          'passive five',passive5,'solo five',alone5)
    print('Robot rename shares identical steps; no external-agent preference was simulated.')

if __name__=='__main__':main()
