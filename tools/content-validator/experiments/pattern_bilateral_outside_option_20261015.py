"""RECIP-03/04: a partner invests before a later outside option appears.
Tests pure separating candidate and fully enumerates worlds. Receiver offers
success fee T, observes late outside R if enabled, and chooses honor/exit.
Low-type deviation is checked; not a unique/mixed equilibrium solver.
"""
from fractions import Fraction as F
from itertools import product
OUTSIDE=(F(2),F(7))
COST={'H':F(1),'L':F(5)}
BASE={'H':F(8),'L':F(-8)}

def receiver_policy(offer,penalty=F(0),observed=True):
    t=F(offer)
    if observed:
        return tuple('HONOR' if BASE['H']-t>=r-penalty else 'EXIT' for r in OUTSIDE)
    mean=sum(OUTSIDE,F())/2
    choice='HONOR' if BASE['H']-t>=mean-penalty else 'EXIT'
    return choice,choice

def candidate(offer,penalty=F(0),observed=True,early=False):
    actions=receiver_policy(offer,penalty,observed)
    if early:
        h=tuple(a=='HONOR' and F(offer)>COST['H'] for a in actions)
        l=tuple(a=='HONOR' and F(offer)>COST['L'] for a in actions)
    else:
        accept=F(sum(a=='HONOR' for a in actions),2)
        h=(F(offer)*accept>COST['H'],)*2
        l=(F(offer)*accept>COST['L'],)*2
    if any(l):return None  # not separating
    total,cost=F(0),F(0)
    worlds={}
    for typ,i in product(('H','L'),range(2)):
        r=OUTSIDE[i]
        prove=(h if typ=='H' else l)[i]
        if prove:
            gain=BASE[typ]-F(offer) if actions[i]=='HONOR' else r-penalty
            cost+=F(1,4)*COST[typ]
        else:gain=r
        worlds[typ,r]=(actions[i] if prove else 'PASS',gain)
        total+=gain/4
    return {'value':total,'offer':offer,'penalty':penalty,'policy':actions,
            'high_prove':h,'low_prove':l,'proof_cost':cost,'worlds':worlds}

def independent_forward(offer,penalty=F(0),observed=True,early=False):
    c=candidate(offer,penalty,observed,early)
    if c is None:return None
    total=F(0)
    for typ,i in product(('H','L'),range(2)):
        r=OUTSIDE[i]
        prove=(c['high_prove'] if typ=='H' else c['low_prove'])[i]
        if not prove:gain=r
        elif c['policy'][i]=='HONOR':gain=BASE[typ]-F(offer)
        else:gain=r-penalty
        total+=gain/4
    assert total==c['value']
    return total

def regression():
    f=candidate(3)
    assert f['policy']==('HONOR','EXIT')
    assert f['value']==F(21,4) and f['proof_cost']==F(1,2)
    assert candidate(2)['value']==F(9,2)
    assert candidate(3,observed=False)['value']==F(19,4)
    assert candidate(3,penalty=F(3))['value']==F(19,4)
    early=candidate(3,early=True)
    assert early['value']==F(21,4) and early['proof_cost']==F(1,4)
    assert candidate(2,penalty=F(3))['value']==F(21,4)
    expected_best={
        (F(0),True,False):F(21,4),
        (F(0),True,True):F(11,2),
        (F(0),False,False):F(21,4),
        (F(0),False,True):F(21,4),
        (F(3),True,False):F(21,4),
        (F(3),True,True):F(21,4),
        (F(3),False,False):F(21,4),
        (F(3),False,True):F(21,4),
    }
    for penalty,observed,early_public in product((F(0),F(3)),(True,False),(False,True)):
        cases=[]
        for offer in range(9):
            case=candidate(offer,penalty,observed,early_public)
            if case:
                assert independent_forward(offer,penalty,observed,early_public)==case['value']
                cases.append((case['value'],offer))
        value,t=max(cases)
        assert value==expected_best[(penalty,observed,early_public)]
        print('penalty',penalty,'observed',observed,'early',early_public,
              'best',value,'offer',t)
    print('RECIP-03/04 PASS')

if __name__=='__main__':regression()
