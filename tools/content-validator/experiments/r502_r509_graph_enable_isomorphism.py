#!/usr/bin/env python3
"""R502-R509: simultaneous faults with repair-changing future action legality."""
from itertools import product
from fractions import Fraction as F
import json

FAULTS=list(product((False,True),repeat=2))
DEMANDS='XY'
def stage2(faults,demand):
    allowed=[task for task,i in [('X',0),('Y',1)] if not faults[i]]
    return max((F(4 if task==demand else 1) for task in allowed),default=F(0))

def setup(faults,repair,cost,mode='graph'):
    fx,fy=faults
    if repair=='KEEP':return faults,F(0)
    index=0 if repair=='X' else 1
    if mode=='graph':
        # Repair an upstream edge; later FINISH uses its resulting validity.
        after=list(faults);after[index]=False
    elif mode=='flat':
        # One-turn ENABLE of the identical task action, no graph required.
        after=list(faults);after[index]=False
    else:raise ValueError(mode)
    return tuple(after),F(cost)

def choices(faults,demand=None,cost=1,allow_repair=True,mode='graph'):
    legal=('KEEP','X','Y') if allow_repair else ('KEEP',)
    options=[]
    for act in legal:
        if mode=='flat':
            # Independently enumerate the enabled task MENU directly,
            # without calling graph fault-transition or stage2().
            enabled={task for task,i in [('X',0),('Y',1)] if not faults[i]}
            if act!='KEEP':enabled.add(act)
            def flat_final(q):
                return max((F(4 if task==q else 1) for task in enabled),default=F(0))
            payoff=(flat_final(demand) if demand else
                    sum((flat_final(q) for q in DEMANDS),F(0))/2)
            payoff-=F(cost if act!='KEEP' else 0)
        else:
            nxt,paid=setup(faults,act,cost,mode)
            payoff=(stage2(nxt,demand) if demand else
                    sum((stage2(nxt,q) for q in DEMANDS),F(0))/2)-paid
        options.append((act,payoff))
    top=max(v for a,v in options)
    return top,tuple(a for a,v in options if v==top)

def root(demand_visible=True,cost=1,allow_repair=True,mode='graph'):
    if demand_visible:
        return sum((choices(f,q,cost,allow_repair,mode)[0]
                    for f in FAULTS for q in DEMANDS),F(0))/8
    return sum((choices(f,None,cost,allow_repair,mode)[0]
                for f in FAULTS),F(0))/4

def audit():
    both=(True,True)
    assert F(1,4)==F(len([f for f in FAULTS if f==both]),len(FAULTS)) # R502
    both_cho=[choices(both,q) for q in DEMANDS]
    assert both_cho==[(F(3),('X',)),(F(3),('Y',))] # R503
    assert choices(both,None)[0]==F(3,2) # R504
    full=root();assert full==F(7,2) # R505
    no_repair=root(allow_repair=False);assert no_repair==F(9,4) # R506
    hidden=root(demand_visible=False);assert hidden==F(23,8) # R507
    assert all(choices(f,q,mode='flat')==choices(f,q,mode='graph')
               for f in FAULTS for q in DEMANDS)
    assert all(choices(f,None,mode='flat')==choices(f,None,mode='graph')
               for f in FAULTS)
    assert root(mode='flat')==full and root(False,mode='flat')==hidden # R508
    free=root(cost=0);costly=root(cost=4)
    assert free==4 and costly==F(9,4) # R509
    return {'schema':'ssnoir.r502-r509.two-fault-enable-isomorphism.v1',
      'protocol':'Two independent public fault bits X/Y, each 0/1 equally likely; public demand q=X/Y independent uniform, either known or still hidden at repair stage. Player has one preparation action KEEP or REPAIR-X/Y costing 1 utility, creating a future legal FINISH-X/Y if that branch is healthy; after q is revealed, one FINISH action. Matching demand pays 4, nonmatching pays 1; no available tasks pay 0. Compare a dependency-edge description with a task-ENABLE description that maps each repair and valid FINISH one-to-one. No pre-existing work, hidden type or social actor.',
      'R502_both_faults_reachable':F(1,4),
      'R503_when_both_broken_public_demand':{q:{'score':v,'optimal_actions':a}
            for q,(v,a) in zip(DEMANDS,both_cho)},
      'R504_both_broken_late_demand':choices(both,None)[0],
      'R505_public_demand_adaptive':full,
      'R506_no_repair':no_repair,
      'R507_demand_only_after_repair':hidden,
      'R508_flattening_checks':{'public_states':len(FAULTS)*len(DEMANDS),
                               'hidden_demand_states':len(FAULTS),
                               'same_roots':True},
      'R509_cost_counterfactual':{'free':free,'too_expensive':costly},
      'verdict':'Both dependencies can genuinely fail at the same time and repair changes the NEXT TURN legal FINISH menu. Yet a flat ENABLE-X/Y action has exactly the same availability, cost and feedback in all reachable states; the player is simply deciding which task option to purchase. Public opportunity information matters (7/2 vs 23/8) but not the graph nomenclature. The stronger graph did not prove a distinct decision verb or human experience.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
