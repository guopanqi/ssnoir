#!/usr/bin/env python3
"""Rights-changing follow-up goal: exact finite counterfactual, not a playtest."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product, permutations

DAYS = (4, 4, 5, 6)


def model(close_long_goal=True):
    @lru_cache(None)
    def expectation(day, b, ticket, entitled):
        if day == len(DAYS):
            return F(9 if entitled and b >= 4 else 0)
        return (max(v for _, v in action_values(day,b,ticket,entitled,True)) +
                max(v for _, v in action_values(day,b,ticket,entitled,False))) / 2

    def legal(day, b, ticket, entitled, urgent):
        if entitled:
            yield ('B', min(4,b+2), 0, True, 0)
        else:
            yield ('IDLE', b, 0, False, 0)
        if urgent:
            yield ('U', b, 1, entitled, DAYS[day])
        if ticket:
            yield ('R', b, 0, not close_long_goal and entitled, 7)

    def action_values(day,b,ticket,entitled,urgent):
        return [(name,F(reward)+expectation(day+1,nb,nt,ent))
                for name,nb,nt,ent,reward in legal(day,b,ticket,entitled,urgent)]

    def evaluate(rule):
        @lru_cache(None)
        def walk(day,b,ticket,entitled):
            if day == len(DAYS):
                return F(9 if entitled and b >= 4 else 0)
            outcomes = []
            for urgent in (False,True):
                actions = tuple(legal(day,b,ticket,entitled,urgent))
                decision = rule(day,b,ticket,entitled,urgent,{a[0] for a in actions})
                chosen = next((a for a in actions if a[0]==decision),None)
                assert chosen is not None,(day,b,ticket,entitled,urgent,decision)
                _,nb,nt,ent,reward=chosen
                outcomes.append(F(reward)+walk(day+1,nb,nt,ent))
            return sum(outcomes)/2
        return walk(0,0,0,True)
    return expectation,action_values,legal,evaluate


def best_day_rule(m):
    # IDLE is available exactly when B is not: six permutations suffice.
    priority = tuple(permutations(('B','U','R')))
    best = (F(-1),None)
    for plan in product(priority,repeat=len(DAYS)):
        def rule(day,b,ticket,entitled,urgent,available):
            return next((action for action in plan[day] if action in available),'IDLE')
        score = m[3](rule)
        if score > best[0]:
            best = (score,plan)
    return best


def traces(m):
    observed = {}
    for offers in product((False,True),repeat=len(DAYS)):
        b=ticket=0
        entitled=True
        for day,urgent in enumerate(offers):
            possible=m[1](day,b,ticket,entitled,urgent)
            action,_=max(possible,key=lambda x:x[1])
            key=(day,b,ticket,entitled,urgent)
            observed.setdefault(key,set()).add(action)
            _,b,ticket,entitled,_=next(
                a for a in m[2](day,b,ticket,entitled,urgent) if a[0]==action
            )
    return observed


def main():
    rights=model(True)
    additive=model(False)
    results=[]
    for name,m in [('rights-closed',rights),('no-rights-closure',additive)]:
        optimal=m[0](0,0,0,True)
        fixed=best_day_rule(m)
        results.append((optimal,fixed[0]))
        print(name,'optimal',optimal,'best daily priority',fixed[0],
              'gap',optimal-fixed[0],'policy',fixed[1])
    assert results==[(F(69,4),F(269,16)),(F(159,8),F(39,2))]
    state=(2,2,1,True,False)
    assert state in traces(rights)
    a=max(rights[1](*state),key=lambda x:x[1])[0]
    b=max(additive[1](*state),key=lambda x:x[1])[0]
    assert a=='B' and b=='R'
    print('reachable same state',state,'closed=>',a,'no closure=>',b)
    print('PASS exact policy and rights counterfactuals')


if __name__=='__main__':
    main()
