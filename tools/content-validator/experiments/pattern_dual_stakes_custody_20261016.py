"""Two-stage entrusted A + independent B: exact action/outcome audit.

One early decision PLEDGE (gain custody of A, no reward yet) or PASS (take 1 SIDE).
Public future need W=A/B and damage H=0/1 independent, each equiprobable.
Two subsequent actions. PLEDGE may SEAL A in one move (unless H=1, then
MEND and SEAL), B_WORK twice for B, or SIDE. If entrusted A not sealed at the
end, distinct promise-breach is recorded, priced 4 only for the optional
scalar evaluation. PASS can do B_WORK twice or SIDE. In direct-A control,
PASS can also do A_WORK twice without obligation. A and B are separate end
identities, not one interchangeable progress bar.
"""
from fractions import Fraction as F
from itertools import product
from collections import defaultdict

WORLDS = tuple(product(('A','B'), (0,1)))
MATCH=14
MISMATCH=4
BREACH_COST=4


def options(early, world, damage, state, direct=False, cancellation=False):
    sealed, mended, b, aw, side, cancelled = state
    if early == 'PASS':
        result=['B_WORK' if b < 2 else None, 'A_WORK' if direct and aw < 2 else None, 'SIDE']
    elif cancelled:
        result=['B_WORK' if b < 2 else None,'SIDE']
    else:
        result=['B_WORK' if b < 2 else None,
                'MEND' if damage and not mended else None,
                'SEAL' if not sealed and (not damage or mended) else None,
                'CANCEL' if cancellation else None,'SIDE']
    return tuple(x for x in result if x is not None)


def advance(state, action):
    seal, mend, b, aw, side, cancelled = state
    if action=='B_WORK':b+=1
    elif action=='A_WORK':aw+=1
    elif action=='MEND':mend=True
    elif action=='SEAL':seal=True
    elif action=='CANCEL':cancelled=True
    elif action=='SIDE':side+=1
    else:raise ValueError(action)
    return seal,mend,b,aw,side,cancelled


def outcome(early, w, state, breach_cost=BREACH_COST, direct=False):
    sealed, _, b, aw, side, cancelled = state
    A = sealed or (early=='PASS' and direct and aw==2)
    B = b==2
    breach = (early=='PLEDGE' and not sealed)
    # A and B may both be achieved in the 3-action slack control only.
    g = (MATCH if w=='A' else MISMATCH) if A else 0
    g += (MATCH if w=='B' else MISMATCH) if B else 0
    g += side + (1 if early=='PASS' else 0)
    return (F(g)-F(breach_cost)*breach, bool(A),bool(B),bool(breach),side+(early=='PASS'))


def sequences(early, w, hazard, direct=False, cancellation=False, late_actions=2):
    def walk(prefix, state, n):
        if not n:
            yield prefix, state
            return
        for a in options(early,w,hazard,state,direct,cancellation):
            yield from walk(prefix+(a,),advance(state,a),n-1)
    yield from walk((),(False,False,0,0,0,False),late_actions)


def best_in_world(early,w,h,*,direct=False, cancellation=False,
                  breach_cost=BREACH_COST,require_A=False,forbid_breach=False,late_actions=2):
    choices=[]
    for actions,s in sequences(early,w,h,direct,cancellation,late_actions):
        result=outcome(early,w,s,breach_cost,direct)
        score,A,B,breach,_=result
        if require_A and not A:continue
        if forbid_breach and breach:continue
        choices.append((score,actions,result))
    if not choices:return None
    high=max(x[0] for x in choices)
    return high,tuple(x for x in choices if x[0]==high)


def dynamic(*,direct=False,cancellation=False,breach_cost=BREACH_COST,
            require_A=False,forbid_breach=False,late_actions=2):
    root={}
    for early in ('PLEDGE','PASS'):
        vals={}
        for w,h in WORLDS:
            r=best_in_world(early,w,h,direct=direct,cancellation=cancellation,
                            breach_cost=breach_cost,require_A=require_A,
                            forbid_breach=forbid_breach,late_actions=late_actions)
            if r is None:break
            vals[(w,h)]=r
        if len(vals)==4:
            root[early]=(sum((v[0] for v in vals.values()),F())/4,vals)
    if not root:return None,{}
    mx=max(v[0] for v in root.values())
    return mx,{a:v for a,v in root.items() if v[0]==mx}


def single_early_ledger(early,*,direct=False,breach_cost=BREACH_COST,
                        cancellation=False,require_A=False,forbid_breach=False,late_actions=2):
    vals=[]
    for w,h in WORLDS:
        r=best_in_world(early,w,h,direct=direct,breach_cost=breach_cost,
                        cancellation=cancellation,require_A=require_A,
                        forbid_breach=forbid_breach,late_actions=late_actions)
        if r is None:return None
        score,choices=r
        acts,result=choices[0][1],choices[0][2]
        vals.append((w,h,acts,result))
    return vals


def fixed_suffix(early,*,direct=False,breach_cost=BREACH_COST):
    # A robust fixed script must be legal across both H0 and H1. No peeking.
    scripts={}
    for w,h in WORLDS:
        for seq,st in sequences(early,w,h,direct):
            scripts.setdefault(seq,0)
    values={}
    for seq in scripts:
        if not all(any(seq==s for s,st in sequences(early,w,h,direct)) for w,h in WORLDS):continue
        values[seq]=sum((outcome(early,w,next(st for a,st in sequences(early,w,h,direct) if a==seq),breach_cost,direct)[0] for w,h in WORLDS),F())/4
    return max(values.values()),{s:v for s,v in values.items() if v==max(values.values())}


def forward(ledger):
    totals=defaultdict(F)
    for w,h,acts,(score,A,B,breach,side) in ledger:
        totals['score']+=score/4
        totals['A']+=F(A,4)
        totals['B']+=F(B,4)
        totals['breach']+=F(breach,4)
        totals['SIDE']+=F(side,4)
        totals['wA_A']+=F(w=='A' and A,4)
        totals['wB_B']+=F(w=='B' and B,4)
    return dict(totals)


def regression():
    value,pol=dynamic()
    assert value==F(49,4) and tuple(pol)==('PLEDGE',),(value,pol)
    r=single_early_ledger('PLEDGE');assert r is not None
    assert forward(r)=={'score':F(49,4),'A':F(1,2),'B':F(1,2),
                        'breach':F(1,2),'SIDE':F(1,4),
                        'wA_A':F(1,2),'wB_B':F(1,2)},forward(r)
    assert dynamic()[1]['PLEDGE'][0]==forward(r)['score']
    assert dynamic(breach_cost=0)[0]==F(57,4)
    assert dynamic(direct=True)[0]==F(15)
    assert dynamic(require_A=True)[0]==F(19,2)
    assert dynamic(forbid_breach=True)[0]==F(10)
    assert dynamic(cancellation=True)[0]==value
    assert dynamic(breach_cost=0, direct=True)[0]==F(15)
    assert fixed_suffix('PLEDGE')[0]==F(5)
    assert fixed_suffix('PASS')[0]==F(10)
    # If a voluntary, purely penalizing duty becomes expensive, the globally
    # optimal policy opts OUT before the later difficult decision can occur.
    for two_c in range(0,41):
        c=F(two_c,2)
        optimum,winners=dynamic(breach_cost=c)
        if c < F(17,2):
            assert tuple(winners)==('PLEDGE',)
            assert optimum==F(57,4)-c/2
        elif c > F(17,2):
            assert tuple(winners)==('PASS',)
            assert optimum==10
        else:
            assert set(winners)=={'PLEDGE','PASS'} and optimum==10
        if 'PLEDGE' in winners:
            # No globally optimal, positive-probability B-world branch
            # chooses to honor A instead of completing B and defaulting.
            for w,h,actions,result in single_early_ledger('PLEDGE',breach_cost=c):
                if w=='B':
                    assert actions==('B_WORK','B_WORK') and result[3]
    # Additional realistic action capacity destroys the intended clash:
    # by four late actions BOTH independent results are always achieved.
    assert dynamic(late_actions=3)[0]==F(31,2)
    assert dynamic(late_actions=4)[0]==F(37,2)
    four=single_early_ledger('PLEDGE',late_actions=4)
    assert all(res[1] and res[2] and not res[3] for w,h,actions,res in four)
    assert dynamic(require_A=True,late_actions=2)[0]==F(19,2)
    print('CUSTODY PASS: root=',value,'true-world ledger:',forward(r))
    print('main branches:',[(w,h,actions,str(result[0])) for w,h,actions,result in r])
    print('fixed PLEDGE=',fixed_suffix('PLEDGE')[0],'fixed PASS=',fixed_suffix('PASS')[0])
    print('controls: no duty=',dynamic(breach_cost=0)[0],
          'direct A=',dynamic(direct=True)[0],
          'hard A=',dynamic(require_A=True)[0],
          'no breach=',dynamic(forbid_breach=True)[0],
          'cancel allowed=',dynamic(cancellation=True)[0])
    print('late slots 3/4=',dynamic(late_actions=3)[0],dynamic(late_actions=4)[0])
    print('voluntary opt-out threshold=17/2; stronger penalties prevent pledge')
    print('LIMIT: mathematical action/identity facts, no player or blind-agent sessions')


if __name__=='__main__':regression()
