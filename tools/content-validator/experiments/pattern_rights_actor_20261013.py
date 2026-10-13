#!/usr/bin/env python3
"""Two abstract decision families: rights switching and a self-interested partner.

Standard library Fraction and backwards induction. Neither is a human playtest,
Citizen Sleeper 2 rule transcription, independent agent, or Unity verification.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product


def rights(stay=F(4, 5), days=4):
    """One persistent credential, one irreversible switch, four observed jobs.

    Market job A/B starts uniformly; each later job repeats the previous type
    with probability 'stay'. Working with the matching credential pays 5.
    SWITCH consumes today's action, changes the credential permanently.
    """
    other = lambda x: "B" if x == "A" else "A"

    @lru_cache(None)
    def optimum(day, credential, switched, job):
        if day == days:
            return F(0)
        def continuation(next_credential, next_switched):
            if day == days-1:
                return F(0)
            return (stay * optimum(day+1, next_credential, next_switched, job)
                    + (1-stay) * optimum(day+1, next_credential, next_switched, other(job)))
        values = [("SERVE", F(5 if credential == job else 0)
                   + continuation(credential, switched))]
        if not switched:
            values.append(("SWITCH", continuation(job, True)))
        return max(values, key=lambda z:z[1])[1]

    def choice(day, credential, switched, job):
        if day == days:
            return None
        def future(next_credential, next_switched):
            if day == days-1:
                return F(0)
            return (stay * optimum(day+1, next_credential, next_switched, job)
                    + (1-stay) * optimum(day+1, next_credential, next_switched, other(job)))
        options=[("SERVE", F(5 if credential == job else 0)+future(credential, switched))]
        if not switched:
            options.append(("SWITCH",future(job,True)))
        return max(options, key=lambda z:z[1])

    def root_of(rule):
        @lru_cache(None)
        def value(day, credential, switched, job):
            if day == days:
                return F(0)
            change = not switched and rule(day, credential, job)
            reward = 0 if change else 5*(credential == job)
            new_credential = job if change else credential
            if day == days-1:
                return F(reward)
            return (F(reward)
                    + stay*value(day+1,new_credential, switched or change,job)
                    + (1-stay)*value(day+1,new_credential,switched or change,other(job)))
        return (value(0,"A",False,"A")+value(0,"A",False,"B"))/2

    simple = {
        "never_switch":root_of(lambda d,c,j:False),
        "first_mismatch":root_of(lambda d,c,j:c!=j),
        "mismatch_before_last":root_of(lambda d,c,j:c!=j and d < days-1),
        "day_one_mismatch_only":root_of(lambda d,c,j:c!=j and d == 0),
        "first_two_days_mismatch":root_of(lambda d,c,j:c!=j and d <= 1),
    }
    root=(optimum(0,"A",False,"A")+optimum(0,"A",False,"B"))/2
    return root,choice,simple


def partner_model(out_reward=8, event_day=1, tie="neutral"):
    """Public perfect-information sequential game, 3 cycles.

    Player moves A or B, then partner sees the move and autonomously chooses
    A/B/HOLD/OUT. Partner has one work effort for all three cycles. Partner's
    terminal utility: B>=2 adds 4; A>=4 adds 5; completing their own external
    OUT adds 'out_reward'. Player utility: A>=4 adds 6, B>=2 adds 1.
    OUT becomes available in event_day with probability 1/2; both observe.
    Both plan continuation optimally (not an exogenous partner script).
    """
    assert event_day in (1,2)
    assert tie in ("neutral","player_favor","player_disfavor")
    coef={"neutral":0,"player_favor":1,"player_disfavor":-1}[tie]

    @lru_cache(None)
    def continuation(day,a,b,energy,offer,outsourced):
        if day==3:
            return (F(6*(a>=4)+(b>=2)),F(4*(b>=2)+5*(a>=4)+out_reward*outsourced))
        if day==event_day and offer == -1:
            no=continuation(day,a,b,energy,0,outsourced)
            yes=continuation(day,a,b,energy,1,outsourced)
            return tuple((x+y)/2 for x,y in zip(no,yes))
        return player(day,a,b,energy,offer,outsourced)[1]

    def responder(day,a,b,energy,offer,outsourced):
        options=[("HOLD",a,b,energy,outsourced)]
        if energy:
            options.extend([("A",a+1,b,energy-1,outsourced),
                            ("B",a,b+1,energy-1,outsourced)])
            if offer==1 and not outsourced:
                options.append(("OUT",a,b,energy-1,True))
        scored=[]
        for action,na,nb,ne,done in options:
            scored.append((action,continuation(day+1,na,nb,ne,offer,done),
                           (na,nb,ne,done)))
        return max(scored, key=lambda s:(s[1][1],coef*s[1][0],
                                           {"HOLD":3,"B":2,"OUT":1,"A":0}[s[0]]))

    def player(day,a,b,energy,offer,outsourced):
        candidates=[]
        for action,na,nb in (("A",a+1,b),("B",a,b+1)):
            response=responder(day,na,nb,energy,offer,outsourced)
            candidates.append((action,response[1],response[0],response[2]))
        return max(candidates,key=lambda s:(s[1][0], 1 if s[0]=="A" else 0))

    initial=continuation(0,0,0,1,-1,False)
    paths=[]
    for signal in (0,1):
        a=b=0
        energy=1
        outsourced=False
        actions=[]
        for day in range(3):
            offer = -1 if day<event_day else signal
            selected=player(day,a,b,energy,offer,outsourced)
            a,b,energy,outsourced=selected[3]
            actions.append((selected[0],selected[2],a,b,energy,outsourced))
        paths.append(actions)
    return initial,paths,player,responder


def fixed_announced(script, out_reward=8, tie="neutral"):
    """Different protocol: publicly precommit to a full A/B script in advance.

    The other actor best-responds by backward induction to this exact plan,
    not to the dynamic-equilibrium continuation policy. Compare carefully.
    """
    assert len(script)==3
    coef={"neutral":0,"player_favor":1,"player_disfavor":-1}[tie]

    @lru_cache(None)
    def future(day,a,b,energy,offer,outsourced):
        if day==3:
            return F(6*(a>=4)+(b>=2)),F(4*(b>=2)+5*(a>=4)+out_reward*outsourced)
        if day==1 and offer==-1:
            low=future(day,a,b,energy,0,outsourced)
            high=future(day,a,b,energy,1,outsourced)
            return tuple((x+y)/2 for x,y in zip(low,high))
        if script[day]=="A":
            a+=1
        else:
            b+=1
        actions=[("HOLD",a,b,energy,outsourced)]
        if energy:
            actions.extend([("A",a+1,b,energy-1,outsourced),
                            ("B",a,b+1,energy-1,outsourced)])
            if offer==1 and not outsourced:
                actions.append(("OUT",a,b,energy-1,True))
        scored=[(letter,future(day+1,x,y,e,offer,done))
                for letter,x,y,e,done in actions]
        return max(scored,key=lambda s:(s[1][1],coef*s[1][0],
                               {"HOLD":3,"B":2,"OUT":1,"A":0}[s[0]]))[1]
    return future(0,0,0,1,-1,False)


def run_checks():
    expected=[
        (F(1,5),F(10)),
        (F(1,2),F(10)),
        (F(3,5),F(549,50)),
        (F(4,5),F(683,50)),
        (F(9,10),F(771,50)),
    ]
    for persistence,target in expected:
        optimum,policy,controls=rights(persistence)
        assert optimum==target
        assert max(controls.values())==optimum
        assert policy(3,"A",False,"B")[0]=="SERVE"
        print("RIGHTS stay",persistence,"optimal",optimum,
              "best simple",max(controls.values()),
              "winner", [k for k,v in controls.items() if v==optimum])

    baseline,paths,player,responder=partner_model()
    assert baseline == (F(7,2),F(17,2))
    assert [p[0] for p in paths[0]] == ["A","A","A"]
    assert [p[0] for p in paths[1]] == ["A","B","B"]
    assert [p[1] for p in paths[0]] == ["HOLD","HOLD","A"]
    assert [p[1] for p in paths[1]] == ["HOLD","HOLD","OUT"]
    print("PARTNER conditional public signal",baseline,"paths",paths)
    no_offer=partner_model(out_reward=0)
    late_offer=partner_model(event_day=2)
    assert no_offer[0] == (F(6),F(5))
    assert late_offer[0] == (F(3),F(13,2))
    for t in ("player_favor","player_disfavor"):
        assert partner_model(tie=t)[0]==baseline
        assert [p[0] for p in partner_model(tie=t)[1][1]]==["A","B","B"]
    announced=[(script,fixed_announced(script)[0]) for script in product("AB",repeat=3)]
    best_script=max(announced,key=lambda x:x[1])
    assert best_script == (("A","A","A"),F(3))
    print("PARTNER free outside opportunity",no_offer[0],
          "late signal",late_offer[0],"best publicly committed A/B script",
          best_script)
    # At day2 after common day1 A/HOLD, this event strictly reverses preference.
    _,_,best,responder=partner_model()
    signal0_A=responder(1,2,0,1,0,False)[1][0]
    signal0_B=responder(1,1,1,1,0,False)[1][0]
    signal1_A=responder(1,2,0,1,1,False)[1][0]
    signal1_B=responder(1,1,1,1,1,False)[1][0]
    assert (signal0_A-signal0_B,signal1_A-signal1_B)==(F(5),F(-1))
    print("PARTNER reachable turn-two preference differences A minus B",
          (signal0_A-signal0_B,signal1_A-signal1_B))
    print("PASS rights switching and strategic partner exact counterfactuals")


if __name__=="__main__":
    run_checks()
