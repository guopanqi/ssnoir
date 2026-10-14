#!/usr/bin/env python3
"""ROUTE01-04: reassigning an in-flight background worker.

Five player turns. Public demand A/B equally likely, revealed before t2.
A/B need 4 units, each gives base4 +20 if current demand targets it;
SIDE gives 1. START_A/START_B spends one turn to start a worker that
produces one unit on each of the next THREE player actions. SWITCH_
consumes an action and redirects remaining worker output; a locked
worker cannot switch; an auto-follow machine uses the player's WORK
focus for free. Once three automated ticks have elapsed, no more work.
Strong initial waiting and prewritten scripts are both compared.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('START_A','START_B','SWITCH_A','SWITCH_B','WORK_A','WORK_B','SIDE')
INIT=(0,0,0,'-',0)
MODES=('redirect','locked','auto_follow')

def legal(st,act,mode='redirect'):
    a,b,side,owner,ticks=st
    if act=='SIDE' or act.startswith('WORK'):return True
    if act.startswith('START'):return owner=='-'
    if act.startswith('SWITCH'):
        return mode!='locked' and owner!='-' and ticks<3 and act[-1]!=owner
    return False

def step(st,act,mode='redirect'):
    a,b,side,owner,ticks=st
    assert legal(st,act,mode)
    if act.startswith('START'):
        owner=act[-1]
    else:
        if act.startswith('SWITCH'):owner=act[-1]
        if act=='WORK_A':a+=1
        if act=='WORK_B':b+=1
        if act=='SIDE':side+=1
        if owner!='-' and ticks<3:
            if mode=='auto_follow' and act in ('WORK_A','WORK_B'):
                owner=act[-1]
            if owner=='A':a+=1
            else:b+=1
            ticks+=1
    return a,b,side,owner,ticks

def terminal(st,demand):
    a,b,side,_,_=st
    return F((4+20*(demand=='A'))*(a>=4)+
             (4+20*(demand=='B'))*(b>=4)+side)

def solve(mode='redirect',signal=2,turns=5):
    @lru_cache(None)
    def dp(t,st,demand):
        if t==signal and demand=='?':
            return (dp(t,st,'A')+dp(t,st,'B'))/2
        if t==turns:return terminal(st,demand)
        return max(dp(t+1,step(st,ac,mode),demand)
                   for ac in ACT if legal(st,ac,mode))
    return dp(0,INIT,'?'),dp

def fixed(mode='redirect',turns=5):
    best=None;bestseq=[]
    for seq in product(ACT,repeat=turns):
        st=INIT
        for ac in seq:
            if not legal(st,ac,mode):ac='SIDE'
            st=step(st,ac,mode)
        expected=(terminal(st,'A')+terminal(st,'B'))/2
        if best is None or expected>best:best=expected;bestseq=[seq]
        elif expected==best:bestseq.append(seq)
    return best,tuple(bestseq)

def witness(mode,plan_a,plan_b):
    outcomes=[]
    for demand,seq in (('A',plan_a),('B',plan_b)):
        st=INIT
        for ac in seq:
            st=step(st,ac,mode)
        outcomes.append((demand,seq,terminal(st,demand),st))
    return sum(x[2] for x in outcomes)/2,outcomes

def main():
    roots={m:solve(m)[0] for m in MODES}
    assert roots=={'redirect':F(53,2),'locked':26,'auto_follow':27}
    assert [fixed(m)[0] for m in MODES]==[17,17,17]
    can_redirect,redirect_branches=witness('redirect',
        ('SIDE','START_A','WORK_A','SIDE','SIDE'),
        ('SIDE','START_A','SWITCH_B','WORK_B','SIDE'))
    cannot_redirect,locked_branches=witness('locked',
        ('SIDE','SIDE','START_A','WORK_A','WORK_A'),
        ('SIDE','SIDE','START_B','WORK_B','WORK_B'))
    freely_follow,follow_branches=witness('auto_follow',
        ('SIDE','START_A','WORK_A','SIDE','SIDE'),
        ('SIDE','START_A','WORK_B','SIDE','SIDE'))
    assert can_redirect==roots['redirect']
    assert cannot_redirect==roots['locked']
    assert freely_follow==roots['auto_follow']
    assert [x[2] for x in redirect_branches]==[27,26]
    assert [x[2] for x in locked_branches]==[26,26]
    assert [x[2] for x in follow_branches]==[27,27]
    print('ROUTE dynamic',roots,'fixed',[fixed(m)[0] for m in MODES])
    print('ROUTE conditional',redirect_branches)
    print('ROUTE safe waiting bypass',locked_branches)
    print('ROUTE automatic work-following',follow_branches)
    print('PASS reassignment and safe-waiting control; no new agent autonomy')

if __name__=='__main__':main()
