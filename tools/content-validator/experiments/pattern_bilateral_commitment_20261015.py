"""RECIP-01/02: pure separating incentive and counterfactual audit.
Sender H/L must produce costly proof before collaboration can occur.
This tests a specified pure separating candidate, not equilibrium uniqueness.
A certified substitute does both information verification AND productive work.
"""
from dataclasses import dataclass
from fractions import Fraction as F
from itertools import product

SAFE=F(1)
PRIOR={'H':F(1,2),'L':F(1,2)}
COST={'H':F(1),'L':F(5)}
REWARD=F(3)
HONOR=F(2)
BASE={'GOOD':F(8),'BAD':F(1)}

@dataclass(frozen=True)
class Result:
    valid_separation: bool
    player_value: F | None
    sender_actions: tuple[str,str]
    sender_incentives: tuple[F,F]

def sender_response(contract,q=F(1,2),costs=COST):
    freq={'FULL':F(1),'CONDITIONAL':q,'NO_CONTRACT':F(0)}[contract]
    delta={t:REWARD*freq-costs[t] for t in ('H','L')}
    policy={t:('PROVE' if delta[t]>0 else 'PASS') for t in ('H','L')}
    return policy,delta

def contract_value(contract='CONDITIONAL',q=F(1,2),fee=F(0),costs=COST):
    actions,gains=sender_response(contract,q,costs)
    result=(actions['H'],actions['L'])
    if result==('PROVE','PASS'):
        high=q*(BASE['GOOD']-HONOR)+(1-q)*(SAFE if contract=='CONDITIONAL' else BASE['BAD']-HONOR)
        val=PRIOR['H']*high+PRIOR['L']*SAFE-fee
        return Result(True,val,result,(gains['H'],gains['L']))
    if result==('PASS','PASS'):
        return Result(False,SAFE-fee,result,(gains['H'],gains['L']))
    return Result(False,None,result,(gains['H'],gains['L']))

def certified_substitute(q=F(1,2),cost=F(0)):
    # Stronger than a check: it independently supplies the productive proof.
    return PRIOR['H']*(q*BASE['GOOD']+(1-q)*SAFE)+PRIOR['L']*SAFE-cost

def forward(contract,q,fee=F(0)):
    policy,_=sender_response(contract,q)
    total=F(0)
    for t,event in product(('H','L'),('GOOD','BAD')):
        weight=PRIOR[t]*(q if event=='GOOD' else 1-q)
        if policy[t]=='PASS' or (contract=='CONDITIONAL' and event=='BAD'):
            value=SAFE
        else:value=BASE[event]-HONOR
        total+=weight*value
    return total-fee

def regression():
    q=F(1,2)
    assert contract_value('FULL',q).player_value==F(7,4)
    assert contract_value('CONDITIONAL',q).player_value==F(9,4)
    assert contract_value('NO_CONTRACT',q).player_value==SAFE
    for c in ('FULL','CONDITIONAL','NO_CONTRACT'):
        assert contract_value(c,q).player_value==forward(c,q)
    assert [certified_substitute(q,F(v)) for v in (0,1,2)]==[F(11,4),F(7,4),F(3,4)]
    assert contract_value('CONDITIONAL',q,fee=F(1)).player_value==F(5,4)
    assert contract_value('FULL',q,fee=F(1)).player_value==F(3,4)
    assert contract_value('CONDITIONAL',F(1,4)).sender_actions==('PASS','PASS')
    assert contract_value('CONDITIONAL',F(1,3)).sender_actions==('PASS','PASS')
    for q in (F(1,4),F(1,3),F(1,2),F(3,4),F(1)):
        for c in ('FULL','CONDITIONAL'):
            assert contract_value(c,q).player_value==forward(c,q)
        print('q',q,'full',contract_value('FULL',q).player_value,'conditional',
              contract_value('CONDITIONAL',q).player_value)
    print('RECIP-01/02 PASS')

if __name__=='__main__':regression()
