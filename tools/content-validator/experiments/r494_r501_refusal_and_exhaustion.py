#!/usr/bin/env python3
"""R494-R501: own-energy limitation makes a private motive ambiguous after refusal."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

ACTIONS=('ASK-A','ASK-B','RESTORE','SELF')
INIT=(('A',0,1),('A',1,1),('B',0,1),('B',1,1))
def clean(rows):
    d={}
    for th,e,w in rows:d[(th,e)]=d.get((th,e),0)+w
    return tuple((th,e,w) for (th,e),w in sorted(d.items()) if w)

def outcomes(rows,act,observe=True,ask_value=3):
    groups={}
    total=sum(w for th,e,w in rows)
    for th,e,w in rows:
        if act.startswith('ASK'):
            yes=(th==act[-1] and e>0)
            obs=('ACCEPT' if yes else 'REFUSE') if observe else 'HIDDEN'
            nxt=0 if yes else e
            reward=ask_value if yes else 0
        elif act=='RESTORE':obs,nxt,reward='-',1,0
        else:obs,nxt,reward='-',e,1
        group=groups.setdefault(obs,[])
        group.append((th,nxt,w,reward))
    out=[]
    for obs,g in groups.items():
        mass=sum(x[2] for x in g)
        out.append((obs,F(mass,total),
                    F(sum(w*r for _,_,w,r in g),mass),
                    clean((th,e,w) for th,e,w,_ in g)))
    return out

def optimal(allow_restore=True,observe=True,ask_value=3,initial=INIT):
    actions=tuple(a for a in ACTIONS if allow_restore or a!='RESTORE')
    @lru_cache(None)
    def v(t,rows):
        if t==4:return F(0)
        return max(sum(p*(r+v(t+1,next_rows)) for _,p,r,next_rows
                       in outcomes(rows,act,observe,ask_value))
                   for act in actions)
    return v(0,clean(initial))

def scripted(script,initial=INIT):
    expected=F(0)
    for theta,e,w in initial:
        rows=clean(((theta,e,1),))
        value=F(0)
        for act in script:
            only=outcomes(rows,act)[0]
            value+=only[2]
            rows=only[3]
        expected+=F(w,sum(a[2] for a in initial))*value
    return expected

def audit():
    assert sum(x[2] for x in INIT)==4
    # When ASK-A was refused, three latent causes remain.
    refusal=[x for x in outcomes(clean(INIT),'ASK-A') if x[0]=='REFUSE'][0]
    assert refusal[1]==F(3,4)
    assert refusal[3]==(('A',0,1),('B',0,1),('B',1,1))  # R494
    root=optimal();assert root==5                       # R495
    fixed={s:scripted(s) for s in product(ACTIONS,repeat=4)}
    fmax=max(fixed.values())
    assert len(fixed)==256 and fmax==4                  # R496
    no_restore=optimal(allow_restore=False)
    no_feedback=optimal(observe=False)
    assert no_restore==no_feedback==4                  # R497 R498
    known_theta=(optimal(initial=(('A',0,1),('A',1,1)))
                 +optimal(initial=(('B',0,1),('B',1,1))))/2
    assert known_theta==6                             # R499
    known_energy=(optimal(initial=(('A',0,1),('B',0,1)))
                  +optimal(initial=(('A',1,1),('B',1,1))))/2
    assert known_energy==F(23,4)                      # R500
    weak=optimal(ask_value=2);strong=optimal(ask_value=4)
    assert weak==4 and strong==F(13,2)                # R501
    # The witness is conditional only on an actual acceptance:
    # RESTORE / ASK-A / [ACCEPT:RESTORE ASK-A; REFUSE:ASK-B SELF].
    a=scripted(('RESTORE','ASK-A','RESTORE','ASK-A'),
               initial=(('A',0,1),('A',1,1)))
    b=scripted(('RESTORE','ASK-A','ASK-B','SELF'),
               initial=(('B',0,1),('B',1,1)))
    assert (a,b)==(6,4) and (a+b)/2==root
    return {'schema':'ssnoir.r494-r501.refusal-or-exhaustion.v1',
        'R494_refusal_posterior':{'probability':refusal[1],'latent_states':refusal[3]},
        'R495_optimal_observed':root,
        'R496_best_of_256_blind_scripts':{'value':fmax,'count':len(fixed)},
        'R497_no_restore':no_restore,'R498_no_response_observation':no_feedback,
        'R499_type_known':known_theta,'R500_energy_known':known_energy,
        'R501_offer_reward_threshold':{'reward_2':weak,'reward_4':strong},
        'constructive_witness':{'theta_A':a,'theta_B':b},
        'protocol':'Four turns, private fixed goal A/B and private initially 0/1 effort independent and equiprobable. Player uses one action ASK-A, ASK-B, RESTORE, SELF. Actor voluntarily ACCEPTS a matching request if effort>0, scoring player 3 and using 1 effort; otherwise REFUSES and earns no benefit. RESTORE sets effort to 1 regardless of private type; SELF guarantees 1. Player sees ACCEPT/REFUSE after ASK and updates beliefs; no unmentioned correct/incorrect score feedback. Actor here has private sustained preference and real effort gating, but does not choose an independent offstage schedule nor plan future contracts.',
        'verdict':'A refusal conflates opposed interest with exhaustion, so its meaning is not a direct identity signal. Optimal opens RESTORE to make subsequent ASK-A diagnostic; adaptive 5 vs all 256 prewritten blind scripts <=4. The whole gain is exactly a short make-test-diagnostic policy; it reduces to intervention+information+resource recovery (R269/P08/P06), not proven persistent social understanding. No fake human.'}
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(t) for t in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
