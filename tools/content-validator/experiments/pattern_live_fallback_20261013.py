#!/usr/bin/env python3
"""HEDGE01-05: when does simultaneous active work strictly matter?

Five player actions. A may have an EARLY opening window (only turn1).
A has H14/L5, B H14/L0, independently equiprobable, public on OPEN.
Both need two WORK after OPEN. A live project expires after >= deadline
subsequent global actions, losing 4 unless completed or dropped (1 action).
Strong no-overlap comparator forbids OPEN_B while A live and vice versa,
without removing either project from the entire action set. Enumerate
adaptive DP and 7**5 unconditional action scripts (illegal -> SIDE).
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('OPEN_A','OPEN_B','WORK_A','WORK_B','DROP_A','DROP_B','SIDE')
VALUES=((14,5),(14,0))
NEW=('new',0,0,0)
DONE=('done',0,0,0)
INIT=(NEW,NEW)
TURNS=5

def legal(st,a,overlap=True,drop=True,second=True):
    if a=='SIDE':return True
    verb,name=a.split('_');i=0 if name=='A' else 1
    if i==1 and not second:return False
    state=st[i][0]
    if verb=='OPEN':
        return state=='new' and (overlap or not any(x[0]=='live' for x in st))
    if verb=='WORK':return state=='live'
    if verb=='DROP':return state=='live' and drop
    return False

def advance(st,a,value=None,deadline=3,liability=True):
    assert legal(st,a)
    objs=list(st);income=F(a=='SIDE');fresh=-1
    if a!='SIDE':
        verb,name=a.split('_');i=0 if name=='A' else 1
        status,age,work,v=objs[i]
        if verb=='OPEN':
            assert value in VALUES[i]
            objs[i]=('live',0,0,value);fresh=i
        elif verb=='WORK':
            work+=1
            if work==2:income+=v;objs[i]=DONE
            else:objs[i]=('live',age,work,v)
        elif verb=='DROP':objs[i]=DONE
    for i,obj in enumerate(objs):
        status,age,work,v=obj
        if status=='live' and i!=fresh:
            if age+1>=deadline:
                income-=4 if liability else 0
                objs[i]=DONE
            else:objs[i]=('live',age+1,work,v)
    return tuple(objs),income

def terminal(st,liability=True):
    return F((-4 if liability else 0)*sum(x[0]=='live' for x in st))

def dynamic(overlap=True,deadline=3,drop=True,second=True,
            liability=True,early_a=False):
    @lru_cache(None)
    def dp(t,st):
        if t==TURNS:return terminal(st,liability)
        choices=[]
        for a in ACT:
            if not legal(st,a,overlap,drop,second):continue
            if early_a and a=='OPEN_A' and t>0:continue
            z=VALUES[0 if a.endswith('A') else 1] if a.startswith('OPEN') else (None,)
            options=[advance(st,a,v,deadline,liability) for v in z]
            choices.append(sum(r+dp(t+1,ns) for ns,r in options)/len(options))
        return max(choices)
    return dp(0,INIT)

def fixed(overlap=True,deadline=3,drop=True,second=True,
          liability=True,early_a=False):
    scores=[]
    for p in product(ACT,repeat=TURNS):
        expected=F(0)
        for va,vb in product(*VALUES):
            st=INIT;income=F(0)
            for t,a in enumerate(p):
                use=a if legal(st,a,overlap,drop,second) and (
                    not early_a or a!='OPEN_A' or t==0) else 'SIDE'
                value=va if use=='OPEN_A' else vb if use=='OPEN_B' else None
                st,r=advance(st,use,value,deadline,liability);income+=r
            expected+=(income+terminal(st,liability))/4
        scores.append((expected,p))
    best=max(x for x,p in scores)
    return best,tuple(p for x,p in scores if x==best)

def hedge_witness(deadline=3):
    """Short adaptive script, uses TWO simultaneous LIVE tasks after low A."""
    outputs=[]
    for va,vb in product(*VALUES):
        if va==14:
            plan=('OPEN_A','WORK_A','WORK_A','SIDE','SIDE')
        elif vb==14:
            plan=('OPEN_A','OPEN_B','DROP_A','WORK_B','WORK_B')
        else:
            plan=('OPEN_A','OPEN_B','WORK_A','WORK_A','DROP_B')
        st=INIT;income=F(0);ever_double=False
        for a in plan:
            val=va if a=='OPEN_A' else vb if a=='OPEN_B' else None
            st,r=advance(st,a,val,deadline)
            income+=r
            ever_double|=sum(x[0]=='live' for x in st)==2
        outputs.append((va,vb,income+terminal(st),ever_double,plan))
    return sum(x[2] for x in outputs)/4,outputs

def main():
    base=dynamic(early_a=True)
    no_overlap=dynamic(overlap=False,early_a=True)
    no_early_window=dynamic(early_a=False)
    serial_no_window=dynamic(overlap=False,early_a=False)
    no_drop=dynamic(drop=False,early_a=True)
    no_second=dynamic(second=False,early_a=True)
    short_deadline=dynamic(deadline=2,early_a=True)
    short_deadline_serial=dynamic(deadline=2,overlap=False,early_a=True)
    best_fixed,_=fixed(early_a=True)
    witness,branches=hedge_witness()
    assert (base,no_overlap,no_early_window,serial_no_window,
            no_drop,no_second,short_deadline,short_deadline_serial,best_fixed,witness)==(
            F(51,4),F(47,4),F(51,4),F(51,4),
            F(23,2),F(23,2),F(47,4),F(47,4),F(23,2),F(51,4))
    assert sum(x[3] for x in branches)==2
    assert [x[2] for x in branches]==[16,16,14,5]
    print('HEDGE early-A-window overlap',base,'strict-no-overlap',no_overlap,
          'best fixed',best_fixed)
    print('remove early window: overlap',no_early_window,
          'serial can match',serial_no_window)
    print('remove DROP',no_drop,'remove B',no_second,
          'short deadline overlap/serial',short_deadline,short_deadline_serial)
    print('transparent actual policy',branches)
    print('PASS real concurrent-live advantage needs early opportunity and viable fallback')

if __name__=='__main__':main()
