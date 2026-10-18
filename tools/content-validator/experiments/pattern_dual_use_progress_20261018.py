"""DUAL-USE: work-in-progress can be consumed to do another job.

Four actions: BUILD_A adds 1 A stock, BUILD_B adds 1 B work, RECYCLE
consumes 1 A stock to add 2 B work, SIDE adds 1. Demand A/B is
equally likely and revealed after step two. A requires 3 work, B needs
2; completed matching result pays 12, mismatching result 2. Partial
work has no terminal value. Strong controls: no RECYCLE, earlier/later
reveal, best full fixed script and same-cost SAFE COMMON adding +1 to
both A and B. All Fraction, all legal 4-step plans.
"""
from fractions import Fraction as F

BASE = ('BUILD_A', 'BUILD_B', 'RECYCLE', 'SIDE')
ZERO = (0, 0, 0)


def transition(s, action, recycle=True, common=False):
    a,b,side=s
    if action=='BUILD_A':return a+1,b,side
    if action=='BUILD_B':return a,b+1,side
    if action=='RECYCLE' and recycle and a>=1:return a-1,b+2,side
    if action=='SIDE':return a,b,side+1
    if action=='COMMON' and common:return a+1,b+1,side
    return None


def actions(s,recycle=True,common=False):
    return tuple(a for a in BASE+(('COMMON',) if common else ())
                 if transition(s,a,recycle,common) is not None)


def sequences(s,steps,recycle=True,common=False):
    if steps==0:
        yield (),s
        return
    for action in actions(s,recycle,common):
        t=transition(s,action,recycle,common)
        for suffix,finish in sequences(t,steps-1,recycle,common):
            yield (action,)+suffix,finish


def score(s,w):
    a,b,side=s
    assert w in ('A','B')
    return F(side+(12 if w=='A' else 2)*(a>=3)
                +(12 if w=='B' else 2)*(b>=2))


def best_suffix(s,steps,w,recycle=True,common=False):
    rows=[(score(final,w),seq,final)
          for seq,final in sequences(s,steps,recycle,common)]
    best=max(t[0] for t in rows)
    return best,tuple(x for x in rows if x[0]==best)


def dynamic(reveal=2,recycle=True,common=False,p=F(1,2)):
    rows=[]
    for prefix,s in sequences(ZERO,reveal,recycle,common):
        A=best_suffix(s,4-reveal,'A',recycle,common)
        B=best_suffix(s,4-reveal,'B',recycle,common)
        rows.append((p*A[0]+(1-p)*B[0],prefix,s,A,B))
    best=max(row[0] for row in rows)
    return best,tuple(row for row in rows if row[0]==best)


def fixed(recycle=True,common=False,p=F(1,2)):
    rows=[]
    for seq,s in sequences(ZERO,4,recycle,common):
        rows.append((p*score(s,'A')+(1-p)*score(s,'B'),seq,s))
    best=max(x[0] for x in rows)
    return best,tuple(x for x in rows if x[0]==best)


def token_encoding(s):
    """State rename to a generic ordinary inventory mechanism."""
    a,b,side=s
    return {'stock':a,'project_b':b,'side':side}


def token_transition(t,action,common=False):
    t=t.copy()
    if action=='BUILD_A':t['stock']+=1
    elif action=='BUILD_B':t['project_b']+=1
    elif action=='RECYCLE' and t['stock']>=1:
        t['stock']-=1;t['project_b']+=2
    elif action=='SIDE':t['side']+=1
    elif action=='COMMON' and common:
        t['stock']+=1;t['project_b']+=1
    else:return None
    return t


def check_full_tree_isomorphism(common=False):
    frontier={ZERO}
    total_edges=0
    for turn in range(4):
        next_frontier=set()
        for s in frontier:
            for action in BASE+(('COMMON',) if common else ()):
                x=transition(s,action,True,common)
                y=token_transition(token_encoding(s),action,common)
                assert (x is None)==(y is None)
                if x is not None:
                    assert token_encoding(x)==y
                    for w in ('A','B'):
                        expected=(12 if w=='A' else 2)*(y['stock']>=3)
                        expected+=(12 if w=='B' else 2)*(y['project_b']>=2)
                        expected+=y['side']
                        assert score(x,w)==F(expected)
                    next_frontier.add(x)
                    total_edges+=1
        frontier=next_frontier
    assert total_edges>=25
    return total_edges


def regression():
    assert dynamic()[0]==F(27,2)
    assert dynamic(recycle=False)[0]==13
    assert fixed()[0]==9
    assert fixed(recycle=False)[0]==9
    assert dynamic(reveal=1)[0]==F(27,2)
    assert dynamic(reveal=3)[0]==13
    assert dynamic(reveal=4)[0]==9
    assert dynamic(common=True)[0]==15
    assert fixed(common=True)[0]==15

    value,solutions=dynamic()
    assert {x[1] for x in solutions}=={
        ('BUILD_A','SIDE'),('SIDE','BUILD_A')}
    case=next(x for x in solutions if x[1]==('BUILD_A','SIDE'))
    assert case[3][0]==13 and case[4][0]==14
    assert ('BUILD_A','BUILD_A') in {r[1] for r in case[3][1]}
    assert ('RECYCLE','SIDE') in {r[1] for r in case[4][1]}
    assert score((3,0,1),'A')==13 and score((0,2,2),'B')==14
    assert ('BUILD_B','BUILD_B','SIDE','SIDE') in {r[1] for r in fixed()[1]}
    assert case[3][1][0][2]!=case[4][1][0][2]

    assert check_full_tree_isomorphism(False)>0
    assert check_full_tree_isomorphism(True)>0
    print('DUAL-USE PASS dynamic=27/2; no conversion=13; strongest fixed=9')
    print('Late reveal step3=13; no reveal=9; common safe=15 (fixed=15)')
    print('Canonical: BUILD_A,SIDE; A -> BUILD_A,BUILD_A; B -> RECYCLE,SIDE')
    print('All legal transitions & state payoffs isomorphic to ordinary stock inventory')
    print('LIMIT standard-library exact model; no real participants/agents/Unity')


if __name__=='__main__':
    regression()
