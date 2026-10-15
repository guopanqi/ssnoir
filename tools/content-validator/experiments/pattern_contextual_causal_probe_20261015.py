"""CAUSAL01-04: context-conditioned unknown response law and information actions.
Exact Fraction DP; independent forward enumeration over true worlds and RNG.
The hidden state is a fixed law, not a strategic opponent.
"""
from fractions import Fraction as F
from functools import lru_cache

HI, LO = F(4,5), F(1,5)

def update(p, like0, like1):
    chance=p*like0+(1-p)*like1
    return chance,p*like0/chance

def create_solver(n,win=7,fail=-9,signal_accuracy=F(9,10),
                  show_context=True,can_calibrate=True,remember=True):
    win,fail,q=F(win),F(fail),F(signal_accuracy)
    assert 0<q<1

    @lru_cache(None)
    def before(t,p):
        if t==0:return F(0)
        if show_context:
            return (choose(t,p,0)[0]+choose(t,p,1)[0])/2
        return choose(t,p,None)[0]

    @lru_cache(None)
    def choose(t,p,cue):
        if t==0:return F(0),('END',)
        options={'SAFE':F(1)+before(t-1,p)}
        if can_calibrate:
            s,ps=update(p,q,1-q)
            _,pf=update(p,1-q,q)
            if not remember:ps=pf=F(1,2)
            options['CALIBRATE']=s*before(t-1,ps)+(1-s)*before(t-1,pf)
        for action in (0,1):
            if cue is None:pr0=pr1=F(1,2)
            else:
                pr0=HI if action==cue else LO
                pr1=LO if action==cue else HI
            s,ps=update(p,pr0,pr1)
            _,pf=update(p,1-pr0,1-pr1)
            if not remember:ps=pf=F(1,2)
            options['RUN'+str(action)]=(
                s*(win+before(t-1,ps))+
                (1-s)*(fail+before(t-1,pf)))
        best=max(options.values())
        return best,tuple(a for a,v in options.items() if v==best)
    return before(n,F(1,2)),choose

def forward(n,win=7,fail=-9,signal_accuracy=F(9,10),
            show_context=True,can_calibrate=True,remember=True):
    """Replay the policy in each real hidden world, never leaking theta to DP."""
    answer,choose=create_solver(n,win,fail,signal_accuracy,
                                show_context,can_calibrate,remember)
    total=F(0);tests=F(0);paths=0

    def recur(t,theta,p,mass,score,test_count):
        nonlocal total,tests,paths
        if t==0:
            total+=mass*score
            tests+=mass*test_count
            paths+=1
            return
        for actual_cue in (0,1):
            cue=actual_cue if show_context else None
            action=choose(t,p,cue)[1][0]
            weight=mass/2
            if action=='SAFE':
                recur(t-1,theta,p,weight,score+1,test_count)
            elif action=='CALIBRATE':
                q=F(signal_accuracy)
                for positive in (True,False):
                    like=q if positive==(theta==0) else 1-q
                    _,post=update(p,q if positive else 1-q,
                                  1-q if positive else q)
                    if not remember:post=F(1,2)
                    recur(t-1,theta,post,weight*like,score,test_count+1)
            else:
                a=int(action[-1])
                rate=HI if (a==actual_cue)==(theta==0) else LO
                for success in (True,False):
                    like=rate if success else 1-rate
                    if cue is None:like0=like1=F(1,2)
                    else:
                        like0=HI if a==cue else LO
                        like1=LO if a==cue else HI
                        if not success:like0,like1=1-like0,1-like1
                    _,post=update(p,like0,like1)
                    if not remember:post=F(1,2)
                    recur(t-1,theta,post,weight*like,
                          score+(win if success else fail),test_count)
    for theta in (0,1):
        recur(n,theta,F(1,2),F(1,2),F(0),0)
    assert total==answer,('forward discrepancy',total,answer)
    return answer,tests,paths

def one_calibration_then_fixed(n,win=7,fail=-9,q=F(9,10)):
    # Ignore all subsequent observed work results after one diagnostic.
    success=q*HI+(1-q)*LO
    return (n-1)*(success*win+(1-success)*fail)

def regression():
    n=4
    optimal,expected_tests,paths=forward(n)
    no_test=forward(n,can_calibrate=False)[0]
    cue_hidden=forward(n,show_context=False)[0]
    weak_signal=forward(n,signal_accuracy=F(7,10))[0]
    no_memory=forward(n,remember=False)[0]
    assert (optimal,no_test,cue_hidden,weak_signal,no_memory)==(
        F(5534,625),F(148,25),F(4),F(148,25),F(4))
    assert one_calibration_then_fixed(n)==F(213,25)
    assert expected_tests==1
    _,choose=create_solver(n)
    assert choose(n,F(1,2),0)[1]==('CALIBRATE',)
    assert choose(n,F(1,2),1)[1]==('CALIBRATE',)
    print('n4: dynamic',optimal,'no_test',no_test,'no_cue',cue_hidden,
          'weak_signal',weak_signal,'no_memory',no_memory,
          'fixed_after_first_cal',one_calibration_then_fixed(n),
          'world_paths',paths)
    for n in (2,3,4,5,6):
        for q in (F(7,10),F(4,5),F(9,10)):
            value,tests,_=forward(n,signal_accuracy=q)
            never=forward(n,can_calibrate=False)[0]
            assert value>=never
            print('n',n,'q',q,'optimal',value,'never_test',never,'E_tests',tests)
    print('CAUSAL01-04 PASS')

if __name__=='__main__':
    regression()
