#!/usr/bin/env python3
"""R406–413: public-goal, same-information sequential vs precommitment.
Counterparty optimizes OWN three-turn utility in both protocols.
The publicly known fixed theta here is NOT R390's hidden theta.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json
DEMAND='ABA'; RULES='OAB'; COST=F(1,4)

def stage(t,previous,proposal,theta,veto):
    assert not veto or (proposal!='O' and proposal!=theta)
    effective='O' if veto else proposal
    actual=theta if effective=='O' else effective
    return (F(actual==DEMAND[t])-COST*(proposal!=previous),F(actual==theta)),effective

def plan(seq,theta,tiebreak):
    candidates=[]
    for veto_at in (None,0,1,2):
        p=o=F(0);previous='O';valid=veto_at is None
        for t,rule in enumerate(seq):
            veto=t==veto_at and rule!='O' and rule!=theta
            if t==veto_at:valid=veto
            (a,b),previous=stage(t,previous,rule,theta,veto)
            p+=a;o+=b
        if valid:candidates.append((p,o,veto_at))
    selfish=max(x[1] for x in candidates)
    ties=[c for c in candidates if c[1]==selfish]
    return (min if tiebreak=='hostile' else max)(ties,key=lambda c:c[0])

def online(theta,tiebreak):
    @lru_cache(None)
    def value(t,previous,veto_left):
        if t==3:return (F(0),F(0),'END',False)
        proposals=[]
        for rule in RULES:
            responses=[]
            possible=(False,True) if veto_left and rule!='O' and rule!=theta else (False,)
            for veto in possible:
                (a,b),effective=stage(t,previous,rule,theta,veto)
                future=value(t+1,effective,veto_left and not veto)
                responses.append((a+future[0],b+future[1],veto))
            selfish=max(x[1] for x in responses)
            ties=[r for r in responses if r[1]==selfish]
            chosen=(min if tiebreak=='hostile' else max)(ties,key=lambda r:r[0])
            proposals.append((*chosen[:2],rule,chosen[2]))
        # Deterministic tie between PLAYER-equal proposals favors other.
        return max(proposals,key=lambda r:(r[0],r[1]))
    root=value(0,'O',True);previous='O';remaining=True;trace=[];p=o=F(0)
    for t in range(3):
        chosen=value(t,previous,remaining)
        rule,veto=chosen[2:]
        (a,b),previous=stage(t,previous,rule,theta,veto)
        p+=a;o+=b;remaining &= not veto
        trace.append({'turn':t+1,'rule':rule,'veto':veto,'effective':previous,
                      'player':str(a),'other':str(b)})
    assert (p,o)==root[:2]
    return root[:2],trace

def audit():
    result={}
    for tb in ('hostile','friendly'):
        cases={}
        for theta in 'AB':
            table={''.join(seq):plan(''.join(seq),theta,tb)
                   for seq in product(RULES,repeat=3)}
            highest=max(z[0] for z in table.values())
            best=[seq for seq,z in table.items() if z[0]==highest]
            live,trace=online(theta,tb)
            cases[theta]={'committed_best':highest,'best_plans':best,
                          'online_player':live[0],'online_other':live[1],
                          'commitment_minus_online':highest-live[0],
                          'online_example':trace}
        result[tb]=cases
    # R406: reproduce earlier precommitment values when theta is unknown.
    for tb,old in [('hostile',F(3,2)),('friendly',F(17,8))]:
        assert max(sum(plan(''.join(s),theta,tb)[0] for theta in 'AB')/2
                   for s in product(RULES,repeat=3))==old
    # R407–410: public type, same legal action menu and cost.
    assert (result['hostile']['A']['committed_best'],result['friendly']['A']['committed_best'])==(2,F(11,4))
    assert (result['hostile']['B']['committed_best'],result['friendly']['B']['committed_best'])==(F(5,4),F(5,2))
    assert (result['hostile']['A']['online_player'],result['friendly']['A']['online_player'])==(2,F(9,4))
    assert (result['hostile']['B']['online_player'],result['friendly']['B']['online_player'])==(F(3,2),F(3,2))
    # R411–412: neither precommitment nor online strictly dominates.
    assert (result['hostile']['A']['commitment_minus_online'],result['friendly']['A']['commitment_minus_online'])==(0,F(1,2))
    assert (result['hostile']['B']['commitment_minus_online'],result['friendly']['B']['commitment_minus_online'])==(F(-1,4),F(1))
    # R413: favorable own-utility tie can involve immediate strategic veto.
    assert result['friendly']['A']['online_example'][0]['veto']
    return {'schema':'ssnoir.r406-r413.online-strategic-other.v1',
      'protocol':'theta=A/B publicly known to both in BOTH arms. Player demand ABA, each rule O/A/B costs 1/4 to change relative to last effective rule. Other picks preferred theta when open and may veto ONE excluding rule, restoring O this turn. Other anticipates future player policy or announced plan and maximizes 3-turn own preference matches. Own-utility ties broken hostile/friendly to player; player ties favor other. Online player sees past veto and rechooses rule each turn; precommitment player announces all three before other responds.',
      'R406_27_plans_regression':{'hostile_hidden_type':'3/2','friendly_hidden_type':'17/8'},
      'R407_R413_public_type_cases':result,
      'limit':'This is a complete-information SPE with specified tiebreak, NOT an empirical social agent; not directly compared numerically to R374/R390 because information timing differs.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(v) for v in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
