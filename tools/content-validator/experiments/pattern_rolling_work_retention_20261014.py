#!/usr/bin/env python3
"""RETENTION01-04: volatile progress, interrupting opportunities, and voluntary LOCK.

On each day U arrives independently P=1/2 and is publicly observed.
WORK_A gives +1 toward A3 (completion pays12 and remains banked forever).
A one-day diversion from unfinished A erases one A progress unless locked.
URGENT gets4 if U is present; SIDE gets1. LOCK costs one entire player day,
requires 1 or2 unfinished A progress, and prevents future decay but gives
no work or reward. An exposed result is not a player preference judgment.
"""
from fractions import Fraction as F
from functools import lru_cache
from collections import defaultdict

ACTIONS=('WORK_A','URGENT','SIDE','LOCK')

def solve(horizon=8,allow_lock=True,urgent_chance=F(1,2),
          reward_a=12,reward_urgent=4,reward_side=1):
    @lru_cache(None)
    def expected(day,p,locked,completed):
        if day==horizon:return F(0)
        return (urgent_chance*evaluate(day,p,locked,completed,True)[0]
                +(1-urgent_chance)*evaluate(day,p,locked,completed,False)[0])
    @lru_cache(None)
    def evaluate(day,p,locked,completed,urgent):
        options={}
        if not completed:
            done=p+1>=3
            options['WORK_A']=F(reward_a if done else 0)+expected(
                day+1,0 if done else p+1,locked,completed or done)
        decay_p=p if locked or completed else max(0,p-1)
        options['SIDE']=F(reward_side)+expected(day+1,decay_p,locked,completed)
        if urgent:
            options['URGENT']=F(reward_urgent)+expected(day+1,decay_p,locked,completed)
        if allow_lock and 0<p<3 and not locked and not completed:
            options['LOCK']=expected(day+1,p,True,completed)
        best=max(options.values())
        # Tie-break against lock, so logged LOCK is actually selected as optimum.
        chosen=next(x for x in ACTIONS if x in options and options[x]==best)
        return best,chosen,options
    return expected(0,0,False,False),evaluate

def optimal_policy_reach(horizon=8):
    root,evaluate=solve(horizon)
    frontier={(0,0,False,False):F(1)};locks=[]
    for day in range(horizon):
        nxt=defaultdict(F)
        for (t,p,locked,done),mass in frontier.items():
            for urgent in (False,True):
                _,action,_=evaluate(t,p,locked,done,urgent)
                if action=='LOCK':
                    locks.append((day,p,urgent,mass/2))
                if action=='WORK_A':
                    complete=p+1>=3
                    target=(t+1,0 if complete else p+1,locked,done or complete)
                elif action=='LOCK':
                    target=(t+1,p,True,done)
                else:
                    target=(t+1,p if locked or done else max(0,p-1),locked,done)
                nxt[target]+=mass/2
        frontier=nxt
    return root,locks

def main():
    expected={5:F(145,8),6:F(333,16),7:F(375,16),8:F(1669,64)}
    no_lock={5:F(145,8),6:F(333,16),7:F(375,16),8:F(1667,64)}
    for horizon in (5,6,7,8):
        a,_=solve(horizon,True);b,_=solve(horizon,False)
        assert a==expected[horizon],(horizon,a,expected[horizon])
        assert b==no_lock[horizon],(horizon,b,no_lock[horizon])
        print('horizon',horizon,'with-lock',a,'without-lock',b,'delta',a-b)
    root,locks=optimal_policy_reach()
    assert root==F(1669,64)
    assert locks==[(1,1,False,F(1,4))],locks
    assert root-solve(8,False)[0]==F(1,32)
    print('normal initial-state selected LOCK histories:',locks)
    print('PASS volatile work, rare stabilization, exact dynamic control')

if __name__=='__main__':main()
