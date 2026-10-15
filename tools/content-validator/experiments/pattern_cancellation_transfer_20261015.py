"""RECIP-05: compensation for exit vs larger success fee.
Sender types H/L choose costly productive proof, player later sees outside R.
Receiver response assumes on-path separating belief H; checks H participation
and L no-mimic strict incentives. Pure candidate, not all equilibria.
"""
from fractions import Fraction as F
from itertools import product

OUTSIDE=(F(2),F(8))
PROOF_COST={'H':F(2),'L':F(5)}
GOOD=F(8)

def contract(success,cancel):
    t,d=F(success),F(cancel)
    moves=tuple('HONOR' if GOOD-t>=r-d else 'EXIT' for r in OUTSIDE)
    promised=sum((t if a=='HONOR' else d for a in moves),F())/2
    h_gain=promised-PROOF_COST['H']
    l_gain=promised-PROOF_COST['L']
    if not (h_gain>0 and l_gain<0):return None
    by_world={}
    sender={}
    for typ,r in product(('H','L'),OUTSIDE):
        if typ=='L':
            by_world[typ,r]=r;sender[typ,r]=F(0)
        else:
            ix=OUTSIDE.index(r)
            a=moves[ix]
            by_world[typ,r]=GOOD-t if a=='HONOR' else r-d
            sender[typ,r]=(t if a=='HONOR' else d)-PROOF_COST['H']
    return {'player':sum(by_world.values(),F())/4,
            'sender':sum(sender.values(),F())/4,
            'moves':moves,'worlds':by_world,'sender_worlds':sender,
            'high_low_proof_incentives':(h_gain,l_gain)}

def regression():
    success_only=contract(5,0)
    compensated=contract(3,2)
    assert success_only and compensated
    assert success_only['moves']==compensated['moves']==('HONOR','EXIT')
    assert success_only['player']==compensated['player']==F(21,4)
    assert success_only['sender']==compensated['sender']==F(1,4)
    assert success_only['worlds']['H',F(2)]==3
    assert compensated['worlds']['H',F(2)]==5
    assert success_only['worlds']['H',F(8)]==8
    assert compensated['worlds']['H',F(8)]==6
    assert success_only['sender_worlds']['H',F(8)]==-2
    assert compensated['sender_worlds']['H',F(8)]==0
    assert contract(3,0) is None and contract(4,0) is None
    assert sum(OUTSIDE,F())/2==5
    table=[contract(T,D) for T,D in product(range(9),range(4))]
    valid=[r for r in table if r is not None]
    assert len(valid)==15
    assert max(r['player'] for r in valid)==F(21,4)
    print('success_fee_5_cancel_0',success_only)
    print('success_fee_3_cancel_2',compensated)
    print('valid_contracts',len(valid),'best',F(21,4))
    print('RECIP-05 PASS')

if __name__=='__main__':regression()
