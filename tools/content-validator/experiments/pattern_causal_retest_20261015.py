"""CAUSAL05/06: when an imperfect diagnosis is contradicted by real work,
is it valuable to diagnose AGAIN rather than just follow initial evidence?
Second independent DP implementation with a strictly limited test budget.
"""
from fractions import Fraction as F
from functools import lru_cache
from pattern_contextual_causal_probe_20261015 import HI,LO,create_solver,update

def solve(n,max_tests,win=7,fail=-9,q=F(9,10)):
    win,fail,q=F(win),F(fail),F(q)
    @lru_cache(None)
    def before(t,p,k):
        if t==0:return F(0)
        return (choose(t,p,k,0)[0]+choose(t,p,k,1)[0])/2
    @lru_cache(None)
    def choose(t,p,k,c):
        if t==0:return F(0),('END',)
        options={'SAFE':F(1)+before(t-1,p,k)}
        if k>0:
            s,up=update(p,q,1-q)
            _,down=update(p,1-q,q)
            options['CALIBRATE']=s*before(t-1,up,k-1)+(1-s)*before(t-1,down,k-1)
        for action in (0,1):
            p0=HI if action==c else LO
            p1=LO if action==c else HI
            s,up=update(p,p0,p1)
            _,down=update(p,1-p0,1-p1)
            options['RUN'+str(action)]=s*(win+before(t-1,up,k))+(1-s)*(fail+before(t-1,down,k))
        best=max(options.values())
        return best,tuple(a for a,v in options.items() if v==best)
    return before(n,F(1,2),max_tests),choose

def regression():
    expected={2:(F(2),F(71,25),F(71,25)),
              3:(F(17,5),F(143,25),F(143,25)),
              4:(F(148,25),F(5534,625),F(5534,625)),
              5:(F(5419,625),F(7544,625),F(15301,1250)),
              6:(F(7362,625),F(241947,15625),F(245634,15625))}
    for n in range(2,8):
        none,_=solve(n,0)
        one,limited=solve(n,1)
        two,_=solve(n,2)
        unlimited=create_solver(n)[0]
        assert none<=one<=two<=unlimited
        assert none==create_solver(n,can_calibrate=False)[0]
        if n in expected:assert (none,one,two)==expected[n]
        print('n',n,'none',none,'one',one,'two',two,
              'unlimited',unlimited,'marginal_retest',unlimited-one)
    assert solve(5,1)[0]<create_solver(5)[0]
    _,policy=create_solver(5)
    assert policy(5,F(1,2),0)[1]==('CALIBRATE',)
    # On the CAL+ branch, prior P(theta0)=9/10. Running action0
    # in public cue0 then failing yields P(theta0)=9/13.
    _,post=update(F(9,10),F(1,5),F(4,5))
    assert post==F(9,13)
    assert policy(4,F(9,10),0)[1]==('RUN0',)
    assert policy(3,F(9,13),0)[1]==('CALIBRATE',)
    assert limited(3,F(9,13),0,0)[1]!=('CALIBRATE',)
    # P(first calibration +)=1/2; P(public cue0)=1/2;
    # conditional failure probability=13/50.
    probability=F(1,2)*F(1,2)*F(13,50)
    assert probability==F(13,200)
    print('reachable recheck path: CAL+ -> cue0 RUN0 failure ->',
          'posterior 9/13 -> CAL again; P(path)=',probability)
    print('CAUSAL05/06 PASS')

if __name__=='__main__':
    regression()
