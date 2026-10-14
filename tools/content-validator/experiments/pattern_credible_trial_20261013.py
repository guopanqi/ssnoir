#!/usr/bin/env python3
"""TRL01-03: productive trial, honest information and strategic cosmetics.

Types G/B equally likely, future job succeeds with probabilities 3/4,1/4.
Hiring yields 16 on success minus 6; SIDE yields 2. A pilot consumes 1
and produces 1 certain unit, net zero; its PASS/FAIL is informative only
when B cannot cosmetic-polish the trial. Low type B may increase pilot
PASS rate from 1/4 to 3/4 without changing actual future competence,
at private cost c. Both sender types receive 4 if hired. Exhaustively
test pure Bayesian best responses; no claimed human/LLM behavior.
"""
from fractions import Fraction as F
from itertools import product

TYPES=('G','B')
SIGS=('P','F')
ACTIONS=('HIRE','SIDE')
PRIOR={'G':F(1,2),'B':F(1,2)}
REAL={'G':F(3,4),'B':F(1,4)}
FUTURE_VALUE=F(16)
HIRE_COST=F(6)
SIDE_VALUE=F(2)
PILOT_WORK=F(1)
PILOT_COST=F(1)
SENDER_HIRE_GAIN=F(4)

def signal_prob(typ,polish,s):
    pass_prob=F(3,4) if typ=='B' and polish else REAL[typ]
    return pass_prob if s=='P' else 1-pass_prob

def posterior(polish,s):
    weighted={t:PRIOR[t]*signal_prob(t,polish,s) for t in TYPES}
    mass=sum(weighted.values())
    assert mass>0
    return {t:weighted[t]/mass for t in TYPES}

def hire_value(polish,s):
    return FUTURE_VALUE*sum(posterior(polish,s)[t]*REAL[t] for t in TYPES)-HIRE_COST

def sender_hire_probability(polish,response):
    return sum(signal_prob('B',polish,s) for s,a in zip(SIGS,response) if a=='HIRE')

def strategy_outcomes(polish,response):
    player=F(0);hired=F(0)
    for s,a in zip(SIGS,response):
        mass=sum(PRIOR[t]*signal_prob(t,polish,s) for t in TYPES)
        player+=mass*(hire_value(polish,s) if a=='HIRE' else SIDE_VALUE)
        hired+=mass*(a=='HIRE')
    return player+PILOT_WORK-PILOT_COST,hired

def pure_equilibria(cosmetic_cost=F(1)):
    equilibria=[]
    for polish,response in product((False,True),product(ACTIONS,repeat=2)):
        rational=True
        for s,a in zip(SIGS,response):
            v=hire_value(polish,s)
            if a=='HIRE' and v<SIDE_VALUE:rational=False
            if a=='SIDE' and v>SIDE_VALUE:rational=False
        if not rational:continue
        chosen=SENDER_HIRE_GAIN*sender_hire_probability(polish,response)-cosmetic_cost*int(polish)
        alternative=SENDER_HIRE_GAIN*sender_hire_probability(not polish,response)-cosmetic_cost*int(not polish)
        if chosen>=alternative:equilibria.append((polish,response,strategy_outcomes(polish,response)))
    return equilibria

def pilot_value(quality=F(3,4),income=F(1),cost=F(1)):
    # An honest trial's signal likelihood is quality for G and 1-quality for B.
    expected=F(0)
    decisions=[]
    for s in SIGS:
        prob={t:(quality if t=='G' else 1-quality) if s=='P'
                 else (1-quality if t=='G' else quality) for t in TYPES}
        mass=sum(PRIOR[t]*prob[t] for t in TYPES)
        p_future=sum(PRIOR[t]*prob[t]*REAL[t] for t in TYPES)/mass
        h=FUTURE_VALUE*p_future-HIRE_COST
        action='HIRE' if h>SIDE_VALUE else 'SIDE'
        expected+=mass*(h if action=='HIRE' else SIDE_VALUE)
        decisions.append((s,action,h))
    return expected+income-cost,tuple(decisions)

def main():
    prior_hire=FUTURE_VALUE*sum(PRIOR[t]*REAL[t] for t in TYPES)-HIRE_COST
    assert prior_hire==SIDE_VALUE==2
    assert pilot_value()==(3,(('P','HIRE',F(4)),('F','SIDE',F(0))))
    assert pilot_value(income=0)[0]==2
    assert pilot_value(income=1,cost=2)[0]==2
    assert posterior(True,'P')==posterior(True,'F')=={'G':F(1,2),'B':F(1,2)}
    assert hire_value(True,'P')==hire_value(True,'F')==2
    assert pure_equilibria(1)==[(True,('HIRE','SIDE'),(F(2),F(3,4)))]
    assert sender_hire_probability(True,('HIRE','SIDE'))*4-1==2
    assert sender_hire_probability(False,('HIRE','SIDE'))*4==1
    assert pure_equilibria(2)==[
        (False,('HIRE','SIDE'),(F(3),F(1,2))),
        (True,('HIRE','SIDE'),(F(2),F(3,4)))]
    assert pure_equilibria(3)==[(False,('HIRE','SIDE'),(F(3),F(1,2)))]
    assert [(q,pilot_value(q)[0]) for q in (F(1,2),F(5,8),F(3,4),F(1))]==[
        (F(1,2),F(2)),(F(5,8),F(5,2)),(F(3,4),F(3)),(F(1),F(4))]
    print('TRL01 baseline 2, honest productive pilot 3, inspection-only 2')
    print('TRL02 imitative-B equilibrium payoff 2 and no posterior type information')
    print('TRL03 imitation cost2 allows 2 pure equilibria; cost3 yields honest one')
    print('TRL04 honest signal quality q 1/2 5/8 3/4 1 -> 2 5/2 3 4')
    print('PASS pure strategic profiles, Bayes, two-agent incentives and controls')

if __name__=='__main__':main()
