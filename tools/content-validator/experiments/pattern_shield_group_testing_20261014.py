#!/usr/bin/env python3
"""GROUP01-04: protection-as-diagnosis, exact subset posterior and strong baselines.

N equally likely possible sources. Each TEST(S) protects subset S; if source
outside S the event occurs, its source is accurately observed, and independent
injury occurs with probability 1/2. Otherwise NO_EVENT, with posterior
restricted to S. At most 3 player actions: TEST(S), STOP(i), SIDE(+1).
Completion rewards correct STOP, no injury, and SIDE separately.

All-subset vs singleton-only vs no testing are FULL initial-state DPs,
not comparisons at arbitrarily chosen local histories. Exact probabilities.
"""
from functools import lru_cache
from fractions import Fraction as F

def solve(n=4,turns=3,safety=6,correct=12,side=1,mode='all'):
    full=(1<<n)-1
    @lru_cache(None)
    def dp(t,possible,injured,stopped):
        if not t:return F(safety)*int(not injured)
        choices=[F(side)+dp(t-1,possible,injured,stopped)]
        if not stopped:
            for i in range(n):
                choices.append(F(correct)*F(bool(possible&(1<<i)),possible.bit_count())+
                               dp(t-1,possible,injured,True))
        if mode!='none':
            for shield in range(1,full+1):
                if mode=='singleton' and shield.bit_count()!=1:continue
                inside=shield&possible
                total=F(0)
                if inside:
                    total+=F(inside.bit_count(),possible.bit_count())*dp(t-1,inside,injured,stopped)
                for i in range(n):
                    if possible&(1<<i) and not shield&(1<<i):
                        # The exposed incident identifies i; damage probability1/2.
                        total+=F(1,possible.bit_count())*(
                            dp(t-1,1<<i,injured,stopped)+
                            dp(t-1,1<<i,True,stopped))/2
                choices.append(total)
        return max(choices)
    return dp(turns,full,False,False)

def one_singleton_witness(n=4,safety=6,correct=12,side=1):
    # One TEST protects source 0. Absence uniquely identifies 0,
    # otherwise the trace uniquely identifies some other source.
    no_injury=F(1,n)+F(n-1,n)*F(1,2)
    return F(safety)*no_injury+correct+side,(no_injury,F(1),F(1))

def minimum_exposure_bound(n):
    # Among source types that cause no exposure at any TEST:
    # every test result is NO_EVENT, so the policy's zero-exposure
    # history is identical. At most one source can be identified by it.
    # Uniform prior => P(at least one exposed incident)>=(n-1)/n.
    return F(n-1,n)

def main():
    for s,c,expected,baseline in [
        (6,12,F(67,4),11),
        (12,8,F(33,2),16),
        (0,12,13,5),
        (20,12,F(51,2),25),
    ]:
        all_result=solve(safety=s,correct=c)
        singleton=solve(safety=s,correct=c,mode='singleton')
        no_tests=solve(safety=s,correct=c,mode='none')
        assert (all_result,singleton,no_tests)==(expected,expected,baseline)
        print('weights',s,c,'all',all_result,'singleton',singleton,'none',no_tests)
    val,vec=one_singleton_witness()
    assert val==solve()
    assert vec==(F(5,8),F(1),F(1))
    assert minimum_exposure_bound(4)==F(3,4)
    for n in (2,3,4,5):
        all_result=solve(n=n)
        singleton=solve(n=n,mode='singleton')
        witness,_=one_singleton_witness(n=n)
        assert all_result==singleton==witness,(n,all_result,singleton,witness)
        assert minimum_exposure_bound(n)==F(n-1,n)
        print('n',n,'optimal',all_result,'minimum-exposure',minimum_exposure_bound(n))
    # Not a universal theorem about ALL possible weighted utilities;
    # this grid is only a regression check under this exact protocol.
    for s in (0,2,4,6,8,12,20,30):
        for c in (2,4,8,12,16,24):
            assert solve(safety=s,correct=c)==solve(safety=s,correct=c,mode='singleton')
    print('PASS subset-testing versus one targeted test and no-probe controls')

if __name__=='__main__':main()
