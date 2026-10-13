#!/usr/bin/env python3
"""SSNoir abstract decision research: small exhaustive models, not human playtests.
Q: evidence integrity × optional result; D: opponent memory × decoy;
W: two-day attention × informative forecast. Toy assumptions, not game tuning.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product


def quality(horizon=4, allow_rush=True, allow_retract=True,
            optional=True, retraction_costs_progress=True):
    """Public state: progress [0,3], blemishes [0,2], side progress [0,2]."""
    def actions(state):
        p, flaw, side = state
        if allow_rush:
            yield 'RUSH', ((F(1, 2), (min(3, p+2), flaw, side)),
                           (F(1, 2), (min(3, p+2), min(2, flaw+1), side)))
        yield 'SAFE', ((F(1), (min(3, p+1), flaw, side)),)
        if optional and side < 2:
            yield 'SIDE', ((F(1), (p, flaw, side+1)),)
        if allow_retract and flaw > 0:
            yield 'RETRACT', ((F(1), (max(0, p-1) if retraction_costs_progress else p,
                                      flaw-1, side)),)
    def payout(state):
        p, flaw, side = state
        return F((7 if flaw == 0 else 2) +
                 (3 if optional and flaw == 0 and side == 2 else 0)) if p >= 3 else F(0)
    @lru_cache(None)
    def solve(remaining, state):
        best = ('STOP', payout(state))
        if not remaining:
            return best
        for name, branches in actions(state):
            value = sum(prob * solve(remaining-1, dest)[1] for prob, dest in branches)
            if value > best[1]:
                best = (name, value)
        return best
    return solve


def quality_fixed_baseline(horizon=4):
    """Enumerate precommitted scripts; illegal actions consume an idle slot."""
    def payout(state):
        p, flaw, side = state
        return F((7 if flaw == 0 else 2) + (3 if side == 2 and flaw == 0 else 0)) if p >= 3 else F(0)
    def apply(st, act):
        p, flaw, side = st
        if act == 'SAFE':
            return ((F(1), (min(3,p+1),flaw,side)),)
        if act == 'RUSH':
            return ((F(1,2), (min(3,p+2),flaw,side)),
                    (F(1,2), (min(3,p+2),min(2,flaw+1),side)))
        if act == 'SIDE' and side < 2:
            return ((F(1), (p,flaw,side+1)),)
        if act == 'RETRACT' and flaw > 0:
            return ((F(1), (max(0,p-1),flaw-1,side)),)
        return ((F(1), st),)
    def run(plan):
        masses = {(0,0,0): F(1)}
        for act in plan:
            if act == 'STOP':
                break
            newer = {}
            for st, mass in masses.items():
                for prob, dest in apply(st,act):
                    newer[dest] = newer.get(dest,F(0)) + mass*prob
            masses = newer
        return sum(mass*payout(st) for st,mass in masses.items())
    return max((run(plan),plan) for plan in
               product(('SAFE','RUSH','SIDE','RETRACT','STOP'),repeat=horizon))


def decoy(horizon=4, allow_decoy=True):
    """WORK on lane updates signal and progress; DECOY only updates signal.
    After every even step, opponent removes 2 from PREVIOUS marked lane.
    Completion rewards: A>=2 =>4; B>=2 =>7; each monotonic in progress.
    """
    @lru_cache(None)
    def solve(t,a,b,previous_mark):
        if t == horizon:
            return ('END',F((4 if a>=2 else 0)+(7 if b>=2 else 0)))
        best = ('END',F(-1))
        for lane in (0,1):
            for kind in (('WORK','DECOY') if allow_decoy else ('WORK',)):
                aa,bb = a,b
                if kind == 'WORK':
                    if lane == 0: aa = min(2,aa+1)
                    else: bb = min(2,bb+1)
                if (t+1) % 2 == 0:
                    if previous_mark == 0: aa = max(0,aa-2)
                    else: bb = max(0,bb-2)
                value = solve(t+1,aa,bb,lane)[1]
                if value > best[1]:
                    best = (kind+str(lane),value)
        return best
    return solve


def windows(b_threshold=3, forecast='early', q_high=F(9,10), q_low=F(1,5)):
    """Two days, two actions/day; A must reach 2 on day1 (base4).
    A completion plus day2 event permits C (2 day2 actions, extra8).
    Persistent B yields9 if b_threshold met by end day2.
    Forecast H/L is equally likely, revealed after first day1 action;
    P(event|H)=q_high; P(event|L)=q_low. Event itself known on day2.
    """
    probs = {'H':q_high,'L':q_low}
    def outcome(day1,event):
        a=day1.count('A'); b=day1.count('B')
        base=F(4 if a==2 else 0)
        payoffs=[]
        for day2 in product('BC',repeat=2):
            if 'C' in day2 and (a<2 or not event): continue
            payoffs.append(base +
                (F(9) if b+day2.count('B') >= b_threshold else 0) +
                (F(8) if a==2 and event and day2.count('C')==2 else 0))
        return max(payoffs)
    def expected(day1,signal):
        q=probs[signal]
        return q*outcome(day1,True)+(1-q)*outcome(day1,False)
    if forecast=='early':
        strategies=[]
        for first in 'AB':
            for h,l in product('AB',repeat=2):
                val=(expected(first+h,'H')+expected(first+l,'L'))/2
                strategies.append((val,first+':H>'+h+',L>'+l))
    elif forecast=='late':
        strategies=[((expected(day1,'H')+expected(day1,'L'))/2,day1)
                    for day1 in product('AB',repeat=2)]
    elif forecast=='oracle':
        chance=(q_high+q_low)/2
        yes=max(outcome(day1,True) for day1 in product('AB',repeat=2))
        no=max(outcome(day1,False) for day1 in product('AB',repeat=2))
        strategies=[(chance*yes+(1-chance)*no,'oracle')]
    else:
        raise ValueError(forecast)
    return max(strategies)


def paid_forecast_first(b_threshold=3):
    """An explicit SCOUT consumes first day1 slot: no time to finish A."""
    return F(9) if b_threshold <= 3 else F(0)


def main():
    print('Q: evidence integrity + side result')
    for horizon in (3,4,5):
        variants={'full':{},'no_rush':{'allow_rush':False},
                  'no_retract':{'allow_retract':False},
                  'no_optional':{'optional':False},
                  'free_clean':{'retraction_costs_progress':False}}
        output={name:str(quality(horizon,**params)(horizon,(0,0,0))[1])
                for name,params in variants.items()}
        print('  steps',horizon,output)
    opt=quality(4)
    print('  after first RUSH: clean',opt(3,(2,0,0)),
          'blemished',opt(3,(2,1,0)))
    print('  strongest fully precommitted 4-step plan:',quality_fixed_baseline(4))
    assert opt(4,(0,0,0))[1]==F(17,2)
    assert opt(3,(2,0,0))[1]==10 and opt(3,(2,1,0))[1]==7
    assert quality_fixed_baseline(4)[0]==7
    assert quality(5)(5,(0,0,0))[1]==10
    print('D: memory-targeting + no-progress decoy')
    for horizon in (2,4,6):
        for t in range(horizon+1):
            for a,b in product(range(3),repeat=2):
                for mark in (0,1):
                    assert decoy(horizon,True)(t,a,b,mark)[1]==decoy(horizon,False)(t,a,b,mark)[1]
        print('  steps',horizon,'with',decoy(horizon,True)(0,0,0,0),
              'without',decoy(horizon,False)(0,0,0,0))
    print('W: cross-day windows and the value of keeping an option')
    for threshold in (2,3,4):
        print('  B threshold',threshold,
              {kind:windows(threshold,kind) for kind in ('early','late','oracle')})
    print('  paid forecast:',paid_forecast_first(3))
    print('  uninformative forecast:',windows(3,'early',F(11,20),F(11,20)))
    assert windows(3,'early')[0]==F(101,10)
    assert windows(3,'late')[0]==9
    assert windows(3,'oracle')[0]==F(213,20)
    assert paid_forecast_first(3)==9
    assert windows(2,'early')[0]==windows(2,'late')[0]==13
    assert windows(4,'early')[0]==windows(4,'late')[0]==9
    assert windows(3,'early',F(11,20),F(11,20))[0]==9
    print('PASS: exhaustive exact Fraction comparisons and ablations')


if __name__=='__main__':
    main()
