"""RECIP-06: pure signalling equilibrium multiplicity with off-path beliefs.
Offer T=3, outside bid 2 or 7 equally likely, sender proof H/L costs 1/5.
Proof posterior Bayes-correct on path. With neither type proving, beliefs
off path are not fixed by Bayes. No mixed equilibrium or refinements claimed.
"""
from fractions import Fraction as F
from itertools import product
COST={'H':F(1),'L':F(5)}
BASE={'H':F(8),'L':F(-8)}
BIDS=(F(2),F(7))
T=F(3)

def response(p_h):
    value=p_h*BASE['H']+(1-p_h)*BASE['L']-T
    return tuple('HONOR' if value>=r else 'EXIT' for r in BIDS)

def incentives(policy):
    p=F(sum(a=='HONOR' for a in policy),2)
    return {typ:T*p-COST[typ] for typ in ('H','L')}

def candidate(h,l,offpath=F(0)):
    belief=F(1,2) if h and l else F(1) if h else F(0) if l else offpath
    policy=response(belief)
    gains=incentives(policy)
    supports=(gains['H']>=0 if h else gains['H']<=0) and (
        gains['L']>=0 if l else gains['L']<=0)
    player=F(0)
    for typ,r in product(('H','L'),BIDS):
        proof=h if typ=='H' else l
        player+=(BASE[typ]-T if proof and policy[BIDS.index(r)]=='HONOR' else r)/4
    return {'posterior':belief,'policy':policy,'sender_gains':gains,
            'candidate':supports,'player':player}

def regression():
    good=candidate(True,False)
    assert good['candidate'] and good['posterior']==1
    assert good['policy']==('HONOR','EXIT')
    assert good['sender_gains']=={'H':F(1,2),'L':F(-7,2)}
    assert good['player']==F(21,4)
    assert response(F(13,16))==('HONOR','EXIT')
    assert response(F(12,16))==('EXIT','EXIT')
    for k in range(13):
        bad=candidate(False,False,F(k,16))
        assert bad['candidate'] and bad['player']==F(9,2)
    assert not candidate(False,False,F(13,16))['candidate']
    for h,l in product((False,True),repeat=2):
        print('types_prove',h,l,'outcome',candidate(h,l))
    # Executable low-R escrow commits a response without relying on beliefs.
    assert incentives(('HONOR','EXIT'))=={'H':F(1,2),'L':F(-7,2)}
    print('RECIP-06 PASS; pure candidates, not unique equilibrium certification')

if __name__=='__main__':regression()
