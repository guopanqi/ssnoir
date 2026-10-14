#!/usr/bin/env python3
"""Self-binding and strategic sabotage: finite exact abstract controls.
Author-only mathematical evidence. Reuses unchanged CROSS/COA prior protocols.
"""
from fractions import Fraction as F
from itertools import product

import pattern_mutable_goals_crosscontract_20261014 as cross
import pattern_partner_outcome_commitment_20261014 as coop

MOVES = ('A', 'B', 'SIDE')
TYPES = {'TARGET_A': (5, 3), 'TARGET_B': (3, 5)}

def pre_state(early):
    return (early.count('A'), early.count('B'), early.count('SIDE')), 'SHIELD' in early

def foe(state, typ):
    a, b, _ = state
    av, bv = TYPES[typ]
    options = {'STRIKE_A':F(av if a else 0),
               'STRIKE_B':F(bv if b else 0), 'EXIT':F(1)}
    op = max(options, key=lambda k:(options[k], -list(options).index(k)))
    assert options[op] > max(v for k,v in options.items() if k != op)
    return op, options[op]

def apply(state, attack, mode='erase', shield=False):
    a, b, side = state
    if attack=='EXIT' or shield:return state
    if mode=='erase':
        return (0,b,side) if attack=='STRIKE_A' else (a,0,side)
    if mode=='chip':
        return (max(0,a-1),b,side) if attack=='STRIKE_A' else (a,max(0,b-1),side)
    raise ValueError(mode)

def score(state, later):
    a,b,side=state
    a+=later.count('A');b+=later.count('B');side+=later.count('SIDE')
    vec=(int(a>=3),int(b>=3),side)
    return F(10*vec[0]+8*vec[1]+side),vec

def branches(early, mode='erase', allow_shield=False):
    if 'SHIELD' in early and not allow_shield:raise ValueError(early)
    state, shield=pre_state(early)
    result={}
    for typ in TYPES:
        strike,opp_pay=foe(state,typ)
        post=apply(state,strike,mode,shield)
        best=max(((*score(post,late),late) for late in product(MOVES,repeat=2)),
                 key=lambda x:x[0])
        result[typ]=(strike,opp_pay,post,best[0],best[1],best[2])
    return result

def solve(mode='erase',allow_shield=False,fixed=False):
    beginnings=MOVES+(('SHIELD',) if allow_shield else ())
    rows=[]
    for pre in product(beginnings,repeat=2):
        b=branches(pre,mode,allow_shield)
        if fixed:
            scripts=[(sum((score(z[2],late)[0] for z in b.values()),F())/2,late)
                     for late in product(MOVES,repeat=2)]
            v,late=max(scripts,key=lambda x:x[0])
            rows.append((v,pre,late,b))
        else:
            v=sum((z[3] for z in b.values()),F())/2
            rows.append((v,pre,b))
    return max(rows,key=lambda x:x[0]),rows

def can_guarantee_a(mode='erase',allow_shield=False):
    for _,pre,b in solve(mode,allow_shield)[1]:
        if all(any(score(z[2],late)[1][0]
                   for late in product(MOVES,repeat=2)) for z in b.values()):
            return pre
    return None

def private_promise():
    flexible,_=cross.full()
    goals={g:cross.hard_goal(g) for g in ('A','B','BOTH','FULL_C')}
    assert flexible[0]==F(31,2)
    assert {k:v[0] for k,v in goals.items()} == {
        'A':F(29,2),'B':F(27,2),'BOTH':F(11),'FULL_C':F(31,2)}
    assert goals['A'][1]==('WORK_A','WORK_A')
    assert goals['B'][1]==('WORK_B','WORK_B')
    assert all(v[0]<=flexible[0] for v in goals.values())
    for fee in (F(0),F(1,4),F(1),F(3)):
        assert max([flexible[0]]+[v[0]-fee for v in goals.values()])==flexible[0]
    # Distinct previous public contractual protocol: changes a real
    # second actor's decision, so cannot be conflated with a private pledge.
    assert coop.unbound()[1][0]==3
    assert coop.best_contract()[0][1][0]==15
    assert coop.partner_second_veto_menu()[0][1][0]==F(21,2)
    assert coop.partner_second_veto_menu(bilateral=True)[0][1][0]==15
    return flexible,goals

def adversarial():
    destroyed,early_options=solve()
    chip,_=solve('chip')
    fixed,_=solve('erase',fixed=True)
    shield,_=solve('erase',allow_shield=True)
    assert (destroyed[0],destroyed[1])==(F(9),('A','B'))
    assert (fixed[0],fixed[1],fixed[2])==(F(5),('A','B'),('A','A'))
    assert (chip[0],chip[1])==(F(10),('A','A'))
    assert (shield[0],shield[1])==(F(10),('A','SHIELD'))
    ta=destroyed[2]['TARGET_A'];tb=destroyed[2]['TARGET_B']
    assert (ta[0],ta[5],ta[4])==('STRIKE_A',('B','B'),(0,1,0))
    assert (tb[0],tb[5],tb[4])==('STRIKE_B',('A','A'),(1,0,0))
    assert can_guarantee_a() is None
    assert can_guarantee_a(allow_shield=True) is not None
    assert can_guarantee_a('chip') is not None
    # Knowing attacker type before first 2 actions adds nothing:
    # AB is best in BOTH types, but late attack feedback changes the route.
    for typ in TYPES:
        type_max=max(branches(pre)[typ][3]
                     for pre in product(MOVES,repeat=2))
        assert type_max==destroyed[2][typ][3]
    # Independently reconstruct conditional score and two terminal vectors.
    assert (ta[3]+tb[3])/2==destroyed[0]
    assert [(ta[4][0],tb[4][0]),(ta[4][1],tb[4][1])]==[(0,1),(1,0)]
    print("SABOTAGE", {"erase":str(destroyed[0]),"prep":destroyed[1],
        "target_A":{"strike":ta[0],"next":ta[5],"value":str(ta[3])},
        "target_B":{"strike":tb[0],"next":tb[5],"value":str(tb[3])},
        "fixed":str(fixed[0]),"chip":str(chip[0]),
        "one_action_shield":str(shield[0]),"early_type_information_value":"0"})
    assert len(early_options)==9

def main():
    flex,goals=private_promise()
    print("PLEDGE", {"flex":str(flex[0]),"binding":{k:str(v[0]) for k,v in goals.items()},
        "no_extra_benefit_of_pruning":"all goals <= flex"})
    adversarial()
    print("PASS: pledge and strategic actor full menus, late feedback, "
          "fixed scripts, goal feasibility and ordinary shield")

if __name__=="__main__":
    main()
