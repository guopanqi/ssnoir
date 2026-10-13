#!/usr/bin/env python3
"""RECORD01-06: true source information versus externally claimable proof.

Four actions. Hidden R/B equiprobable. WATCH creates a positive trace
identifying the source but risks injury1/2. TEST_R shields R and identifies
the source by either trace(B) or NO_EVENT(R), with exposure only on B.
A positive trace can always be SUBMITted. An absence can only be
SUBMITted if REGISTER was executed BEFORE testing (a frozen protocol).
REGISTER uses one entire turn. POSTHOC variant can register afterwards.
SAFE_PROBE creates equally valid proof with no injury at one turn.
Generic_LICENSE is exactly identical to REGISTER in every legal action and
state transition. We do not model third-party independent choices.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product

ACT=('REGISTER','WATCH','TEST_R','TEST_B','STOP_R','STOP_B',
     'SUBMIT','SIDE','SAFE_PROBE','REGISTER_AFTER')

def solve(turns=4,safety=6,correct=8,proof=10,mode='prereg'):
    assert mode in ('prereg','generic_license','none','posthoc','safeprobe')
    @lru_cache(None)
    def dp(t,known,record,registered,stopped,submitted,injured):
        if not t:return F(safety)*int(not injured)
        options=[F(1)+dp(t-1,known,record,registered,stopped,submitted,injured)]
        if not stopped:
            for j in ('R','B'):
                p=F(1,2) if known=='?' else F(int(known==j))
                options.append(F(correct)*p+
                               dp(t-1,known,record,registered,True,submitted,injured))
        if not submitted and record!='none' and (record in ('trace','safe') or registered):
            options.append(F(proof)+dp(t-1,known,record,registered,stopped,True,injured))
        if record=='none':
            if not registered and mode in ('prereg','generic_license','posthoc'):
                options.append(dp(t-1,known,record,True,stopped,submitted,injured))
            for method in ('WATCH','TEST_R','TEST_B'):
                total=F(0)
                for theta in ('R','B'):
                    mass=F(1,2) if known=='?' else F(int(known==theta))
                    if not mass:continue
                    exposed=(method=='WATCH' or method[-1]!=theta)
                    rec='trace' if exposed else 'null'
                    if exposed:
                        total+=mass*(dp(t-1,theta,rec,registered,stopped,submitted,injured)+
                                     dp(t-1,theta,rec,registered,stopped,submitted,True))/2
                    else:total+=mass*dp(t-1,theta,rec,registered,stopped,submitted,injured)
                options.append(total)
            if mode=='safeprobe':
                options.append(sum(dp(t-1,theta,'safe',registered,stopped,submitted,injured)
                                   for theta in ('R','B'))/2)
        if mode=='posthoc' and record=='null' and not registered:
            options.append(dp(t-1,known,record,True,stopped,submitted,injured))
        return max(options)
    return dp(turns,'?','none',False,False,False,False)

def fixed(turns=4,safety=6,correct=8,proof=10,mode='prereg'):
    """Enumerate every 10**turns prewritten command string.
    Illegal actions give SIDE=1. None may branch on the actual source or
    subsequently acquired observation. Injury is integrated explicitly.
    """
    best=None;winners=[]
    for plan in product(ACT,repeat=turns):
        total=F(0)
        for theta in ('R','B'):
            record='none';registered=False;stopped=None;submitted=False;side=0
            for ac in plan:
                if ac=='REGISTER' and record=='none' and not registered and mode in ('prereg','generic_license','posthoc'):
                    registered=True
                elif ac=='REGISTER_AFTER' and record=='null' and not registered and mode=='posthoc':
                    registered=True
                elif ac in ('WATCH','TEST_R','TEST_B') and record=='none':
                    record='trace' if ac=='WATCH' or ac[-1]!=theta else 'null'
                elif ac=='SAFE_PROBE' and record=='none' and mode=='safeprobe':
                    record='safe'
                elif ac in ('STOP_R','STOP_B') and stopped is None:
                    stopped=ac[-1]
                elif ac=='SUBMIT' and not submitted and record!='none' and (record in ('trace','safe') or registered):
                    submitted=True
                else:side+=1
            for injured in (False,True):
                harm=injured and record=='trace'
                total+=F(1,4)*(safety*int(not harm)+correct*int(stopped==theta)+
                               proof*int(submitted)+side)
        if best is None or total>best:best=total;winners=[plan]
        elif total==best:winners.append(plan)
    return best,tuple(winners)

def policy_vectors():
    # (P(no injury),P(correct STOP),P(accepted SUBMIT),E(SIDE))
    return {'WATCH_POSITIVE':(F(1,2),F(1),F(1),F(1)),
            'PREREGISTER_TEST_R':(F(3,4),F(1),F(1),F(0)),
            'POSTHOC_REGISTER':(F(3,4),F(1),F(1),F(1,2)),
            'SAFE_PROBE':(F(1),F(1),F(1),F(1))}

def main():
    roots={m:solve(mode=m) for m in ('none','prereg','generic_license','posthoc','safeprobe')}
    assert roots=={'none':22,'prereg':F(45,2),'generic_license':F(45,2),
                   'posthoc':23,'safeprobe':25},roots
    assert {m:fixed(mode=m)[0] for m in roots}=={
        'none':18,'prereg':F(37,2),'generic_license':F(37,2),
        'posthoc':19,'safeprobe':21}
    assert [solve(turns=t,mode='prereg') for t in (3,4,5)]==[21,F(45,2),F(47,2)]
    assert [solve(turns=t,mode='posthoc') for t in (3,4,5)]==[21,23,24]
    vec=policy_vectors()
    for v in vec.values():
        assert len(v)==4 and all(0<=z<=1 for z in v[:3])
    for mode,value in roots.items():
        print(mode,'dynamic',value,'best prewritten',fixed(mode=mode)[0])
    print('policy outcome vectors',vec)
    print('PASS: precommitted null evidence, late registration, safe proof, strongest robot licensing')

if __name__=='__main__':main()
