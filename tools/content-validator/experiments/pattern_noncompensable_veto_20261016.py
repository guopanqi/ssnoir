"""Physical-preservation veto vs cash compensation: counterexample audit.

Same A/B player payoffs as BOUND-THIRD, but the partner's expressed condition
is now PHYSICAL A must survive in every publicly possible demand world.
Money is not a substitute for the physical outcome. All policies in this file
are genuinely enforceable, observable prior to partner's JOIN/EXIT choice.
Only four deterministic world->action policies; no human preferences inferred.
"""
from fractions import Fraction as F
from itertools import product

from pattern_third_party_bond_20261016 import (
    WORLDS, A_VALUE, B_VALUE, contract
)

POLICIES = tuple(product(('A', 'B'), repeat=2))
SAFE_B_ONLY = F(9)


def expected_player(policy, transfer=F()):
    """Matching task payoffs under commitment, minus unconditional transfer."""
    return sum(((A_VALUE[w] if a == 'A' else B_VALUE[w])
                for w, a in zip(WORLDS, policy)), F()) / 2 - transfer


def preservation(policy):
    """A physically protected in every reachable world."""
    return all(a == 'A' for a in policy)


def partner_veto(policy, compensation=F()):
    """The partner insists on A; no amount of money changes that objective."""
    assert compensation >= 0
    return preservation(policy)


def outcome_vector(policy):
    return {'A_rate': sum((F(a == 'A', 2) for a in policy), F()),
            'B_rate': sum((F(a == 'B', 2) for a in policy), F()),
            'demand_match': sum((F(w == a, 2)
                                 for w, a in zip(WORLDS, policy)), F()),
            'all_worlds_A': preservation(policy)}


def enumerate_policies():
    return {p: {'score': expected_player(p),
                'accepted_physical': partner_veto(p),
                'result': outcome_vector(p)} for p in POLICIES}


def regression():
    x = enumerate_policies()
    assert len(x) == 4
    assert x[('A', 'A')]['score'] == 9
    assert x[('A', 'B')]['score'] == 14
    assert x[('B', 'A')]['score'] == 4
    assert x[('B', 'B')]['score'] == 9
    assert {p for p in POLICIES if x[p]['accepted_physical']} == {('A', 'A')}
    assert max(x[p]['score'] for p in POLICIES
               if x[p]['accepted_physical']) == SAFE_B_ONLY
    # Same scalar root does NOT imply same outcome or fulfilled stakeholder goal.
    assert outcome_vector(('A', 'A')) != outcome_vector(('B', 'B'))
    assert outcome_vector(('A', 'A'))['all_worlds_A']
    assert not outcome_vector(('B', 'B'))['all_worlds_A']
    for d in (0, 2, 6, 20, 1000):
        assert {p for p in POLICIES if partner_veto(p, F(d))} == {('A', 'A')}
    # Money-only partner in previous experiment joins on D=6 even though B
    # branch still physically loses A: this is an IMPORTANT different preference.
    cash_partner = contract(bond=6, hard_partner_floor=True)
    assert cash_partner['join'] and cash_partner['player'] == 11
    assert cash_partner['A'] == F(1, 2)
    assert cash_partner['partner_harm'] == 0
    # A post-world release may produce score14 only by relaxing A preservation.
    waived = ('A', 'B')
    assert expected_player(waived) == 14
    assert not preservation(waived)
    # A strong ordinary menu that can actually accomplish BOTH A and B
    # at real cost 3 makes all-world A preservation compatible with B success.
    joint_outcome = (A_VALUE['A'] + B_VALUE['A'] +
                     A_VALUE['B'] + B_VALUE['B']) / 2 - 3
    assert joint_outcome == 15 > 9
    print('NONCOMPENSABLE-VETO PASS: all 4 policies checked')
    print('Physical A right -> only AA accepted; player9, A=1, B=0')
    print('SAFE B-only also player9, A=0, B=1: same scalar, different identity')
    print('Cash floor D6 player11 has only A=1/2: payment is not preservation')
    print('Strict waiver to allow AB earns14 but no longer promises A in all worlds')
    print('Strong ordinary both-outcome tool cost3 earns15: no longer a forced dilemma')
    print('LIMIT: an explicit partner goal is an input, not evidence humans hold it')


if __name__ == '__main__':
    regression()
