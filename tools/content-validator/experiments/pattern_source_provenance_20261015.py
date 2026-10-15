"""SOURCE01-06: correlated witness reports and source-audit vs fresh witness.
Exact Fraction posterior DP; forward full-world replay, cost and oracle ablations.
Prior T and duplicate D independent 1/2. Report1 correct .75.
Report2 is literal copy of report1 when D, otherwise independent .75.
Report3 always new and independent .75.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
ACC=F(3,4); SAFE=F(3); RIGHT=F(10); WRONG=F(-28)
WORLDS={}
for t,d,s1,s2,s3 in product((0,1),repeat=5):
    w=F(1,4)
    w*=ACC if s1==t else 1-ACC
    w*=(F(1) if s1==s2 else F()) if d else (ACC if s2==t else 1-ACC)
    w*=ACC if s3==t else 1-ACC
    if w:WORLDS[t,d,s1,s2,s3]=w
assert sum(WORLDS.values(),F())==1

def solve(limit=1,audit=True,new=True,oracle=False,cost_new=F(1),
          cost_audit=F(1),independent_only=False):
    worlds={k:v*(2 if independent_only else 1) for k,v in WORLDS.items()
            if not independent_only or k[1]==0}
    assert sum(worlds.values(),F())==1
    @lru_cache(None)
    def decide(s1,s2,d=None,s3=None,t=None,remaining=1):
        matched={k:v for k,v in worlds.items() if k[2:4]==(s1,s2)
                 and (d is None or k[1]==d)
                 and (s3 is None or k[4]==s3)
                 and (t is None or k[0]==t)}
        mass=sum(matched.values(),F());assert mass
        p=sum((v for k,v in matched.items() if k[0]==0),F())/mass
        choices={'SAFE':SAFE,'CLAIM0':p*RIGHT+(1-p)*WRONG,
                 'CLAIM1':(1-p)*RIGHT+p*WRONG}
        def branch(index,cost):
            acc=F()
            for observed in (0,1):
                prob=sum((v for k,v in matched.items() if k[index]==observed),F())/mass
                if not prob:continue
                args=dict(d=d,s3=s3,t=t)
                args[{1:'d',4:'s3',0:'t'}[index]]=observed
                acc+=prob*decide(s1,s2,remaining=remaining-1,**args)[0]
            return acc-cost
        if remaining:
            if audit and d is None:choices['AUDIT']=branch(1,cost_audit)
            if new and s3 is None:choices['NEW']=branch(4,cost_new)
            if oracle and t is None:choices['ORACLE']=branch(0,F(1))
        best=max(choices.values())
        return best,tuple(a for a,v in choices.items() if v==best)
    root=F()
    for s1,s2 in product((0,1),repeat=2):
        mass=sum((v for k,v in worlds.items() if k[2:4]==(s1,s2)),F())
        root+=mass*decide(s1,s2,remaining=limit)[0]
    return root,decide,worlds

def forward(**kwargs):
    root,policy,worlds=solve(**kwargs)
    totals={'SAFE':F(),'RIGHT':F(),'WRONG':F(),
            'AUDIT':F(),'NEW':F(),'ORACLE':F(),'value':F()}
    for (truth,dup,s1,s2,s3),prior in worlds.items():
        d=third=t=None;left=kwargs.get('limit',1);score=F()
        while True:
            action=policy(s1,s2,d,third,t,left)[1][0]
            if action=='SAFE':
                totals['SAFE']+=prior;score+=SAFE;break
            if action.startswith('CLAIM'):
                correct=int(action[-1])==truth
                totals['RIGHT' if correct else 'WRONG']+=prior
                score+=RIGHT if correct else WRONG
                break
            if action=='AUDIT':
                d=dup;score-=kwargs.get('cost_audit',F(1))
            elif action=='NEW':
                third=s3;score-=kwargs.get('cost_new',F(1))
            elif action=='ORACLE':
                t=truth;score-=1
            else:raise AssertionError(action)
            totals[action]+=prior;left-=1;assert left>=0
        totals['value']+=prior*score
    assert totals['value']==root
    assert totals['SAFE']+totals['RIGHT']+totals['WRONG']==1
    return totals

def naive_independence():
    return sum((v*((RIGHT if s1==t else WRONG) if s1==s2 else SAFE)
                for (t,d,s1,s2,s3),v in WORLDS.items()),F())

def regression():
    agree=sum((p for k,p in WORLDS.items() if k[2]==k[3]),F())
    agree_worlds={k:v for k,v in WORLDS.items() if k[2]==k[3]}
    dup=sum((v for k,v in agree_worlds.items() if k[1]==1),F())/agree
    accuracy=sum((v for k,v in agree_worlds.items() if k[0]==k[2]),F())/agree
    assert (agree,dup,accuracy)==(F(13,16),F(8,13),F(21,26))
    assert naive_independence()==F(11,4)
    # SOURCE10: misreading dependence changes ACTION, not just confidence,
    # only above the misclassification-loss threshold 132/5.
    loss_threshold=(F(21,26)*RIGHT-SAFE)/(1-F(21,26))
    assert loss_threshold==F(132,5)
    for loss in (F(20),F(28)):
        actual_agree=F(21,26)*RIGHT-(1-F(21,26))*loss
        assert (actual_agree<SAFE)==(loss>loss_threshold)
    # At loss=20 one still CLAIMs on agreement, despite an overstated
    # independent-source confidence. At loss=28 one instead safely exits.
    expected={(False,False):F(3),(True,False):F(51,16),
              (False,True):F(283,64),(True,True):F(283,64)}
    for (audit,new),v in expected.items():
        assert solve(audit=audit,new=new)[0]==v
        outcome=forward(audit=audit,new=new)
        print('audit',audit,'new',new,'score',v,'real_world',outcome)
    assert forward(audit=True,new=False)['AUDIT']==F(13,16)
    assert forward(audit=False,new=True)['NEW']==F(13,16)
    assert solve(limit=2)[0]==F(283,64)
    assert solve(oracle=True)[0]==9
    assert solve(independent_only=True)[0]==5
    assert [solve(cost_new=F(i))[0] for i in (1,2,3,4)]==[
        F(283,64),F(231,64),F(51,16),F(51,16)]
    print('SOURCE01-06 PASS: duplicated corroboration; source audit beaten by new report')

if __name__=='__main__':regression()
