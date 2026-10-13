#!/usr/bin/env python3
"""FOCUS01-06: mid-work switching, information timing, and cheap waiting.

6 slots, A/B take 3 WORK each; the rewarded demand is learned after a
specified number of slots. Finishing either gives base5 +14 if demanded.
Switching focus consumes a full slot, except in the free-focus control.
For the primary test the first two slots REQUIRE real work (no SIDE);
this restriction is explicitly removed in the strongest waiting control.
Generic SETUP has exactly the same costs and transitions as SWITCH.
Fraction DP, all fixed legal schedules, full reachable-state isomorphism.
No human/agent blind play or Unity evidence.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('A','B','TO_A','TO_B','SIDE')
INIT=(0,0,'-')
TURNS=6

def legal(st,ac,t,kind='switch',early_side=False):
    a,b,focus=st
    if ac=='SIDE':return early_side or t>=2
    if ac.startswith('TO_'):return kind in ('switch','setup') and focus not in ('-',ac[-1])
    if ac=='A':return a<3 and (focus in ('-','A') or kind=='free')
    if ac=='B':return b<3 and (focus in ('-','B') or kind=='free')
    raise ValueError(ac)

def advance(st,ac):
    a,b,focus=st
    if ac=='SIDE':return st,1
    if ac.startswith('TO_'):return (a,b,ac[-1]),0
    if ac=='A':return (a+1,b,'A'),0
    if ac=='B':return (a,b+1,'B'),0
    raise ValueError(ac)

def payoff(st,demand):
    a,b,_=st
    return ((5+14*(demand=='A'))*(a>=3)+
            (5+14*(demand=='B'))*(b>=3))

def dp(kind='switch',reveal=2,early_side=False):
    @lru_cache(None)
    def solve(t,st,demand):
        if t==TURNS:return F(payoff(st,demand))
        if t==reveal and demand=='?':
            return (solve(t,st,'A')+solve(t,st,'B'))/2
        options=[]
        for ac in ACT:
            if legal(st,ac,t,kind,early_side):
                ns,v=advance(st,ac)
                options.append(F(v)+solve(t+1,ns,demand))
        return max(options)
    return solve(0,INIT,'?'),solve

def fixed(kind='switch',early_side=False):
    best=None;count=0;plans=[]
    for sequence in product(ACT,repeat=TURNS):
        state=INIT;side=0
        for t,ac in enumerate(sequence):
            if not legal(state,ac,t,kind,early_side):break
            state,v=advance(state,ac);side+=v
        else:
            count+=1
            score=F(payoff(state,'A')+payoff(state,'B'),2)+side
            if best is None or score>best:best=score;plans=[sequence]
            elif score==best:plans.append(sequence)
    return best,count,tuple(plans)

def reachable(kind='switch'):
    all_states={(0,INIT)}
    for t in range(TURNS):
        all_states|={(t+1,advance(st,ac)[0]) for i,st in tuple(all_states)
                      if i==t for ac in ACT if legal(st,ac,t,kind)}
    return all_states

def run(demand,prefix,continuation):
    state=INIT;side=0
    for t,ac in enumerate(prefix+continuation):
        assert legal(state,ac,t,'switch')
        state,earn=advance(state,ac);side+=earn
    return F(payoff(state,demand)+side)

WITNESSES={
    2:(('A','A'),('A','SIDE','SIDE','SIDE'),('TO_B','B','B','B')),
    3:(('A','TO_B','SIDE'),('TO_A','A','A'),('B','B','B')),
    4:(('A','A','TO_B','B'),('TO_A','A'),('B','B')),
}

def min_remaining_work(st,target,kind='switch'):
    """BFS for completing a given outcome from a revealed-state snapshot."""
    queue=[(st,0)]
    seen={st}
    for state,depth in queue:
        if state[0 if target=='A' else 1]>=3:return depth
        for ac in ACT[:-1]:  # SIDE cannot help completion.
            if not legal(state,ac,2,kind):continue
            nxt,_=advance(state,ac)
            if nxt not in seen:
                seen.add(nxt)
                queue.append((nxt,depth+1))
    return None

def main():
    roots={t:dp(reveal=t)[0] for t in range(5)}
    assert roots=={0:22,1:21,2:F(41,2),3:20,4:19},roots
    required={2:(1,4),3:(3,3),4:(2,2)}
    for t,(prefix,aa,bb) in WITNESSES.items():
        hi=run('A',prefix,aa)
        lo=run('B',prefix,bb)
        assert (hi+lo)/2==roots[t],(t,hi,lo)
        start=INIT
        for action in prefix:start,_=advance(start,action)
        assert (min_remaining_work(start,'A'),min_remaining_work(start,'B'))==required[t]
        assert all(x<=TURNS-t for x in required[t])
        print('reveal',t,'root',roots[t],
              'A/B branch',hi,lo,'prefix',prefix)
    results={kind:(dp(kind)[0],fixed(kind)) for kind in ('switch','setup','free')}
    assert results['switch'][0]==results['setup'][0]==F(41,2)
    assert results['switch'][1][0]==results['setup'][1][0]==15
    assert results['free'][0]==results['free'][1][0]==24
    assert fixed('switch')[1]==280
    assert dp(early_side=True)[0]==22
    assert fixed(early_side=True)[0]==15
    shared=reachable('switch')
    assert shared==reachable('setup') and len(shared)==89
    for t,st in shared:
        if t==TURNS:continue
        for ac in ACT:
            assert legal(st,ac,t,'switch')==legal(st,ac,t,'setup')
            # Both kind labels share advance(); their legality sets and
            # all 89 reachable (turn,state) pairs are identical by design.
    print('primary',results,'early side waiting',dp(early_side=True)[0],
          'matching reachable states',len(shared))
    print('PASS: genuine conditional switching, cheap waiting dominance, exact setup equivalence')

if __name__=='__main__':main()
