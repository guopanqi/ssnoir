#!/usr/bin/env python3
"""TRIG01-04: doing work A may create a repair obligation.

Four actions. A needs two WORK_A for value10, B two WORK_B for value9,
SIDE immediately +1. The first A reveals SAFE/RISK equally likely.
If RISK, A cannot yield its outcome unless FIX has been performed.
FIX costs a full action. Compare exact adaptive DP, public early
information, independent no-risk control and all fixed four-action
scripts. An abstract P11/R37-family model, NOT the original R37 scene.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('A','B','SIDE','FIX')
INIT=(0,0,0,'unseen',False)

def step(st,act,signal=None):
    a,b,side,issue,repaired=st
    if act=='A':
        a=min(a+1,2)
        if issue=='unseen':
            assert signal in ('safe','risk')
            issue=signal
    elif act=='B':b=min(b+1,2)
    elif act=='SIDE':side+=1
    elif act=='FIX':repaired=True
    return a,b,side,issue,repaired

def utility(st):
    a,b,side,issue,repaired=st
    return F(10*(a==2 and (issue!='risk' or repaired))+9*(b==2)+side)

def adapted(signal_known=False,no_issue=False):
    @lru_cache(None)
    def dp(t,st,known):
        if t==4:return utility(st)
        choices=[]
        for act in ACT:
            if act=='A' and st[3]=='unseen':
                ss=('safe',) if no_issue else (known,) if known is not None else ('safe','risk')
                choices.append(sum(dp(t+1,step(st,act,s),known) for s in ss)/len(ss))
            else:choices.append(dp(t+1,step(st,act),known))
        return max(choices)
    if signal_known:
        return (dp(0,INIT,'safe')+dp(0,INIT,'risk'))/2
    return dp(0,INIT,None)

def fixed():
    results=[]
    for plan in product(ACT,repeat=4):
        score=F(0)
        for signal in ('safe','risk'):
            st=INIT
            for action in plan:st=step(st,action,signal)
            score+=utility(st)/2
        results.append((score,plan))
    mx=max(x[0] for x in results)
    return mx,[x[1] for x in results if x[0]==mx]

def contingent_witness():
    vals=[]
    for signal in ('safe','risk'):
        plan=('A','A','B','B') if signal=='safe' else ('A','A','FIX','SIDE')
        st=INIT
        for a in plan:st=step(st,a,signal)
        vals.append((signal,utility(st),plan))
    return sum(x[1] for x in vals)/2,vals

def main():
    d=adapted();fx,plans=fixed()
    early=adapted(signal_known=True);none=adapted(no_issue=True)
    witness,branches=contingent_witness()
    assert d==15 and fx==14 and early==15 and none==19 and witness==d
    assert ('A','A','B','B') in plans
    assert branches[0][1]==19 and branches[1][1]==11
    print('TRIG adaptive',d,'fixed',fx,'early-omniscient',early,
          'risk-disabled',none,'witness',branches)
    print('PASS conditionally repair or pursue B, but R37 pressure family and short policy suffice')

if __name__=='__main__':main()
