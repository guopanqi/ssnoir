#!/usr/bin/env python3
"""OPEN01-06: actively opening work reveals value and creates a deadline.

Five action slots, two independent projects A/B. OPEN reveals HIGH14/LOW1
at start of a live obligation. Two WORK actions complete it. The live
obligation expires three global actions after OPEN and loses 4, unless
completed or explicitly DROPPED using one whole action. SIDE pays 1.
Compare all adaptive policies to 7**5 fixed plans with a generous legal
fallback of SIDE, then ablate second project, liability, DROP and
report timing. Everything is a frozen abstract toy, not player evidence.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('OPEN_A','OPEN_B','WORK_A','WORK_B','DROP_A','DROP_B','SIDE')
NEW=('new',0,0,0)
DONE=('done',0,0,0)
INIT=(NEW,NEW)
HORIZON=5
VALUES=(14,1)

def legal(st,act,second=True,drop=True):
    if act=='SIDE':return True
    verb,which=act.split('_')
    i=0 if which=='A' else 1
    if i==1 and not second:return False
    status=st[i][0]
    return (verb=='OPEN' and status=='new') or (
        verb=='WORK' and status=='live') or (
        verb=='DROP' and status=='live' and drop)

def step(st,act,v=None,liability=True,report='open'):
    assert legal(st,act)
    objs=list(st);delta=F(act=='SIDE');new_index=-1
    if act!='SIDE':
        verb,which=act.split('_')
        i=0 if which=='A' else 1
        status,age,progress,prior=objs[i]
        if verb=='OPEN':
            assert v in VALUES or (report=='work' and v is None)
            objs[i]=('live',0,0,v if report=='open' else -1)
            new_index=i
        elif verb=='WORK':
            assert prior in VALUES
            progress+=1
            if progress==2:
                delta+=prior;objs[i]=DONE
            else:objs[i]=('live',age,progress,prior)
        elif verb=='DROP':objs[i]=DONE
    for i,obj in enumerate(objs):
        status,age,progress,prior=obj
        if status=='live' and i!=new_index:
            if age+1>=3:
                if liability:delta-=4
                objs[i]=DONE
            else:objs[i]=('live',age+1,progress,prior)
    return tuple(objs),delta

def terminal(st,liability=True):
    return F((-4 if liability else 0)*sum(x[0]=='live' for x in st))

def options(st,second=True,drop=True):
    return (x for x in ACT if legal(st,x,second,drop))

def evaluate(second=True,liability=True,drop=True,report='open',oracle=False):
    assert not (report=='work' and oracle)
    @lru_cache(None)
    def dp(t,st,values=None):
        if t==HORIZON:return terminal(st,liability)
        vals=[]
        for action in options(st,second,drop):
            if action.startswith('OPEN'):
                choices=(values[0 if action.endswith('A') else 1],) if oracle else VALUES
                if report=='work':choices=(None,)
                ps=[step(st,action,v,liability,report) for v in choices]
                vals.append(sum(r+dp(t+1,ns,values) for ns,r in ps)/len(ps))
            elif action.startswith('WORK') and st[0 if action.endswith('A') else 1][3]==-1:
                i=0 if action.endswith('A') else 1
                ps=[]
                for v in VALUES:
                    x=list(st);status,age,work,_=x[i]
                    x[i]=('live',age,work,v)
                    ns,r=step(tuple(x),action,liability=liability)
                    ps.append(r+dp(t+1,ns,values))
                vals.append(sum(ps)/2)
            else:
                ns,r=step(st,action,liability=liability)
                vals.append(r+dp(t+1,ns,values))
        return max(vals)
    if oracle:
        return sum(dp(0,INIT,v) for v in product(VALUES,repeat=2))/4
    return dp(0,INIT,None)

def fixed(second=True,liability=True,drop=True):
    # Strong nonreactive comparator: illegal prewritten instructions become
    # SIDE rather than wasting the turn or ending the run.
    vals=[]
    for plan in product(ACT,repeat=HORIZON):
        total=F(0)
        for va,vb in product(VALUES,repeat=2):
            st=INIT;res=F(0)
            for a in plan:
                use=a if legal(st,a,second,drop) else 'SIDE'
                v=va if use=='OPEN_A' else vb if use=='OPEN_B' else None
                st,r=step(st,use,v,liability);res+=r
            total+=(res+terminal(st,liability))/4
        vals.append((total,plan))
    maxval=max(v for v,_ in vals)
    return maxval,tuple(p for v,p in vals if v==maxval)

def strict_nondefault_obligations():
    """Once opened, a project MUST complete: dropping or timing out is invalid.

    This differs from paying a finite -4 penalty and is an explicit
    non-compensatory obligation rather than an inflated reward.
    """
    @lru_cache(None)
    def dp(t,st):
        if t==HORIZON:
            return F(0) if all(x[0]!='live' for x in st) else None
        vals=[]
        for action in ACT:
            if action.startswith('DROP') or not legal(st,action):continue
            samples=[step(st,action,v) for v in VALUES] if action.startswith('OPEN') else [
                step(st,action)]
            next_values=[]
            for ns,r in samples:
                expired=False
                for i,before in enumerate(st):
                    if before[0]=='live' and ns[i][0]=='done':
                        completing=(action=='WORK_'+('A' if i==0 else 'B')
                                    and before[2]==1)
                        if not completing:expired=True
                future=None if expired else dp(t+1,ns)
                if future is None:break
                next_values.append(r+future)
            else:vals.append(sum(next_values)/len(next_values))
        return max(vals) if vals else None
    return dp(0,INIT)

def public_short_policy():
    # H A: finish and spend the spare hands on SIDE.
    # L A: spend one action dropping it, open B and finish B regardless
    # of its value. B LOW=1 is tied with DROP_B then SIDE.
    res=[]
    for va,vb in product(VALUES,repeat=2):
        plan=('OPEN_A','WORK_A','WORK_A','SIDE','SIDE') if va==14 else (
              'OPEN_A','DROP_A','OPEN_B','WORK_B','WORK_B')
        st=INIT;reward=F(0)
        for a in plan:
            v=va if a=='OPEN_A' else vb if a=='OPEN_B' else None
            st,delta=step(st,a,v)
            reward+=delta
        res.append((va,vb,reward+terminal(st),plan))
    return sum(x[2] for x in res)/4,tuple(res)

def main():
    base=evaluate()
    no_b=evaluate(second=False)
    no_liability=evaluate(liability=False)
    no_drop=evaluate(drop=False)
    late=evaluate(report='work')
    oracle=evaluate(oracle=True)
    baseline,examples=fixed()
    short,branches=public_short_policy()
    hard=strict_nondefault_obligations()
    assert (base,no_b,no_liability,no_drop,late,oracle,baseline,short)==(
        F(47,4),F(19,2),F(25,2),F(41,4),F(39,4),F(53,4),
        F(19,2),F(47,4))
    assert hard==F(19,2),hard
    assert len(examples)>0 and branches[2][2]==14 and branches[3][2]==1
    print('OPEN adaptive',base,'fixed',baseline,'simple conditional',short)
    print('strict nondefault liability',hard)
    print('controls single-project',no_b,'no-liability',no_liability,
          'no-drop',no_drop,'late-info',late,'oracle-advance-info',oracle)
    print('short-policy public outcomes',branches)
    print('PASS: finite exact conditional planning + controls; no new human evidence')

if __name__=='__main__':main()
