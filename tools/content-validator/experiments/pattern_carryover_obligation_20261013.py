#!/usr/bin/env python3
"""CARRY01-04: cross-cycle promise/late follow-up versus urgent work.

Five player slots: two in cycle1, three in cycle2. A needs own WORK_A3
for 12; urgent U appears in cycle2 with probability1/2 and needs
WORK_U2 for 10; SIDE earns1 immediately. Cycle1 PLEDGE consumes one
turn and provisionally finishes A but generates a follow-up burden
requiring 1 or 2 FOLLOW actions during cycle2 (each with probability
1/2, revealed at the cycle boundary). A can be cashed only when all
required FOLLOW is done, or own A work independently reaches3.
The follow-up and urgency signals are not known to cycle1 planning.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
ACT=('A','U','SIDE','PLEDGE','FOLLOW')
INIT=(0,0,0,False,-1) # ownA,urgent progress,side,pledged,follow needed

def legal(st,ac,t,urgent):
    a,u,s,pledged,remaining=st
    if ac in ('A','SIDE'):return True
    if ac=='U':return t>=2 and urgent==1
    if ac=='PLEDGE':return t<2 and not pledged
    if ac=='FOLLOW':return t>=2 and pledged and remaining>0
    return False

def step(st,ac):
    a,u,s,p,r=st
    if ac=='A':a+=1
    if ac=='U':u+=1
    if ac=='SIDE':s+=1
    if ac=='PLEDGE':p=True
    if ac=='FOLLOW':r-=1
    return a,u,s,p,r

def terminal(st,urgent):
    a,u,s,p,remain=st
    return F(12*(a>=3 or (p and remain==0))+10*(urgent and u>=2)+s)

def dynamic(allow_pledge=True,follow_options=(1,2)):
    @lru_cache(None)
    def dp(t,st,urgent):
        if t==2 and urgent=='?':
            vals=[]
            for u in (0,1):
                opts=follow_options if st[3] else (0,)
                for need in opts:
                    vals.append(dp(t,st[:4]+(need,),u))
            return sum(vals)/len(vals)
        if t==5:return terminal(st,urgent)
        return max(dp(t+1,step(st,ac),urgent) for ac in ACT
                   if legal(st,ac,t,urgent) and (allow_pledge or ac!='PLEDGE'))
    return dp(0,INIT,'?'),dp

def fixed(allow_pledge=True,follow_options=(1,2)):
    best=None;winners=[]
    for seq in product(ACT,repeat=5):
        outcomes=[]
        for urgent in (0,1):
            for need in follow_options:
                st=INIT
                for t,ac in enumerate(seq):
                    if t==2:st=st[:4]+((need if st[3] else 0),)
                    if not legal(st,ac,t,urgent) or (not allow_pledge and ac=='PLEDGE'):
                        ac='SIDE'
                    st=step(st,ac)
                outcomes.append(terminal(st,urgent))
        val=sum(outcomes)/len(outcomes)
        if best is None or val>best:best=val;winners=[seq]
        elif val==best:winners.append(seq)
    return best,tuple(winners)

def main():
    mixed,dp=dynamic()
    assert mixed==fixed()[0]==18
    assert dynamic(allow_pledge=False)[0]==fixed(allow_pledge=False)[0]==18
    assert dp(2,step(step(INIT,'PLEDGE'),'SIDE'),'?')==F(33,2)
    assert dp(2,step(step(INIT,'PLEDGE'),'A'),'?')==F(31,2)
    assert dp(2,step(step(INIT,'A'),'A'),'?')==18
    for needs,expected in [((0,),20),((1,),19),((2,),18)]:
        assert dynamic(follow_options=needs)[0]==expected
        assert fixed(follow_options=needs)[0]==expected
    assert ('A','A','A','U','U') in fixed()[1]
    print('CARRY dynamic/fixed',mixed,fixed()[0],
          'no pledge',dynamic(allow_pledge=False)[0])
    print('CARRY forced day1 pledge + side value',F(33,2),
          'versus independent manual preparation',18)
    print('CARRY fixed follow-up burden 0/1/2 values',20,19,18)
    print('PASS cross-cycle liability, global precommitment bypass and all 5^5 fixed scripts')

if __name__=='__main__':main()
