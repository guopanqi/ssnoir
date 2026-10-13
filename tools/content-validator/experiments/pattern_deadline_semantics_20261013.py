#!/usr/bin/env python3
"""CLOCK01-04: frozen HEDGE goals, different definitions of deadline pressure.

5 slots, A can OPEN only on first slot in early mode; B anytime.
H/L values A=14/5, B=14/0; OPEN takes a slot, WORK twice, DROP takes
a slot. Expiration loses 4. Compare overlap and strict serial policies
for four qualitatively different clocks. Enumerate every fixed 7**5
instruction plan, with illegal instructions generously converted to SIDE.

No player-experience or original HEDGE engine claim.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

ACT=('OPEN_A','OPEN_B','WORK_A','WORK_B','DROP_A','DROP_B','SIDE')
VALUES=((14,5),(14,0))
NEW=('new',0,0,0)
DONE=('done',0,0,0)
INIT=(NEW,NEW)
CLOCKS=('global','double_if_overlap','neglect_only','contested_only')

def legal(st,act,t,overlap,early):
    if act=='SIDE':return True
    verb,letter=act.split('_')
    i=0 if letter=='A' else 1
    if verb=='OPEN':
        return (st[i][0]=='new' and (i!=0 or not early or t==0)
                and (overlap or st[1-i][0]!='live'))
    if verb=='WORK':return st[i][0]=='live'
    if verb=='DROP':return st[i][0]=='live'
    return False

def step(st,act,value,clock):
    objs=list(st)
    reward=F(act=='SIDE')
    fresh=-1
    if act!='SIDE':
        verb,letter=act.split('_')
        i=0 if letter=='A' else 1
        status,age,progress,oldvalue=objs[i]
        if verb=='OPEN':
            assert value in VALUES[i]
            fresh=i
            objs[i]=('live',0,0,value)
        elif verb=='DROP':objs[i]=DONE
        else:
            if progress+1==2:objs[i]=DONE;reward+=oldvalue
            else:objs[i]=('live',age,progress+1,oldvalue)
    both_live=all(o[0]=='live' for o in objs)
    for i,(status,age,progress,oldvalue) in enumerate(objs):
        if status!='live' or i==fresh:continue
        if clock=='global':step_clock=1
        elif clock=='double_if_overlap':step_clock=2 if both_live else 1
        elif clock=='neglect_only':
            step_clock=0 if act=='WORK_'+('A' if i==0 else 'B') else 1
        elif clock=='contested_only':step_clock=int(both_live)
        else:raise AssertionError(clock)
        age+=step_clock
        if age>=3:objs[i]=DONE;reward-=4
        else:objs[i]=('live',age,progress,oldvalue)
    return tuple(objs),reward

def solve(clock='global',overlap=True,early=True):
    @lru_cache(None)
    def dp(t,st):
        if t==5:return -4*sum(o[0]=='live' for o in st)
        choices=[]
        for act in ACT:
            if not legal(st,act,t,overlap,early):continue
            vs=VALUES[0 if act.endswith('A') else 1] if act.startswith('OPEN') else (None,)
            children=[step(st,act,v,clock) for v in vs]
            choices.append(sum(r+dp(t+1,ns) for ns,r in children)/len(children))
        return max(choices)
    return dp(0,INIT)

def fixed(clock='global',overlap=True,early=True):
    best=F(-10**9)
    plan0=None
    count=0
    for plan in product(ACT,repeat=5):
        total_expected=F(0)
        for va,vb in product(*VALUES):
            st=INIT
            score=F(0)
            for t,act in enumerate(plan):
                chosen=act if legal(st,act,t,overlap,early) else 'SIDE'
                value=va if chosen=='OPEN_A' else vb if chosen=='OPEN_B' else None
                st,earned=step(st,chosen,value,clock)
                score+=earned
            score-=4*sum(o[0]=='live' for o in st)
            total_expected+=score/4
        count+=1
        if total_expected>best:
            best=total_expected
            plan0=plan
    assert count==7**5
    return best,plan0

def main():
    expected={
        'global':(F(51,4),F(47,4)),
        'double_if_overlap':(F(47,4),F(47,4)),
        'neglect_only':(F(51,4),F(47,4)),
        'contested_only':(F(51,4),F(47,4)),
    }
    for clock in CLOCKS:
        actual=(solve(clock,True,True),solve(clock,False,True))
        assert actual==expected[clock],(clock,actual)
        assert solve(clock,True,False)==solve(clock,False,False)==F(51,4)
        baseline=fixed(clock)
        assert baseline[0]==F(23,2),(clock,baseline)
        print(clock,'early A overlap/serial',actual,
              'no opening restriction',F(51,4),
              'strong fixed',baseline)
    print('PASS all clock-semantics DP and fixed-script contrasts')

if __name__=='__main__':main()
