#!/usr/bin/env python3
"""PS01/02: ATN03's erasure/crisis + conditional autonomous one-time B aid.
Two prep work actions, then independent erase A/B 1/2 each, and independent
partner outside offer low/high. Three further player actions. Partner can
supply exactly one B progress following the FIRST postcrisis player action,
iff B incomplete and personally rescuing B pays 3 > outside offer 2/4.
Fraction exact policy enumeration; three-vector results, no human evidence.
"""
from fractions import Fraction as F
from itertools import product
ACT=('A','B','SIDE')
EVENTS=('none','eraseA','eraseB','both')
OFFERS=('low','high')

def work(st,action):
    a,b,s=st
    return min(2,a+(action=='A')),min(2,b+(action=='B')),s+(action=='SIDE')

def crisis(st,event):
    a,b,s=st
    return 0 if event in ('eraseA','both') else a,0 if event in ('eraseB','both') else b,s

def aid(st,offer,mode):
    a,b,s=st
    if mode in ('actor','backup') and offer=='low' and b<2:
        b+=1
    elif mode not in ('actor','backup','none'):
        raise AssertionError(mode)
    return a,b,s

def vec(st):
    return F(st[0]==2),F(st[1]==2),F(st[2])

def score(v,weights):
    return sum(F(x)*y for x,y in zip(weights,v))

def state_after_pre(pre,event):
    st=(0,0,0)
    for a in pre:st=work(st,a)
    return crisis(st,event)

def final(st,tail,offer,mode):
    for i,a in enumerate(tail):
        st=work(st,a)
        if i==0:st=aid(st,offer,mode)
    return vec(st)

def adapt(mode,weights=(10,8,1)):
    rows=[]
    for pre in product(ACT,repeat=2):
        total=[F(0)]*3;policy={}
        for event,offer in product(EVENTS,OFFERS):
            st=state_after_pre(pre,event)
            options=[(score(final(st,t,offer,mode),weights),
                      tuple(-ACT.index(a) for a in t),t,final(st,t,offer,mode))
                     for t in product(ACT,repeat=3)]
            _,_,tail,v=max(options)
            policy[(event,offer)]=(tail,v)
            for i in range(3):total[i]+=v[i]/8
        rows.append((score(total,weights),tuple(total),pre,policy))
    maximum=max(x[0] for x in rows)
    return [x for x in rows if x[0]==maximum]

def fixed(mode,weights=(10,8,1)):
    val=F(-1)
    for plan in product(ACT,repeat=5):
        pre,tail=plan[:2],plan[2:]
        total=[F(0)]*3
        for event,offer in product(EVENTS,OFFERS):
            out=final(state_after_pre(pre,event),tail,offer,mode)
            for i in range(3):total[i]+=out[i]/8
        val=max(val,score(total,weights))
    return val

def delayed(mode,weights=(10,8,1)):
    """Crisis known before first post hand, offer known only after first hand."""
    best=F(-1)
    for pre in product(ACT,repeat=2):
        expectation=F(0)
        for event in EVENTS:
            start=state_after_pre(pre,event)
            cand=[]
            for first in ACT:
                expected=F(0)
                for offer in OFFERS:
                    first_state=aid(work(start,first),offer,mode)
                    second_best=max(score(vec(work(work(first_state,a),b)),weights)
                                    for a,b in product(ACT,repeat=2))
                    expected+=second_best/2
                cand.append(expected)
            expectation+=max(cand)/4
        best=max(best,expectation)
    return best

def main():
    for weights in ((10,8,1),(8,10,1),(10,10,1),(10,8,0)):
        with_actor=adapt('actor',weights)
        with_backup=adapt('backup',weights)
        no_help=adapt('none',weights)
        assert with_actor[0][:3]==with_backup[0][:3]
        assert with_actor[0][0]>=no_help[0][0]
        assert with_actor[0][0]>=delayed('actor',weights)>=fixed('actor',weights)
        for pre in product(ACT,repeat=2):
            for e,o in product(EVENTS,OFFERS):
                st=state_after_pre(pre,e)
                for tail in product(ACT,repeat=3):
                    assert final(st,tail,o,'actor')==final(st,tail,o,'backup')
        print('PS',weights,'actor',[(x[0],x[1],x[2]) for x in with_actor],
              'no_partner',[(x[0],x[1],x[2]) for x in no_help],
              'fixed',fixed('actor',weights),'offer_late',delayed('actor',weights))
    default=adapt('actor')[0]
    assert default[:3]==(F(71,4),(F(1),F(7,8),F(3,4)),('A','B'))
    assert adapt('none')[0][0]==F(33,2)
    assert fixed('actor')==17
    assert delayed('actor')==F(71,4)
    assert default[3][('both','low')][1][:2]==(F(1),F(1))
    assert default[3][('both','high')][1][:2]==(F(1),F(0))
    print('PS representative contingencies:',default[3])
    print('PASS 9 preparations x 8 scenarios x 27 tails, 243 fixed plans and information delay')

if __name__=='__main__':main()
