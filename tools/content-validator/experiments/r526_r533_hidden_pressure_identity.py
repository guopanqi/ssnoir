#!/usr/bin/env python3
"""R526-R533: Hidden success- versus failure-trigger pressure, value of information."""
from functools import lru_cache
from fractions import Fraction as F
import json
import r518_r525_pressure_success_failure as prior

TRIGGERS=('success','failure')
T=6
def hidden(cap=2,vent=True,info=False,initial_known=None):
    @lru_cache(None)
    def v(t,p,d,typ):
        if t==T:return prior.finish(p)
        options=[prior.finish(p)]
        if vent:options.append(v(t+1,p,max(0,d-1),typ))
        if info and typ is None:
            options.append(sum((v(t+1,p,d,x) for x in TRIGGERS),F(0))/2)
        possibilities=TRIGGERS if typ is None else (typ,)
        result=F(0)
        for kind in possibilities:
            for hit in (0,1):
                pp=p+hit
                dd=d+(hit if kind=='success' else 1-hit)
                prob=F(1,2*len(possibilities))
                result+=prob*(F(-4) if dd>cap else v(t+1,pp,dd,kind))
        options.append(result)
        return max(options)
    if initial_known is None:return v(0,0,0,None),v.cache_info().currsize,v
    return (sum((v(0,0,0,k) for k in TRIGGERS),F(0))/2, v.cache_info().currsize, v)

def threshold(cap):
    return lambda t,p,d:('STOP' if p>=3 else 'VENT' if d>=cap else 'GO')

def audit():
    unknown,n,visit=hidden()
    assert unknown==F(15,2)                       # R526
    told,_,_=hidden(initial_known=True)
    assert told==F(15,2) and unknown==told        # R527
    simple=sum((prior.evaluate(k,threshold(2)) for k in TRIGGERS),F(0))/2
    assert simple==unknown                        # R528
    paid_first=sum((visit(1,0,0,k) for k in TRIGGERS),F(0))/2
    assert paid_first==F(187,32)                   # R529
    info_allowed=hidden(info=True)[0]
    assert info_allowed==unknown                  # R530
    no_vent=hidden(vent=False)[0]
    assert no_vent==F(35,8)                        # R531
    # One ordinary GO reveals the trigger type: both progression and
    # resulting pressure increment are visible; (hit,delta) is unique.
    reports={}
    for kind in TRIGGERS:
        for hit in (0,1):
            pressure_delta=(hit if kind=='success' else 1-hit)
            reports[(hit,pressure_delta)]=kind
    assert len(reports)==4
    assert reports=={(0,0):'success',(1,1):'success',
                    (0,1):'failure',(1,0):'failure'} # R532
    cap3=hidden(cap=3)[0]
    told3=hidden(cap=3,initial_known=True)[0]
    simple3=sum((prior.evaluate(k,threshold(3),limit=3)
                 for k in TRIGGERS),F(0))/2
    assert cap3==told3==simple3==F(33,4)          # R533
    return {'schema':'ssnoir.r526-r533.hidden-correlation-zero-info-v1',
      'protocol':'As R518-R525, fixed hidden mode success-trigger or failure-trigger, probability 1/2 each; mode remains fixed for all 6 actions. Player sees exact progress/pressure changes after each GO but is not told the correlation mode before acting. Options GO, VENT, STOP; optional INFO consumes a whole action and reveals mode. GO pressure overflow at cap2 => score -4. All states, observations and costs otherwise identical to the public-trigger game. A normal GO itself reveals type because (success,pressure increment) uniquely determines mode.',
      'R526_unknown_trigger_optimal':{'value':unknown,'belief_states':n},
      'R527_known_trigger_ex_ante':told,
      'R528_single_type_blind_threshold':simple,
      'R529_pay_info_first':paid_first,
      'R530_optional_information_action':info_allowed,
      'R531_no_vent':no_vent,
      'R532_normal_GO_reveals_type':{str(k):v for k,v in reports.items()},
      'R533_cap3_replication':{'private_type_optimum':cap3,'public_type_optimum':told3,'simple_threshold':simple3},
      'verdict':'Player can infer unknown causal direction from any ordinary GO, but does not need to: the same understandable VENT-at-cap and STOP-at-goal policy exactly attains each known-mode optimal value. Thus ex ante knowing the hidden pressure trigger has zero decision value at cap2 or cap3; spending an action to learn it first is strictly worse. Adding a mystery variable to a pressure system is not a new investigative gameplay loop unless it changes the actionable best policy. Information with no decision relevance is not player agency.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(x) for x in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
