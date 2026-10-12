#!/usr/bin/env python3
"""R518-R525: same Bernoulli hazard budget, reversed success/hazard correlation.
Inspired by the *actual* archived R37 user feedback, not simulated play.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
import json

T=6
GOAL=3
LIMIT=2
def finish(progress):
    return F(12 if progress>=GOAL else 2*progress)

def trial(trigger,t,p,pressure):
    outcomes=[]
    for success in (0,1):
        new_p=p+success
        new_d=pressure+(success if trigger=='success' else 1-success)
        outcomes.append((F(1,2),new_p,new_d))
    return outcomes

def exact(trigger='success',can_vent=True,limit=LIMIT):
    @lru_cache(None)
    def opt(t,p,d):
        if t==T:return finish(p)
        values=[finish(p)]
        if can_vent:values.append(opt(t+1,p,max(0,d-1)))
        values.append(sum((prob*(F(-4) if nd>limit else opt(t+1,np,nd))
                           for prob,np,nd in trial(trigger,t,p,d)),F(0)))
        return max(values)
    return opt(0,0,0),opt.cache_info().currsize

def evaluate(trigger,policy,limit=LIMIT):
    @lru_cache(None)
    def v(t,p,d):
        if t==T:return finish(p)
        action=policy(t,p,d)
        if action=='STOP':return finish(p)
        if action=='VENT':return v(t+1,p,max(0,d-1))
        assert action=='GO'
        return sum((prob*(F(-4) if nd>limit else v(t+1,np,nd))
                    for prob,np,nd in trial(trigger,t,p,d)),F(0))
    return v(0,0,0)

def all_fixed(trigger):
    result={}
    for tape in product(('GO','VENT','STOP'),repeat=T):
        result[tape]=evaluate(trigger,lambda t,p,d:tape[t])
    maximum=max(result.values())
    return maximum,tuple(t for t,v in result.items() if v==maximum),len(result)

def threshold(t,p,d):
    return 'STOP' if p>=GOAL else 'VENT' if d>=LIMIT else 'GO'

def audit():
    s,ns=exact('success')
    f,nf=exact('failure')
    assert (s,f)==(F(31,4),F(29,4))                # R518
    fixed_s=all_fixed('success')
    fixed_f=all_fixed('failure')
    assert (fixed_s[0],fixed_f[0])==(F(23,4),F(25,4))
    assert fixed_s[2]==fixed_f[2]==3**T==729       # R519
    universal=(evaluate('success',threshold),evaluate('failure',threshold))
    assert universal==(s,f)                        # R520
    no_repair=(exact('success',False)[0],exact('failure',False)[0])
    assert no_repair==(F(15,4),F(5))               # R521
    all_go=(evaluate('success',lambda t,p,d:'GO'),
            evaluate('failure',lambda t,p,d:'GO'))
    assert all_go==(F(-3,2),F(3,2))                # R522
    # Under either rule, each GO has exactly one Bernoulli(1/2)
    # +1 hazard outcome, so the *marginal* risk is held equal.
    for trigger in ('success','failure'):
        assert sorted(nd-d for _,np,nd in trial(trigger,0,0,0))==[0,1]
        assert sum(prob for prob,np,nd in trial(trigger,0,0,0) if nd==1)==F(1,2) #R523
    no_danger=(exact('success',limit=6)[0],exact('failure',limit=6)[0])
    assert no_danger==(F(9),F(9))                  # R524
    cap_three=(exact('success',limit=3)[0],exact('failure',limit=3)[0])
    assert cap_three==(F(9),F(15,2))              # R525
    return {'schema':'ssnoir.r518-r525.pressure-trigger-correlation.v1',
       'protocol':'6 slots, one action each GO/VENT/STOP. GO independently succeeds with p=1/2, +1 progress on success; conditional pressure+1 if (success-trigger) GO success, or if (failure-trigger) GO failure. Both have the SAME marginal pressure+1 p=1/2 on every GO, and the SAME progress successes. VENT costs a slot and reduces pressure by 1, floor0. STOP scores immediately. End score 12 if progress>=3, otherwise 2*progress. If pressure exceeds cap2 during GO, immediate terminal score -4. All outcome feedback observed after each GO. No actor, narrative, or human tester.',
       'R518_correlated_vs_anticorrelated_exact':{'success_burden':s,'failure_burden':f,'states':[ns,nf]},
       'R519_strong_729_public_fixed_scripts':{'success_burden':fixed_s[0],'failure_burden':fixed_f[0],'scripts_each':729,'optimal_script_counts':[len(fixed_s[1]),len(fixed_f[1])]},
       'R520_universal_pressure_threshold':{'success_burden':universal[0],'failure_burden':universal[1],'policy':'Stop after goal; else vent when pressure==2; otherwise GO'},
       'R521_vent_forbidden':{'success_burden':no_repair[0],'failure_burden':no_repair[1]},
       'R522_never_repair':{'success_burden':all_go[0],'failure_burden':all_go[1]},
       'R523_equal_marginal_hazard':'both conditions increment pressure on exactly one of two equiprobable GO outcomes; probability 1/2 per GO',
       'R524_pressure_unbounded':{'success_burden':no_danger[0],'failure_burden':no_danger[1]},
       'R525_threshold_cap_three':{'success_burden':cap_three[0],'failure_burden':cap_three[1]},
       'verdict':'Moving hazard from failed GO to successful GO changes distributions over reachable progress/danger states and exact returns, despite identical marginal hazard and success probabilities. But the simple, readable threshold policy is globally optimal for BOTH cap2 variants. These are potentially useful variants of P11 pressure feedback, not by themselves distinct new decision verbs. Genuine historical R37 feedback suggested both are interesting; it did not test this frozen model.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(t) for t in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
