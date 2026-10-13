#!/usr/bin/env python3
"""CERT01-04: noncompensatory certificate makes outsourced acceptance useful.

4-6 actions. A3 worth12, B2 worth9, SIDE worth1. SEND (cost1)
starts a GOOD helper (A2) or BAD helper (A1), each with probability 1/2;
production arrives over the next two player turns. ACCEPT after two
follow-up actions (cost1) claims the produced A-progress and an
independent certificate C. Owning C AND finishing A pays a separate10.
A rival ordinary REGISTER obtains the same certificate for one action
but supplies no automatic work. A generic AUTO robot has IDENTICAL
rules to HELPER; changing its name is not independent agent behavior.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product

INIT=(0,0,0,-1,False)
ACTS=('A','B','SIDE','SEND','ACCEPT','REGISTER')
MODES=('helper','robot','credential','none')

def actions(st,mode):
    a,b,side,age,accepted=st
    opts=['A','B','SIDE']
    if mode in ('helper','robot'):
        if age==-1:opts.append('SEND')
        if age>=2 and not accepted:opts.append('ACCEPT')
    elif mode=='credential' and not accepted:
        opts.append('REGISTER')
    return tuple(opts)

def advance(st,act,mode):
    a,b,side,age,accepted=st
    assert act in actions(st,mode)
    if act=='SEND':age=0
    elif act=='ACCEPT':accepted=True;age+=1
    elif act=='REGISTER':accepted=True
    else:
        if act=='A':a+=1
        elif act=='B':b+=1
        else:side+=1
        if age>=0:age+=1
    return (a,b,side,age,accepted)

def terminal(st,quality,mode,credential_value=10):
    a,b,side,age,accepted=st
    received=0
    if mode in ('helper','robot') and accepted:
        received=min(age,2 if quality=='good' else 1)
    valid_a=a+received>=3
    valid_b=b>=2
    valid_c=accepted and valid_a
    reward=F(12*valid_a+9*valid_b+side+credential_value*valid_c)
    return reward,(int(valid_a),int(valid_b),int(valid_c),side)

def solve(mode='helper',turns=5,credential_value=10,require_c=False):
    @lru_cache(None)
    def dp(t,st,quality):
        if t==turns:
            reward,goals=terminal(st,quality,mode,credential_value)
            return reward if not require_c or goals[2] else None
        values=[]
        for ac in actions(st,mode):
            ns=advance(st,ac,mode)
            if ac=='SEND':
                futures=[dp(t+1,ns,q) for q in ('good','bad')]
                if any(v is None for v in futures):continue
                values.append(sum(futures)/2)
            else:
                v=dp(t+1,ns,quality)
                if v is not None:values.append(v)
        return max(values) if values else None
    return dp(0,INIT,'?'),dp

def fixed(mode='helper',turns=5,credential_value=10):
    best=None;wins=[]
    for sequence in product(ACTS,repeat=turns):
        vals=[]
        for q in ('good','bad'):
            st=INIT
            for ac in sequence:
                if ac not in actions(st,mode):ac='SIDE'
                st=advance(st,ac,mode)
            vals.append(terminal(st,q,mode,credential_value)[0])
        v=sum(vals)/2
        if best is None or v>best:best=v;wins=[sequence]
        elif v==best:wins.append(sequence)
    return best,tuple(wins)

def witness(plan_good,plan_bad):
    scores=[];out=[]
    for q,plan in (('good',plan_good),('bad',plan_bad)):
        st=INIT
        for ac in plan:st=advance(st,ac,'helper')
        reward,goals=terminal(st,q,'helper')
        scores.append(reward);out.append((q,plan,reward,goals))
    return sum(scores)/2,out

def main():
    roots={(m,t):solve(m,t)[0] for m in MODES for t in (4,5,6)}
    assert [roots['helper',t] for t in (4,5,6)]==[F(45,2),27,F(63,2)]
    assert [roots['credential',t] for t in (4,5,6)]==[22,23,31]
    assert [roots['none',t] for t in (4,5,6)]==[13,21,22]
    for t in (4,5,6):
        assert roots['robot',t]==roots['helper',t]
        assert fixed('robot',t)==fixed('helper',t)
    assert [fixed('helper',t)[0] for t in (4,5,6)]==[22,23,31]
    assert [solve('helper',t,credential_value=0)[0] for t in (4,5)]==[13,21]
    assert solve('none',5,require_c=True)[0] is None
    assert solve('helper',5,require_c=True)[0]==27
    assert solve('credential',5,require_c=True)[0]==23
    val4,branches4=witness(('SEND','A','SIDE','ACCEPT'),('SEND','A','A','ACCEPT'))
    val5,branches5=witness(('SEND','A','B','B','ACCEPT'),('SEND','A','A','SIDE','ACCEPT'))
    assert val4==F(45,2) and val5==27
    assert [x[2] for x in branches4]==[23,22]
    assert [x[2] for x in branches5]==[31,23]
    assert [x[3] for x in branches5]==[(1,1,1,0),(1,0,1,1)]
    print('CERT dynamic roots',roots)
    print('CERT four/five witnesses',branches4,branches5)
    print('CERT helper fixed [4,5,6]',[fixed('helper',t)[0] for t in (4,5,6)])
    print('PASS distinct claimability, generic robot equivalence and ordinary credential control')

if __name__=='__main__':main()
