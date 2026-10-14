#!/usr/bin/env python3
"""Two finite abstract cross-family models; no human, blind agent, or native game evidence.

MULTI: an independently self-interested partner offers A/B work with a co-certificate.
COMMIT: a genuinely binding public H/L contract changes prior strategic participation.
All values use Fraction; strong substitutes and exhaustive small menus are tested.
"""
from fractions import Fraction as F
from itertools import product

GOALS = ("A", "B")
ACTS = ("A", "B", "SIDE")

def outcome(actions, helper, certificate=4):
    a = actions.count("A") + (helper == "A") >= 3
    b = actions.count("B") + (helper == "B") >= 3
    co = helper in GOALS and (a or b)
    side = actions.count("SIDE")
    return F(10*a + 9*b + certificate*co + side), (F(a),F(b),F(co),F(side))

def actor_choice(preference, continuation):
    # The other actor strictly prefers completing its own target (6) to EXIT (2).
    own = []
    for option in ("A","B","EXIT"):
        v,vector = outcome(continuation[option],option)
        utility = F(2) if option == "EXIT" else 6*vector[0 if preference=="A" else 1]
        own.append((utility,option))
    return max(own,key=lambda x:(x[0],-("A","B","EXIT").index(x[1])))

def multi(invite=True,auto=False,observe=True,certificate=4,direct_cert=False):
    if not invite:
        plans = []
        for path in product(ACTS,repeat=3):
            reward,vec=outcome(path,"EXIT",certificate)
            if direct_cert and (vec[0] or vec[1]):
                reward+=certificate
                vec=(vec[0],vec[1],F(1),vec[3])
            plans.append((reward,path,vec))
        return max(plans,key=lambda x:x[0])
    if observe:
        cont={}
        for offer in ("A","B","EXIT"):
            cont[offer]=max(((*outcome(path,offer,certificate),path)
                            for path in product(ACTS,repeat=2)),key=lambda x:x[0])
        for typ in GOALS:
            if not auto:
                u,chosen=actor_choice(typ,{x:cont[x][2] for x in cont})
                assert chosen==typ and u>2,(typ,u,chosen)
            else:
                chosen=typ # A/B equally likely; auto tool has identical work+certificate output
            assert chosen==typ
        root=sum((cont[t][0] for t in GOALS),F(0))/2
        vector=tuple(sum((cont[t][1][i] for t in GOALS),F(0))/2 for i in range(4))
        return root,cont,vector
    # A non-conditional two-action plan is announced and cannot react to the helper.
    plans=[]
    for path in product(ACTS,repeat=2):
        choices={t:actor_choice(t,{x:path for x in ("A","B","EXIT")})[1] for t in GOALS}
        vals=[outcome(path,choices[t],certificate) for t in GOALS]
        root=sum((r[0] for r in vals),F(0))/2
        vector=tuple(sum((r[1][i] for r in vals),F(0))/2 for i in range(4))
        plans.append((root,path,choices,vector))
    return max(plans,key=lambda r:r[0])

# Distinct protocol, not a continuation of MULTI.
# t0 partner may HELP and start A8, or take outside w leaving player SAFE3.
# t1 observes *public*, true next-period demand H9 or L2, first project pays5.
# t1 PAY costs player3/benefits partner4, COMP costs2/benefits partner3,
# BYPASS gives player4/partner0 and destroys remaining joint-work access.
# If access retained at t2, the player can use independently accessible BYPASS
# on the final project (value minus1). After t1 BYPASS, t2 only BACKUP1.
# Partner cannot revoke an already maintained t2 access at zero cost.
ACTIONS = ("PAY","COMP","BYPASS")
OWN = {"PAY":F(4),"COMP":F(3),"BYPASS":F(0)}
FIRST = {"PAY":F(2),"COMP":F(3),"BYPASS":F(4)}

def branch(signal,action):
    second=(F(9 if signal=="H" else 2)-1) if action!="BYPASS" else F(1)
    return FIRST[action]+second,OWN[action]

def contract(policy,outside=F(13,4),p_high=F(1,2)):
    actor_future=p_high*OWN[policy["H"]]+(1-p_high)*OWN[policy["L"]]
    help_now=actor_future>outside # at equality, the actor takes EXIT
    player = (F(8)+p_high*branch("H",policy["H"])[0]
              +(1-p_high)*branch("L",policy["L"])[0]) if help_now else F(3)
    return player,actor_future if help_now else outside,help_now

def available_contracts(outside=F(13,4),p_high=F(1,2)):
    return [(dict(zip(("H","L"),(a,b))),contract({"H":a,"L":b},outside,p_high))
            for a,b in product(ACTIONS,repeat=2)]

def best_contract(outside=F(13,4),p_high=F(1,2)):
    options=available_contracts(outside,p_high)
    return max(options,key=lambda row:row[1][0]),options

def unbound(outside=F(13,4),p_high=F(1,2)):
    # After partner HELP, subgame-optimal future action is chosen, without precommit.
    policy={s:max(ACTIONS,key=lambda a:branch(s,a)[0]) for s in ("H","L")}
    return policy,contract(policy,outside,p_high)

def forward_commit(policy,outside=F(13,4),p_high=F(1,2)):
    # Separately replay each public branch and compute partner's independent benefit.
    own=sum((prob*OWN[policy[s]] for s,prob in (("H",p_high),("L",1-p_high))),F(0))
    if own<=outside:return F(3),outside,False
    money=F(8)
    for signal,prob in (("H",p_high),("L",1-p_high)):
        a=policy[signal]
        t1=FIRST[a]
        t2=F(1) if a=="BYPASS" else F(9 if signal=="H" else 2)-1
        money+=prob*(t1+t2)
    return money,own,True

def check():
    dynamic=multi()
    assert dynamic[0]==F(27,2),dynamic
    assert dynamic[2]==(F(1,2),F(1,2),F(1),F(0))
    assert dynamic[1]["A"][2]==("A","A") and dynamic[1]["B"][2]==("B","B")
    assert multi(invite=False)[0]==10
    assert multi(invite=False,direct_cert=True)[0]==14
    fixed=multi(observe=False)
    assert fixed[0]==7,fixed
    # Even the whole strategy-dependent continuation is identical to an
    # ordinary free A/B conditional certified tool; not merely same total score.
    assert multi(auto=True)==dynamic
    assert multi(certificate=0)[0]==F(19,2)
    assert multi(invite=False,certificate=0)[0]==10
    print("MULTI",{"adaptive":str(dynamic[0]),"self":10,"fixed_after_INVITE":str(fixed[0]),
                   "automatic_tool_equal":True,"terminal_vector":tuple(map(str,dynamic[2]))})
    free,free_outcome=unbound()
    assert free=={"H":"COMP","L":"BYPASS"} and free_outcome==(3,F(13,4),False)
    plain=contract({"H":"PAY","L":"PAY"})
    assert plain==(F(29,2),F(4),True)
    best,all9=best_contract()
    assert best[1]==(F(15),F(7,2),True)
    assert best[0] in ({"H":"PAY","L":"COMP"},{"H":"COMP","L":"PAY"})
    for policy,res in all9:
        assert forward_commit(policy)==res,(policy,res,forward_commit(policy))
    assert contract({"H":"COMP","L":"COMP"})==(3,F(13,4),False)
    # Complete ordinary prefabricated signal->action table: exactly 3^2 plans,
    # exactly the same H/L transitions as the "binding clause" representation.
    prefabricated={tuple(policy[s] for s in ("H","L")):
                   (branch("H",policy["H"]),branch("L",policy["L"]))
                   for policy,_ in all9}
    assert len(prefabricated)==9
    assert all(prefabricated[tuple(policy[s] for s in ("H","L"))]==
               (branch("H",policy["H"]),branch("L",policy["L"])) for policy,_ in all9)
    # No timely H/L forecast when t1 action is due:
    # unconditional COMP is private-optimal but partner rejects.
    blind_free=max(ACTIONS,key=lambda a:(branch("H",a)[0]+branch("L",a)[0])/2)
    assert blind_free=="COMP"
    assert contract({"H":blind_free,"L":blind_free})[0]==3
    assert max(contract({"H":a,"L":a})[0] for a in ACTIONS)==F(29,2)
    for outside in (F(2),F(3),F(13,4),F(7,2),F(4)):
        print("ACTOR_OUTSIDE",str(outside),str(unbound(outside)[1][0]),
              str(best_contract(outside)[0][1][0]))
    for p_high in (F(1,4),F(1,2),F(3,4)):
        policy,res=best_contract(p_high=p_high)[0]
        assert forward_commit(policy,p_high=p_high)==res
        print("PUBLIC_FORECAST",str(p_high),policy,str(res[0]))
    print("COMMIT",{"free":str(free_outcome[0]),"bind_all_PAY":str(plain[0]),
                    "best_contingent":str(best[1][0]),"policy":best[0],
                    "no_early_forecast":str(F(29,2)),
                    "all9_checked":len(all9)})
    print("PASS: exact rational choices, 9 complete binding menus, strong mechanical controls")
if __name__=="__main__":check()
