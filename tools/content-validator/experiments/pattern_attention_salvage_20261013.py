#!/usr/bin/env python3
"""ATN03/04: two goals threatened by public mid-encounter erasure.
Pre: two work actions. Incident: independent 1/2 erasure A and B,
both publicly observed. Post: three actions. Completion threshold A=B=2.
Exact full five-action fixed plans, all post-incident responses, early
information upper bound and no-double-erasure control. No human evidence.
"""
from fractions import Fraction as Q
from itertools import product
ACT=('A','B','SIDE')
EVENTS=('none','eraseA','eraseB','eraseBoth')
PROBS=(Q(1,4),)*4

def step(s,act):
    a,b,side=s
    return min(2,a+(act=='A')),min(2,b+(act=='B')),side+(act=='SIDE')

def event(s,e):
    a,b,side=s
    if e in ('eraseA','eraseBoth'):a=0
    if e in ('eraseB','eraseBoth'):b=0
    return a,b,side

def terminal(s):
    a,b,side=s
    return Q(a>=2),Q(b>=2),Q(side)

def score(vec,weights):
    return sum(v*Q(w) for v,w in zip(vec,weights))

def play(plan,e):
    s=(0,0,0)
    for t,a in enumerate(plan):
        if t==2:s=event(s,e)
        s=step(s,a)
    return terminal(s)

def _play_from(st,plan):
    for act in plan:st=step(st,act)
    return st

def best_policy(weights=(10,8,1)):
    preps={}
    for pre in product(ACT,repeat=2):
        st=(0,0,0)
        for act in pre:st=step(st,act)
        entries=[]
        for e in EVENTS:
            choices=[]
            for tail in product(ACT,repeat=3):
                v=terminal(_play_from(event(st,e),tail))
                choices.append((score(v,weights),v,tail))
            winner=max(choices,key=lambda it:(it[0],tuple(-ACT.index(x) for x in it[2])))
            entries.append((e,winner))
        root=sum(p*r[0] for p,(_,r) in zip(PROBS,entries))
        vec=tuple(sum(p*r[1][i] for p,(_,r) in zip(PROBS,entries)) for i in range(3))
        preps[pre]=(root,vec,entries)
    highest=max(v[0] for v in preps.values())
    return {p:out for p,out in preps.items() if out[0]==highest},preps

def static_best(weights=(10,8,1)):
    cases=[]
    for plan in product(ACT,repeat=5):
        vec=tuple(sum(w*play(plan,e)[i] for e,w in zip(EVENTS,PROBS)) for i in range(3))
        cases.append((score(vec,weights),vec,plan))
    highest=max(c[0] for c in cases)
    return highest,[c for c in cases if c[0]==highest]

def no_event_policies():
    return max(score(play(plan,'none'),(10,8,1)) for plan in product(ACT,repeat=5))

def early_revelation(weights=(10,8,1)):
    each=[(e,max(score(play(plan,e),weights) for plan in product(ACT,repeat=5))) for e in EVENTS]
    return sum(w*value for (_,value),w in zip(each,PROBS)),each

def only_single_event(weights=(10,8,1)):
    # Events none/eraseA/eraseB equiprobable; eraseBoth omitted.
    results=[]
    for pre in product(ACT,repeat=2):
        st=(0,0,0)
        for act in pre:st=step(st,act)
        value=Q(0)
        for e in EVENTS[:3]:
            value+=max(score(terminal(_play_from(event(st,e),tail)),weights)
                       for tail in product(ACT,repeat=3))/3
        results.append((value,pre))
    return max(results)

def simple_tail(e):
    return {
        'none':('A','B','SIDE'),
        'eraseA':('A','A','B'),
        'eraseB':('A','B','B'),
        'eraseBoth':('A','A','SIDE'),
    }[e]

def simple_result(weights=(10,8,1)):
    vec=tuple(sum(w*play(('A','B')+simple_tail(e),e)[i]
                  for e,w in zip(EVENTS,PROBS)) for i in range(3))
    return score(vec,weights),vec

def main():
    for weights in ((10,8,1),(8,10,1),(10,10,1),(10,8,0),(12,4,1)):
        winners,preps=best_policy(weights)
        fixed,plans=static_best(weights)
        print('ATN03 weights',weights,'optimal',next(iter(winners.values()))[:2],
              'preps',list(winners),'static',fixed,'static_plans',len(plans))
        assert all(out[0]>=fixed for out in winners.values())
        if weights==(10,8,1):
            assert next(iter(winners.values()))[0]==Q(33,2)
            assert fixed==15
            assert simple_result(weights)==(Q(33,2),(Q(1),Q(3,4),Q(1,2)))
            print('ATN03 per-event plan')
            for p,out in winners.items():
                print('  pre',p,[(e,v[1],v[2]) for e,v in out[2]])
        if weights==(8,10,1):assert next(iter(winners.values()))[1][:2]==(Q(3,4),Q(1))
    assert no_event_policies()==19
    early,parts=early_revelation()
    assert early==Q(35,2)
    assert parts==[('none',Q(19)),('eraseA',Q(19)),('eraseB',Q(19)),('eraseBoth',Q(13))]
    single=only_single_event()
    assert single[0]==Q(55,3)
    print('ATN04 early_information',early,'by_event',parts,
          'without_double_erasure',single,'full_fixed',static_best()[0],
          'no_erasure',no_event_policies())
    print('PASS exact 9 initial preparations, 4x27 tails and 243 fixed schedules')

if __name__=='__main__':main()
