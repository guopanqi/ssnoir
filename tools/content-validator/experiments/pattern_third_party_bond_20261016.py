"""BOUND-THIRD: someone's own investment changes voluntary commitment.

Full public contract, future demand A/B is equally likely, player chooses A/B
once demand is known. Partner first decides JOIN/EXIT by its independent own
payoffs; EXIT enables player B-only safe outcome (E=9). Partner JOIN allows
both paths, partner earns +4 if player A and loses 6 if B. A conditional bond
D goes to partner only on B; an unconditional upfront fee F goes to partner
regardless of what player later does. Actual A outcome and monetary compensation
are recorded separately. Binding bond is not a verbal promise.

This is a mature contingent-contract/participation pattern; no human evidence.
"""
from fractions import Fraction as F
from itertools import product

WORLDS=('A','B')
A_VALUE={'A':F(14),'B':F(4)}
B_VALUE={'A':F(4),'B':F(14)}
PARTNER_A=F(4)
PARTNER_B=F(-6)
OUTSIDE=F(0)


def player_late(w,bond=F(),fee=F(),full_A=False):
    """Best autonomous continuation, with honor-on-equal explicitly chosen."""
    actions=('A',) if full_A else ('A','B')
    payoff={a:(A_VALUE[w] if a=='A' else B_VALUE[w]-bond)-fee for a in actions}
    best=max(payoff.values())
    a=next(a for a in actions if payoff[a]==best)
    return a,best


def contract(bond=F(),fee=F(),full_A=False,hard_partner_floor=False,assume_join=False):
    results=[]
    for w in WORLDS:
        action,payoff=player_late(w,bond,fee,full_A)
        partner=(PARTNER_A if action=='A' else PARTNER_B+bond)+fee
        results.append((w,action,payoff,partner))
    partner_expected=sum((row[3] for row in results),F())/2
    join=(partner_expected>=OUTSIDE)
    if hard_partner_floor:
        join=join and all(row[3]>=OUTSIDE for row in results)
    if not join and not assume_join:
        # EXIT: can still complete B, but A is physically unavailable.
        return {'player':F(9),'partner':OUTSIDE,'join':False,'late':results,
                'A':F(),'B':F(1),'partner_harm':F()}
    return {'player':sum((row[2] for row in results),F())/2,
            'partner':partner_expected,'join':join,'late':results,
            'A':sum((F(a=='A',2) for w,a,p,r in results),F()),
            'B':sum((F(a=='B',2) for w,a,p,r in results),F()),
            'partner_harm':sum((F(partner<0,2) for w,a,p,partner in results),F())}


def best_offer(values,kind='bond',hard=False):
    choices=[]
    for transfer in values:
        kw={'bond':transfer} if kind=='bond' else {'fee':transfer}
        r=contract(**kw,hard_partner_floor=hard)
        choices.append((r['player'],transfer,r))
    opt=max(score for score,t,r in choices)
    return opt,[(t,r) for score,t,r in choices if score==opt]


def regression():
    assert contract()['join'] is False
    assert contract()['player']==9
    assert contract(bond=1)['join'] is False
    assert contract(bond=2)['join'] is True  # partner tie JOIN
    for d, value, participant, harm in ((2,F(13),F(0),F(1,2)),
                                         (3,F(25,2),F(1,2),F(1,2)),
                                         (6,F(11),F(2),F(0))):
        r=contract(bond=F(d))
        assert r['player']==value and r['partner']==participant
        assert r['A']==r['B']==F(1,2)
        assert r['partner_harm']==harm
    assert contract(bond=10)['player']==9
    assert contract(bond=10)['A']==1 and contract(bond=10)['B']==0
    assert contract(full_A=True)['join'] and contract(full_A=True)['player']==9
    # Risk-neutral partner: upfront fee 1 and bond 2 have same root 13,
    # though their conditional transfers and realized partner losses differ.
    upfront=contract(fee=1)
    bonded=contract(bond=2)
    assert upfront['player']==bonded['player']==13
    assert upfront['partner']==bonded['partner']==0
    assert upfront['partner_harm']==bonded['partner_harm']==F(1,2)
    assert upfront['late'][1][3] == -5 and bonded['late'][1][3]==-4
    # If partner requires ZERO loss in every reachable demand world,
    # contingent bond protects downside more efficiently than flat upfront fee.
    assert contract(bond=6,hard_partner_floor=True)['join']
    assert not contract(fee=1,hard_partner_floor=True)['join']
    assert contract(fee=6,hard_partner_floor=True)['join']
    assert contract(fee=6,hard_partner_floor=True)['player']==8
    assert best_offer(tuple(map(F,range(0,13))),hard=False)[0]==13
    assert best_offer(tuple(map(F,range(0,13))),hard=True)[0]==11
    assert best_offer(tuple(map(F,range(0,13))),kind='fee',hard=False)[0]==13
    assert best_offer(tuple(map(F,range(0,13))),kind='fee',hard=True)[0]==9
    assert [t for t,r in best_offer(tuple(map(F,range(0,13))),hard=False)[1]] ==[F(2)]
    assert [t for t,r in best_offer(tuple(map(F,range(0,13))),hard=True)[1]] ==[F(6)]
    # A real enforceable bond needs capacity to post the collateral.
    # No such liquidity mechanism is simulated; caps only remove legal offers.
    assert best_offer(tuple(map(F,range(0,5))),hard=True)[0] == 9
    assert best_offer(tuple(map(F,range(0,7))),hard=True)[0] == 11
    # Hard no-negative monetary outcome still does not imply the entrusted
    # A outcome has been preserved. In B demand branch player completes B.
    r=contract(bond=6,hard_partner_floor=True)
    assert r['late'][1][1]=='B' and r['late'][1][3]==0
    print('THIRD-PARTY PASS: no contract 9 / binding bond2 risk-neutral 13 / hard-floor bond6 11')
    print('Upfront flat fee1 risk-neutral 13, hard-floor fee6 gives 8 (<outside 9)')
    print('B demand still sacrifices physical A even with bond6, partner monetary loss 0')
    print('LIMIT: conditional transfer contract + own EXIT are old GRC/NEG primitives, no player sessions')


if __name__=='__main__':regression()
