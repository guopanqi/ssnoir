#!/usr/bin/env python3
"""R430–437: privately known opponent theta; public policy commitment.
Compare 729 contingency policies to their included 27 fixed plans.
The player ANNOUNCES its whole contingent policy before theta is chosen;
this is commitment, not a spontaneous real social agent.
"""
from itertools import product
from fractions import Fraction as F
import json
RULES='OAB';DEMAND='ABA';COST=F(1,4)

def simulate(policy,theta,veto_at=None):
    prior='O';used=None;player=F(0);other=0;history=[]
    for t in range(3):
        index=0 if t==0 else (1 if used is None else 2) if t==1 else (
            3 if used is None else 4 if used==0 else 5)
        proposed=policy[index]
        veto=(veto_at==t)
        if veto and (used is not None or proposed=='O' or proposed==theta):
            return None
        if veto:used=t
        effective='O' if veto else proposed
        outcome=theta if effective=='O' else effective
        gain=F(outcome==DEMAND[t])-COST*(proposed!=prior)
        player+=gain;other+=int(outcome==theta)
        history.append({'t':t+1,'proposal':proposed,'veto':veto,'outcome':outcome})
        prior=effective
    return player,other,history

def respond(policy,theta,tie):
    z=[simulate(policy,theta,i) for i in (None,0,1,2)]
    legal=[r for r in z if r is not None]
    own=max(r[1] for r in legal)
    best=[r for r in legal if r[1]==own]
    return (min if tie=='hostile' else max)(best,key=lambda r:r[0])

def fixed(s):return s[0]+s[1]*2+s[2]*3

def scan(tie):
    results=[]
    for z in product(RULES,repeat=6):
        p=''.join(z)
        value=sum(respond(p,t,tie)[0] for t in 'AB')/2
        results.append((value,p))
    return results

def best(vals,allowed=lambda p:True):
    xs=[x for x in vals if allowed(x[1])]
    mx=max(x[0] for x in xs)
    return mx,sorted(x[1] for x in xs if x[0]==mx),len(xs)

def audit():
    hostile=scan('hostile');friendly=scan('friendly')
    assert len(hostile)==len(friendly)==3**6==729  # R430
    isfixed=lambda p:p[1]==p[2] and p[3]==p[4]==p[5]
    hfixed=best(hostile,isfixed);ffixed=best(friendly,isfixed)
    assert (hfixed[0],ffixed[0],hfixed[2])==(F(3,2),F(17,8),27)  # R431
    hbest=best(hostile);assert hbest[:2]==(F(7,4),['AAAAAO']) # R432
    fbest=best(friendly);assert fbest[:2]==(F(17,8),['AAAAAA']) # R433
    own_a=respond('AAAAAO','A','hostile')
    assert own_a[:2]==(F(7,4),3) and not any(x['veto'] for x in own_a[2]) # R434
    own_b=respond('AAAAAO','B','hostile')
    assert own_b[:2]==(F(7,4),2) and own_b[2][1]['veto'] # R435
    no_late=best(hostile,lambda p:p[3]==p[4]==p[5])
    assert no_late[0]==F(13,8) # R436
    only_last_one=best(hostile,lambda p:p[0]==p[1]==p[2]==p[3]==p[4])
    assert only_last_one[:2]==(F(7,4),['AAAAAO']) and only_last_one[2]==9 # R437
    return {
      'schema':'ssnoir.r430-r437.private-theta-contingent-commitment.v1',
      'protocol':'theta is uniformly private to counterpart (A/B); player does not see theta before selecting public 6-node contingent policy. Other knows theta and the policy; has one legal veto on a rule excluding its own theta, restoring OPEN that turn; chooses own 3-turn matched preference maximum, ties player-hostile or player-friendly. The policy specifies proposal t0, proposals t1 for no veto/veto0, and t2 for no veto/veto0/veto1. Player cannot rewrite policy mid-game. P demand ABA, changing previous effective rule costs 1/4. 27 fixed plans are a strict subset of all 729.',
      'R430_policy_count':729,
      'R431_fixed_27':{'hostile':hfixed,'friendly':ffixed},
      'R432_R433_full_729':{'hostile':hbest,'friendly':fbest},
      'R434_thetaA_witness':own_a,
      'R435_thetaB_witness':own_b,
      'R436_no_conditional_late_branch':no_late,
      'R437_only_one_contingent_branch':only_last_one,
      'verdict':'Under hostile own-utility ties, one conditional OPEN-at-t2-if-veto1 clause raises player from fixed-plan 3/2 to 7/4 while theta stays private. Under friendly ties fixed AAA already gets 17/8 and contingency adds zero. The 729-state advantage has a one-clause explanation; no novel complex social-agent experience or fun is established.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(v) for v in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
