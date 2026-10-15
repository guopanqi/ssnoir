"""SSNoir SIGNAL-AUDIT01: two-type costly proof, pure separating incentive audit.
Does NOT enumerate mixed equilibria or establish uniqueness.
"""
from fractions import Fraction as F

TYPES=('H','L')
PRIOR={'H':F(1,2),'L':F(1,2)}
RECEIVER={'H':8,'L':-8}
SAFE=1
SENDER_ACCEPT=3

def receiver_expected_accept(types):
    total=sum((PRIOR[t] for t in types),F(0))
    return sum((PRIOR[t]*RECEIVER[t] for t in types),F(0))/total

def separating(cost_h,cost_l):
    checks=(SENDER_ACCEPT-cost_h>=0,
            SENDER_ACCEPT-cost_l<=0,
            RECEIVER['H']>=SAFE,
            SAFE>=RECEIVER['L'])
    valid=all(checks)
    value=PRIOR['H']*RECEIVER['H']+PRIOR['L']*SAFE if valid else None
    return {'valid':valid,'player_payoff':value,'conditions':checks}

def pooling_no_proof(cost_h,cost_l):
    pool=max(F(SAFE),receiver_expected_accept(TYPES))
    return {'pool_payoff':pool,
            'deviation_if_proof_accepted':(SENDER_ACCEPT-cost_h>0,
                                          SENDER_ACCEPT-cost_l>0)}

def benchmark():
    without=max(receiver_expected_accept(TYPES),F(SAFE))
    perfect=PRIOR['H']*max(F(RECEIVER['H']),F(SAFE))+PRIOR['L']*max(F(RECEIVER['L']),F(SAFE))
    return without,perfect,perfect-2

def regression():
    assert benchmark()==(F(1),F(9,2),F(5,2))
    separating_case=separating(1,5)
    mimic=separating(1,2)
    unable=separating(4,5)
    assert separating_case['valid'] and separating_case['player_payoff']==F(9,2)
    assert not mimic['valid'] and not unable['valid']
    assert pooling_no_proof(1,5)['pool_payoff']==F(1)
    print('no_info,free_perfect_inspect,paid_perfect_inspect',benchmark())
    print('separating',separating_case)
    print('cheap_mimic',mimic)
    print('H_cannot_afford_proof',unable)
    print('pooling',pooling_no_proof(1,5))
    print('PASS: pure separating conditions; no claim of unique equilibrium')

if __name__=='__main__':
    regression()
