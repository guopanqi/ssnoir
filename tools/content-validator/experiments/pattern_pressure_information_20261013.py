#!/usr/bin/env python3
"""INF-P01: Bayesian check whether knowing the source of pressure is useful.

R518/525 had success-generated vs failure-generated pressure, same marginal
hazard, and a single policy optimal for both. Here mode is truly hidden
until GO feedback; exact posterior over both modes rather than cheating
by solving each model separately and averaging. No user data or new game.
"""
from functools import lru_cache
from fractions import Fraction as F

T=6
GOAL=3

def terminal(p):
    return F(12 if p>=GOAL else 2*p)

def make_solver(cap):
    @lru_cache(None)
    def value(t,p,d,prob_success_mode):
        if t==T:return terminal(p),'END'
        possible=[(terminal(p),'STOP'),
                  (value(t+1,p,max(0,d-1),prob_success_mode)[0],'VENT')]
        grouped={}
        # Each genuine GO reveals both progress outcome and the actual
        # pressure movement. These are not two independent random rolls.
        for mode,weight in (('success',prob_success_mode),
                            ('failure',1-prob_success_mode)):
            if not weight:continue
            for success in (0,1):
                np=p+success
                nd=d+(success if mode=='success' else 1-success)
                mass=weight/2
                group=grouped.setdefault((np,nd),[F(0),F(0)])
                group[0]+=mass
                if mode=='success':group[1]+=mass
        go=F(0)
        for (np,nd),(mass,success_mass) in grouped.items():
            if nd>cap:go+=mass*F(-4)
            else:go+=mass*value(t+1,np,nd,success_mass/mass)[0]
        possible.append((go,'GO'))
        return max(possible,key=lambda pair:pair[0])
    return value

def audit():
    for cap,known_values,unknown,paid in (
        (2,(F(31,4),F(29,4)),F(15,2),F(187,32)),
        (3,(F(9),F(15,2)),F(33,4),F(237,32)),
    ):
        f=make_solver(cap)
        a,b=f(0,0,0,F(1))[0],f(0,0,0,F(0))[0]
        free=(a+b)/2
        mixed=f(0,0,0,F(1,2))[0]
        paid_info=(f(1,0,0,F(1))[0]+f(1,0,0,F(0))[0])/2
        assert (a,b)==known_values
        assert (mixed,free,paid_info)==(unknown,unknown,paid)
        assert f(0,0,0,F(1,2))[1]=='GO'
        assert paid_info<mixed
        # The smaller counterfactual is stronger: even from each possible
        # public state (t,p,pressure), free mode revelation has ZERO extra
        # decision value in these finite rules. States need not be reachable.
        inspected=0
        for t in range(T):
            for p in range(GOAL+1):
                for d in range(cap+1):
                    mixed_local=f(t,p,d,F(1,2))[0]
                    fully=(f(t,p,d,F(1))[0]+f(t,p,d,F(0))[0])/2
                    assert mixed_local==fully,(cap,t,p,d,mixed_local,fully)
                    inspected+=1
        print('INF-P01 cap',cap,'known',tuple(map(str,(a,b))),
              'unknown',str(mixed),'free reveal',str(free),
              'paid reveal',str(paid_info),'public states checked',inspected)
    print('PASS INF-P01 exact hidden-type posterior, zero action-value signal, paid-reveal ablation')

if __name__=='__main__':audit()
