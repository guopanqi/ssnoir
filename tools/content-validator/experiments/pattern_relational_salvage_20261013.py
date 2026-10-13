#!/usr/bin/env python3
"""Relational permission, partner outside option and salvage timing.

Exact finite game (Fraction). Not independent blind AI, a human study or Unity.
A private opportunity at turn 1 is observed by the partner, not the player,
until the partner stays/leaves after the player's action. All payoffs are public.
"""
from dataclasses import dataclass
from functools import lru_cache
from fractions import Fraction as Q

DAYS=4
A_PAY=16
H_PAY=10
SIDE=2
SALVAGE=5
HIGH=12
LOW=3
BASE_EXIT=4

@dataclass(frozen=True)
class State:
    turn:int=0
    use:int=0
    help:int=0
    secured:bool=False
    side:int=0
    present:bool=True

def actions(s,can_secure):
    if not s.present: return ("SIDE",)
    if s.secured: return ("HELP","SIDE")
    out=["HELP","USE","SIDE"]
    if can_secure and s.use>=1: out.append("SECURE")
    return tuple(out)

def advance(s,a):
    if a=="USE":return State(s.turn+1,s.use+1,s.help,s.secured,s.side,s.present)
    if a=="HELP":return State(s.turn+1,s.use,s.help+1,s.secured,s.side,s.present)
    if a=="SIDE":return State(s.turn+1,s.use,s.help,s.secured,s.side+1,s.present)
    if a=="SECURE":return State(s.turn+1,0,s.help,True,s.side,s.present)
    raise ValueError(a)

def payoff(s):
    player=SIDE*s.side+SALVAGE*int(s.secured)
    if s.present and s.use>=2 and not s.secured:player+=A_PAY
    return (Q(player),Q(H_PAY*int(s.present and s.help>=1)))

def game(*,can_secure=True,public=True,can_leave=True,tie="stay"):
    assert tie in ("stay","leave")
    def outside(t,signal):
        return HIGH if t==1 and signal==1 else LOW if t==1 else BASE_EXIT

    @lru_cache(None)
    def solve(s,signal=-1):
        if s.turn==DAYS:return (*payoff(s),())
        if not s.present:
            p,q,trail=solve(advance(s,"SIDE"),0)
            return p,q,(("SIDE","GONE"),)+trail
        if s.turn==1 and signal==-1:
            high=solve(s,1)
            low=solve(s,0)
            return (high[0]+low[0])/2,(high[1]+low[1])/2,("PUBLIC_OFFERS",high[2],low[2])
        scored=[(a,response(s,a,signal)) for a in actions(s,can_secure)]
        action,result=max(scored,key=lambda x:x[1][0])
        return result[0],result[1],((action,result[2]),)+result[3]

    def response(s,action,signal):
        after=advance(s,action)
        # If the partner stays, both subsequently optimize again.
        stay=solve(after,0 if s.turn>=1 else -1)
        if can_leave:
            leave_value=outside(s.turn,signal)
            if leave_value>stay[1] or (leave_value==stay[1] and tie=="leave"):
                departed=State(after.turn,0,0,after.secured,after.side,False)
                p,_,t=solve(departed,0)
                return p,Q(leave_value),"EXIT",t
        return stay[0],stay[1],"STAY",stay[2]

    @lru_cache(None)
    def hidden(s):
        if s.turn==DAYS:return (*payoff(s),())
        if s.turn==0:
            options=[]
            for action in actions(s,can_secure):
                after=advance(s,action)
                partner_stays=hidden(after)
                if can_leave and partner_stays[1]<BASE_EXIT:
                    departed=State(after.turn,0,0,after.secured,after.side,False)
                    p,_,t=solve(departed,0)
                    options.append((action,(p,Q(BASE_EXIT),("EXIT",t))))
                else:options.append((action,(partner_stays[0],partner_stays[1],("STAY",partner_stays[2]))))
            a,r=max(options,key=lambda x:x[1][0])
            return r[0],r[1],((a,r[2]),)
        if s.turn==1 and s.present:
            # Same player action must be chosen before observing HIGH/LOW.
            # HIGH>partner's maximum possible H payoff; it strictly exits,
            # so STAY afterwards reveals LOW (no off-path Bayesian assumption).
            results=[]
            for a in actions(s,can_secure):
                hi=response(s,a,1)
                lo=response(s,a,0)
                results.append((a,((hi[0]+lo[0])/2,(hi[1]+lo[1])/2,hi,lo)))
            a,(p,q,hi,lo)=max(results,key=lambda x:x[1][0])
            return p,q,((a,"HIDDEN"),hi[2],lo[2])
        return solve(s,0)

    def root():return solve(State()) if public else hidden(State())
    return root,solve,hidden,response

def evaluate_committed(rule,*,can_secure=True,public=True,can_leave=True):
    """Publicly announced player policy: partner best-responds to that policy.

    This is a different (commitment) protocol from the free dynamic game;
    compare as an interpretable policy baseline, NOT automatic dominance.
    """
    @lru_cache(None)
    def walk(s,signal=-1):
        if s.turn==DAYS:return payoff(s)
        if not s.present:return walk(advance(s,"SIDE"),0)
        if s.turn==1 and signal==-1:
            if public:
                hi,lo=walk(s,1),walk(s,0)
                return ((hi[0]+lo[0])/2,(hi[1]+lo[1])/2)
            a=rule(s,-1)
            hi,lo=answer(s,a,1),answer(s,a,0)
            return ((hi[0]+lo[0])/2,(hi[1]+lo[1])/2)
        a=rule(s,signal)
        assert a in actions(s,can_secure),(a,s)
        return answer(s,a,signal)

    def answer(s,a,signal):
        after=advance(s,a)
        stay=walk(after,0 if s.turn>=1 else -1)
        w=HIGH if s.turn==1 and signal==1 else LOW if s.turn==1 else BASE_EXIT
        if can_leave and w>stay[1]:
            gone=State(after.turn,0,0,after.secured,after.side,False)
            return walk(gone,0)[0],Q(w)
        return stay
    return walk(State())

def always_side(s,signal):return "SIDE"
def readable_public(s,signal):
    if s.secured:return "SIDE"
    if s.turn==0:return "USE"
    if s.turn==1:return "SECURE" if signal==1 else "HELP"
    if s.help==0:return "HELP"
    return "USE" if s.use<2 else "SIDE"
def readable_no_secure(s,signal):
    if s.turn==0:return "SIDE"
    if s.turn==1:return "SIDE" if signal==1 else "HELP"
    if s.help==0:return "HELP"
    return "USE" if s.use<2 else "SIDE"
def readable_private(s,signal):
    if s.turn==0:return "HELP"
    if s.turn==1:return "SIDE"
    return "USE" if s.use<2 else "SIDE"
def premature_secure(s,signal):
    if s.turn==0:return "USE"
    if s.turn==1:return "SECURE"
    return "SIDE"

def main():
    expected={(False,True):Q(13),(False,False):Q(12),
              (True,True):Q(27,2),(True,False):Q(12)}
    for (secure,public),want in expected.items():
        player,partner,trace=game(can_secure=secure,public=public)[0]()
        assert (player,partner)==(want,Q(11)),(secure,public,player,partner)
        print("REL-01",{"secure":secure,"public":public,
                        "player":str(player),"partner":str(partner),
                        "trace":trace})
    # Same naturally reached state after opening with USE:
    s=State(turn=1,use=1)
    solve=game()[1]
    hi=solve(s,1);lo=solve(s,0)
    assert hi[2][0]==("SECURE","EXIT") and lo[2][0]==("HELP","STAY")
    assert (hi[0],lo[0])==(Q(9),Q(18))
    assert game(can_secure=False)[1](s,1)[0]==Q(6)
    # A completed/cashed result is mutually exclusive; SECURE cannot be a
    # free add-on to a subsequently completed A target.
    assert game(can_leave=False)[0]()[0]==Q(20)
    for tie in ("stay","leave"):
        assert game(tie=tie)[0]()[0]==Q(27,2)
        assert game(can_secure=False,tie=tie)[0]()[0]==Q(13)
    controls=[
      ("all SIDE",always_side,{},Q(8)),
      ("conditional SECURE",readable_public,{},Q(27,2)),
      ("without SECURE",readable_no_secure,{"can_secure":False},Q(13)),
      ("late/private offer",readable_private,{"public":False},Q(12)),
      ("always bank",premature_secure,{},Q(9))
    ]
    for name,rule,flags,want in controls:
        payoff_pair=evaluate_committed(rule,**flags)
        print("CONTROL",name,tuple(map(str,payoff_pair)))
        assert payoff_pair[0]==want,(name,payoff_pair)
    print("PASS relational permission, timely partial salvage and strong readable scripts")

if __name__=="__main__":
    main()
