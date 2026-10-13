#!/usr/bin/env python3
"""PART01-04: selective protection, evidence by absence, and future test value.

Hidden event source is R/B, each prior1/2. A potential incident occurs with
chance q. FULL prevents it regardless of source. WATCH protects neither.
PARTIAL_R protects R: an R event is suppressed; an unprotected B event is
visible, providing a true B trace. For WATCH, any event reveals its source.
When an event is not prevented, damage chance is 1/2 independently.
Three remaining actions: one noisy PROBE of accuracy3/4, one STOP_R/B, SIDE.
The player's posterior after NO_EVENT differs by intervention; the DP never
looks at true source without an observation. All exact rational arithmetic.
"""
from fractions import Fraction as F
from functools import lru_cache

def continuation(stop_value=12,side_value=1,accuracy=F(3,4)):
    @lru_cache(None)
    def dp(turns,p,probed,stopped):
        if not turns:return F(0)
        options=[F(side_value)+dp(turns-1,p,probed,stopped)]
        if not stopped:
            options.extend(F(stop_value)*(p if t=='R' else 1-p)+
                           dp(turns-1,p,probed,True) for t in ('R','B'))
        if not probed:
            inspect=F(0)
            for report in ('R','B'):
                rlike=accuracy if report=='R' else 1-accuracy
                blike=1-accuracy if report=='R' else accuracy
                p_report=p*rlike+(1-p)*blike
                if p_report:inspect+=p_report*dp(turns-1,p*rlike/p_report,True,stopped)
            options.append(inspect)
        return max(options)
    return lambda p:dp(3,p,False,False)

def solve(q=F(1),safety_value=6,stop_value=12,side_value=1):
    q=F(q);s=F(safety_value);tail=continuation(stop_value,side_value)
    full=s+tail(F(1,2))
    # WATCH: true trace when an event happens, no event leaves prior unchanged.
    watch=s*(1-q/2)+q/2*tail(F(1))+q/2*tail(F(0))+(1-q)*tail(F(1,2))
    # Selectively guard R. A B event is seen with probability q/2; all
    # remaining observations are NO_EVENT, with posterior P(R)=1/(2-q).
    no_event=1-q/2
    posterior_r=F(1,2)/no_event
    partial=s*(1-q/4)+q/2*tail(F(0))+no_event*tail(posterior_r)
    return {'FULL':full,'WATCH':watch,'PARTIAL_R':partial,'PARTIAL_B':partial}

def simple_one_shot(q):
    q=F(q)
    # Transparent policies that STOP the most likely target immediately
    # after the incident signal, then make two SIDE moves.
    return {'FULL_PROBE':(F(1),F(3,4),F(1)),
            'WATCH_TRACE':(1-q/2,(1+q)/2,F(2)),
            'PARTIAL_TRACE_OR_ABSENCE':(1-q/4,(1+q)/2,F(2))}

def main():
    cases={
        F(1):{'FULL':16,'WATCH':17,'PARTIAL_R':F(37,2),'PARTIAL_B':F(37,2)},
        F(1,2):{'FULL':16,'WATCH':F(33,2),'PARTIAL_R':F(65,4),'PARTIAL_B':F(65,4)},
        F(1,4):{'FULL':16,'WATCH':F(65,4),'PARTIAL_R':F(129,8),'PARTIAL_B':F(129,8)}
    }
    for q,expected in cases.items():
        found=solve(q)
        assert found==expected,(q,found,expected)
        vec=simple_one_shot(q)
        # Partial shielding offers the SAME one-shot correctness as WATCH,
        # with LESS harm; this DOES NOT certify sequential DP dominance.
        assert vec['WATCH_TRACE'][1]==vec['PARTIAL_TRACE_OR_ABSENCE'][1]
        assert vec['PARTIAL_TRACE_OR_ABSENCE'][0]>vec['WATCH_TRACE'][0]
        p_no=1-q/2
        assert F(1,2)/p_no==1/(2-q)
        assert q/2+p_no*(F(1,2)/p_no)==(1+q)/2
        print('chance of event without intervention',q,'DP',found,'transparent policy vectors',vec)
    assert cases[F(1)]['PARTIAL_R']>cases[F(1)]['WATCH']
    assert cases[F(1,2)]['PARTIAL_R']<cases[F(1,2)]['WATCH']
    assert solve(F(1),12,8,1)['FULL']==19
    assert solve(F(1),12,8,1)['PARTIAL_R']==19
    print('PASS: absence as evidence, uncertain event baseline, sequential probe value reversal')

if __name__=='__main__':main()
