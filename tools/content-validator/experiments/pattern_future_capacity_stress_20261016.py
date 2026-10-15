"""STRESS-CAPACITY: a stressed action changes future work capacity, not just score.

Abstract two-phase decision with two distinct actual goals. This is a small
mature-contract-pattern thought experiment, NOT Citizen Sleeper 2's actual rules.

t0 PUSH puts 2 work into A and independently breaks one of two t1 action
slots with probability 1/2. t0 WAIT earns 1 SIDE and preserves both t1
slots. At t1, demand A or B is revealed. Goal A requires 3 work, B 2;
matched delivery gives 12, unmatched delivery gives 4, each SIDE yields 1.

Controls: replace break with an equal-on-root money penalty (9/2 per break);
offer an unconditional extra action to restore missing capacity; permit one
COMMON action that safely adds 1 preparation to both goals.
All exact values use Fraction and enumerate every feasible final action pair.
"""
from fractions import Fraction as F
from itertools import product

TARGETS=('A','B')
ACTIONS=('A','B','SIDE')


def available(initial, hurt, mode):
    if initial == 'WAIT' or initial == 'COMMON':
        return 2
    if mode == 'capacity' and hurt:
        return 1
    return 2


def physical(initial, moves):
    pa,pb,side=(2,0,0) if initial=='PUSH' else ((1,1,0) if initial=='COMMON'
                                                 else (0,0,1))
    for action in moves:
        if action == 'A':pa += 1
        elif action == 'B':pb += 1
        elif action == 'SIDE':side += 1
        else:raise ValueError(action)
    return int(pa>=3),int(pb>=2),side


def payoff(w, initial, moves, hurt=False, mode='capacity'):
    done_a,done_b,side=physical(initial,moves)
    total = side + (12 if w=='A' else 4)*done_a + (12 if w=='B' else 4)*done_b
    if initial=='PUSH' and hurt and mode=='penalty':
        return F(total) - F(9,2)
    return F(total)


def late_best(initial, w, hurt, mode='capacity'):
    moves=list(product(ACTIONS,repeat=available(initial,hurt,mode)))
    records=[(payoff(w,initial,seq,hurt,mode),seq) for seq in moves]
    top=max(x[0] for x in records)
    return top,tuple(seq for score,seq in records if score==top)


def exact_root(initial, p_a=F(1,2), mode='capacity'):
    total=F()
    for w,pw in (('A',p_a),('B',1-p_a)):
        for hurt,ph in ((False,F(1,2)),(True,F(1,2))):
            total += pw*ph*late_best(initial,w,hurt,mode)[0]
    return total


def ledger(initial,mode='capacity',p_a=F(1,2)):
    """Forward hidden world probabilities + true physical result identities."""
    totals={'score':F(),'A_done':F(),'B_done':F(),
            'correct':F(),'side':F(),'damaged':F()}
    paths=[]
    for w,pw in (('A',p_a),('B',1-p_a)):
        for hurt,ph in ((False,F(1,2)),(True,F(1,2))):
            v,plans=late_best(initial,w,hurt,mode)
            seq=plans[0]
            a,b,side=physical(initial,seq)
            prob=pw*ph
            totals['score']+=prob*payoff(w,initial,seq,hurt,mode)
            totals['A_done']+=prob*a
            totals['B_done']+=prob*b
            totals['correct']+=prob*(a if w=='A' else b)
            totals['side']+=prob*side
            totals['damaged']+=prob*(hurt and initial=='PUSH')
            paths.append((w,hurt,seq,v,prob,(a,b,side)))
    return totals,paths


def clear_policy(initial,w,hurt):
    if initial != 'PUSH':
        return ('B','B') if w=='B' else ('SIDE','SIDE')
    if w=='A':
        return ('A',) if hurt else ('A','SIDE')
    return ('A',) if hurt else ('B','B')


def regression():
    for i in ('PUSH','WAIT','COMMON'):
        for w in TARGETS:
            for hurt in (True,False):
                for mode in ('capacity','penalty','backstop'):
                    value,plans=late_best(i,w,hurt,mode)
                    assert any(payoff(w,i,seq,hurt,mode)==value for seq in plans)
                    assert all(len(seq)==available(i,hurt,mode) for seq in plans)
    assert exact_root('PUSH')==F(41,4)
    assert exact_root('WAIT')==9
    assert exact_root('COMMON')==F(25,2)
    assert exact_root('PUSH',mode='penalty') == F(41,4)
    assert exact_root('PUSH',mode='backstop') == F(25,2)
    assert late_best('PUSH','B',False)[0] == 12
    assert late_best('PUSH','B',True)[0] == 4
    assert late_best('PUSH','B',True)[1] == (('A',),)
    for w in TARGETS:
        for hurt in (True,False):
            actual=clear_policy('PUSH',w,hurt)
            assert actual in late_best('PUSH',w,hurt)[1]
    capacity,c_paths=ledger('PUSH')
    penalty,p_paths=ledger('PUSH',mode='penalty')
    assert capacity['score']==penalty['score']==F(41,4)
    assert capacity['A_done']==F(3,4)
    assert capacity['B_done']==F(1,4)
    assert penalty['A_done']==F(1,2)
    assert penalty['B_done']==F(1,2)
    assert capacity['correct']==F(3,4)
    assert penalty['correct']==1
    assert capacity['damaged']==penalty['damaged']==F(1,2)
    assert capacity['score']==exact_root('PUSH')
    assert penalty['score']==exact_root('PUSH',mode='penalty')
    # Exogenous safe preparation weakly dominates the dangerous PUSH policy
    # at the same first-move cost and publicly stated result definition.
    assert exact_root('COMMON') > exact_root('PUSH') > exact_root('WAIT')
    # Posterior-dependent first-action choice with no tuning after reveal.
    # WAIT root = 13-8p, PUSH root=8+9p/2; indifference at p=2/5.
    for p in (F(),F(1,4),F(2,5),F(1,2),F(1)):
        assert exact_root('WAIT',p) == 13-8*p
        assert exact_root('PUSH',p) == 8+F(9,2)*p
        if p < F(2,5):assert exact_root('WAIT',p)>exact_root('PUSH',p)
        elif p > F(2,5):assert exact_root('PUSH',p)>exact_root('WAIT',p)
        else:assert exact_root('WAIT',p)==exact_root('PUSH',p)
    print('STRESS-CAPACITY PASS: PUSH=41/4, WAIT=9, COMMON=25/2')
    print('Matched numerical root scalar-penalty PUSH=41/4, actual capacity changed delivery:')
    print('  capacity=',capacity)
    print('  money-penalty=',penalty)
    print('Normal reachable W=B: intact -> finish B; broken -> finish wrong A')
    print('One-line conditional policy matches exhaustive enumeration')
    print('First-action threshold P(W=A)=2/5; strong safe common prep eliminates gamble')
    print('LIMIT: not actual CS2 rules; no human/independent agent player evidence')


if __name__=='__main__':
    regression()
