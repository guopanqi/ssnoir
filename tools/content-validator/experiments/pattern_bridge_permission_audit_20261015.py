"""BRIDGE13-19: exact equivalence of next-encounter permissions under
different first-encounter histories; retain multiple nonfungible end results.
This is a constructive negative audit for claimed 'nested architecture'.
"""
from fractions import Fraction as F
from itertools import product
from pattern_bridge_two_feedbacks_20261015 import world_distribution,stage2,E,S

HISTORIES={
    'RUSH/no crisis/PRESS':(True,True),
    'RUSH/crisis/PRESS':(True,False),
    'RUSH/crisis/AID':(False,True),
    'WAIT/CERT_E':(True,False),
    'WAIT/CERT_S':(False,True),
}

def next_actions(flags):
    e,s=flags
    return frozenset({'SIDE','ALT1'}|
        ({'E'} if e else set())|({'S'} if s else set()))

def all_legal_suffixes(flags,w):
    """Entire phase2 legal policy/result mapping, NOT only optimal paths."""
    e,s=flags
    first=['SIDE','ALT1']+(['E'] if e else [])+(['S'] if s else [])
    second=['SIDE','ALT2']+(['E'] if e else [])+(['S'] if s else [])
    programs={}
    for a1,a2 in product(first,second):
        if (a1=='ALT1')!=(a2=='ALT2') or (a1 in ('E','S') and a1==a2):
            continue
        if a1=='ALT1':value=F(8);delivered=('ALT',)
        else:
            delivered=tuple(a for a in (a1,a2) if a in ('E','S'))
            value=sum((12 if int(a=='S')==w else 2) for a in delivered)
            value+=sum(a=='SIDE' for a in (a1,a2))
        programs[a1,a2]=(F(value),delivered)
    return programs

def check_same_tickets():
    # Constructive equivalence: all stage2 legal actions, payoffs and
    # results are identical whenever (E,S) permissions are identical.
    for a,b in product(HISTORIES,repeat=2):
        if HISTORIES[a]!=HISTORIES[b]:continue
        assert next_actions(HISTORIES[a])==next_actions(HISTORIES[b])
        for w in (E,S):
            assert stage2(*HISTORIES[a],w)==stage2(*HISTORIES[b],w)
            assert all_legal_suffixes(HISTORIES[a],w)==all_legal_suffixes(HISTORIES[b],w)

def outcome_vector(policy):
    metrics={k:F() for k in ('E','S','ALT','BOTH','SIDE','late_pivot')}
    score=F()
    for hazard,forecast,world,prob in world_distribution():
        if policy=='RUSH_ADAPT':
            action='PRESS' if not hazard or forecast==E else 'AID'
            e,s=(True,not hazard) if action=='PRESS' else (False,True)
            bonus=0
        elif policy=='WAIT':
            e,s=(True,False) if forecast==E else (False,True)
            bonus=1
        elif policy=='RUSH_PRESS':
            e,s=(True,not hazard);bonus=0
        elif policy=='RUSH_AID_ON_CRISIS':
            e,s=(True,True) if not hazard else (False,True)
            bonus=0
        else:raise ValueError(policy)
        payoff,plans=stage2(e,s,world)
        actions,results=plans[0][1:]
        score+=prob*(payoff+bonus)
        for k in ('E','S','ALT'):
            if k in results:metrics[k]+=prob
        if 'E' in results and 'S' in results:metrics['BOTH']+=prob
        if 'SIDE' in actions:metrics['SIDE']+=prob
        if forecast!=world and 'ALT' in results:metrics['late_pivot']+=prob
    return score,metrics

def regression():
    check_same_tickets()
    a,am=outcome_vector('RUSH_ADAPT')
    b,bm=outcome_vector('WAIT')
    c,cm=outcome_vector('RUSH_PRESS')
    d,dm=outcome_vector('RUSH_AID_ON_CRISIS')
    assert (a,b,c,d)==(F(103,8),F(51,4),F(49,4),F(49,4))
    assert am['BOTH']==F(1,2) and bm['BOTH']==0
    assert am['E']==am['S']==F(11,16)
    assert cm['E']==F(3,4) and cm['S']==F(1,2)
    assert dm['S']==F(3,4) and dm['E']==F(1,2)
    assert cm['E']>am['E'] and dm['S']>am['S']
    assert am['ALT']==F(1,8) and bm['ALT']==F(1,4)
    assert am['late_pivot']==F(1,8) and bm['late_pivot']==F(1,4)
    print('adaptive policy',a,am)
    print('safer early waiting',b,bm)
    print('always complete first job',c,cm)
    print('always handle incident',d,dm)
    print('BRIDGE13-19 PASS: permissions are sufficient, multiple end identities')

if __name__=='__main__':regression()
