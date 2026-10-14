#!/usr/bin/env python3
"""Two independent exact studies; standard library; NOT a player test.

EVIDENCE: four actions; registration, endogenous negative observation, actor veto.
DISCLOSURE: private type, verifiable R report, rational updating from silence.
"""
from fractions import Fraction as F

def choose(choices, priority):
    best=max(choices.values())
    return best,next(a for a in priority if a in choices and choices[a]==best)

def partner(proof, outside):
    return 'HELP' if (4 if proof else -6)>outside else 'EXIT'

def leaf(reg,method,truth,p,posthoc):
    proof=method in ('WATCH','SAFE') or method=='SELECT_R' and (truth=='B' or reg)
    danger=method=='WATCH' or method=='SELECT_R' and truth=='B'
    safe=F(1,2) if danger else F(1)
    # Independent outside opportunity 1 w.p. p, 6 w.p. 1-p.
    probhelp=sum((w for outside,w in ((F(1),p),(F(6),1-p))
                  if partner(proof,outside)=='HELP'),F(0))
    reward=F(12 if truth=='R' else 10)
    choices={'DIRECT':F(5),'SIDE':1+max(F(4),probhelp*reward)}
    if proof: choices['CONTACT']=4+probhelp*(reward-3)
    if posthoc and method=='SELECT_R' and truth=='R' and not reg:
        choices['REGISTER_AFTER']=p*reward  # no last-action rescue after veto
    v,a=choose(choices,('CONTACT','REGISTER_AFTER','DIRECT','SIDE'))
    return v+6*safe,a,safe,proof

def evidence(p=F(1,2),register=True,posthoc=False,safe_probe=False):
    plans=[]
    for first in (('REGISTER','SIDE') if register else ('SIDE',)):
        for method in ('SELECT_R','WATCH','FULL')+(('SAFE',) if safe_probe else ()):
            rows=[leaf(first=='REGISTER',method,x,p,posthoc) for x in ('R','B')]
            v=F(first=='SIDE')+(rows[0][0]+rows[1][0])/2
            plans.append((v,first,method,rows))
    v=max(x[0] for x in plans)
    return next(row for row in plans if row[0]==v)

def evidence_forward(plan,p):
    # Independent forward enumeration over hidden source, harm, actor outside.
    _,first,method,rows=plan
    total=F(0);safe_mass=F(0);help_mass=F(0);no_event_mass=F(0)
    for i,truth in enumerate(('R','B')):
        action=rows[i][1]
        for harm in (0,1):
            for outside,prob in ((F(1),p),(F(6),1-p)):
                mass=prob/4
                danger=method=='WATCH' or method=='SELECT_R' and truth=='B'
                safe=not(danger and harm)
                proof=method in ('WATCH','SAFE') or method=='SELECT_R' and (truth=='B' or first=='REGISTER')
                benefit=F(first=='SIDE')+6*safe
                reward=F(12 if truth=='R' else 10)
                if action=='CONTACT':
                    if partner(proof,outside)=='HELP':
                        benefit+=reward+1;help_mass+=mass
                    else:benefit+=4
                elif action=='REGISTER_AFTER':
                    assert method=='SELECT_R' and truth=='R' and first=='SIDE'
                    if partner(True,outside)=='HELP':
                        benefit+=reward;help_mass+=mass
                elif action=='DIRECT':benefit+=5
                else:
                    benefit+=1
                    if proof and p*reward>4:
                        if partner(True,outside)=='HELP':
                            benefit+=reward;help_mass+=mass
                    else:benefit+=4
                total+=mass*benefit;safe_mass+=mass*safe
                no_event_mass+=mass*(method=='SELECT_R' and truth=='R')
    assert total==plan[0]
    return {'value':total,'safe':safe_mass,'help':help_mass,'silent_observation':no_event_mass}

def posterior_r_if_silent(q):
    # R protected, natural event probability q. Silence is not always proof.
    posterior=1/(2-q)
    return posterior,posterior*4-(1-posterior)*6

def disclosure(outside=F(1),bonus=2,precommit=False,automatic=False):
    # Player knows R/B before action, each 1/2. Only R may publish verified R.
    # Actor: INVEST yields own +5 if R / -2 if B; EXIT yields outside.
    # Player: INVEST silent 8, INVEST disclosed R 8+bonus, EXIT backup 4.
    def reply(pR):return 'INVEST' if 5*pR-2*(1-pR)>outside else 'EXIT'
    cases=[]
    for reveal in ((False,) if precommit else (False,True)):
        silentR=F(0) if reveal else F(1,2)
        aS='INVEST' if automatic else reply(silentR)
        aR='INVEST' if automatic else reply(F(1))
        payoffS=8 if aS=='INVEST' else 4
        payoffR=8+bonus if aR=='INVEST' else 4
        if precommit or reveal and payoffR>=payoffS or not reveal and payoffS>=payoffR:
            playerR=payoffR if reveal else payoffS
            playerB=payoffS
            actorR=5 if (aR if reveal else aS)=='INVEST' else outside
            actorB=-2 if aS=='INVEST' else outside
            cases.append({'reveal':reveal,'R':(F(playerR),F(actorR)),
                          'B':(F(playerB),F(actorB)),
                          'player':F(playerR+playerB,2),
                          'actor':F(actorR+actorB,2),
                          'backup':F(int((aR if reveal else aS)=='EXIT')+int(aS=='EXIT'),2)})
    return cases

def verify():
    p=F(1,2)
    plan=evidence(p)
    assert plan[:3]==(F(25,2),'REGISTER','SELECT_R')
    assert [r[1] for r in plan[3]]==['CONTACT','CONTACT']
    assert evidence_forward(plan,p)=={'value':F(25,2),'safe':F(3,4),'help':p,'silent_observation':p}
    assert evidence(p,register=False)[0]==12
    assert evidence(p,register=False,posthoc=True)[0]==F(49,4)
    assert evidence(p,posthoc=True)[0]==F(25,2)
    assert evidence(p,safe_probe=True)[0]==15
    assert evidence(F(1),posthoc=False)[0]==F(33,2)
    assert evidence(F(1),posthoc=True)[0]==17
    assert evidence(F(3,4),posthoc=True)[:3]==(F(117,8),'SIDE','SELECT_R')
    assert evidence(F(0),posthoc=True)[0]==12
    for p in (F(0),F(1,4),F(1,2),F(3,4),F(1)):
        pl=evidence(p,posthoc=True)
        assert evidence_forward(pl,p)['value']==pl[0]
        print('EVIDENCE',p,str(pl[0]),pl[1],pl[2],[r[1] for r in pl[3]])
    assert posterior_r_if_silent(F(1))==(1,4)
    assert posterior_r_if_silent(F(1,2))==(F(2,3),F(2,3))
    base=disclosure();assert len(base)==1
    assert (base[0]['reveal'],base[0]['player'],base[0]['actor'],base[0]['backup'])==(True,7,3,F(1,2))
    closed=disclosure(precommit=True)
    assert (closed[0]['player'],closed[0]['actor'],closed[0]['backup'])==(8,F(3,2),0)
    assert disclosure(automatic=True)[0]['player']==9
    assert disclosure(outside=2)[0]['player']==7
    assert disclosure(outside=2,precommit=True)[0]['player']==4
    assert len(disclosure(bonus=0))==2 # two weak equilibria: no uniqueness claim
    print('DISCLOSURE optional',base,'credible conceal',closed)
    print('PASS exact source/actor/decision and independent forward checks')

if __name__=='__main__':verify()
