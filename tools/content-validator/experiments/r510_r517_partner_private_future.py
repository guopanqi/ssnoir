#!/usr/bin/env python3
"""R510-R517: offstage agent with its own scarce-effort planning, private type.
The agent anticipates uniform future offers (not a PBE or simulated human).
Player's optimal contingent controller is an exact finite Bayesian DP.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

TYPES=('A','B')
ACTIONS=('A','B','SELF','HELP')
HORIZON=4

def partner(future=True):
    def post_help(energy,offer):
        return min(2,energy+1) if offer=='HELP' else energy

    @lru_cache(None)
    def own_future(t,theta,energy):
        if t==HORIZON:return F(0)
        total=F(0)
        for public_q,offer in product((0,1),ACTIONS):
            event,own_reward,new_e=choose(t,theta,energy,public_q,offer)
            total+=own_reward+own_future(t+1,theta,post_help(new_e,offer))
        return total/8

    def choose(t,theta,energy,public_q,offer):
        possibilities=[('REST',F(0),min(2,energy+1))]
        if energy:
            possibilities.append(('OUT',F(2*public_q),energy-1))
            if offer in TYPES:
                possibilities.append(('ACCEPT',F(2+2*(offer==theta)),energy-1))
        def value(z):
            event,own_reward,new_e=z
            future_return=(own_future(t+1,theta,post_help(new_e,offer))
                           if future else F(0))
            return own_reward+future_return,{'REST':2,'OUT':1,'ACCEPT':0}[event]
        return max(possibilities,key=value)
    return choose

def normalize(states):
    merged={}
    for theta,e,weight in states:
        merged[(theta,e)]=merged.get((theta,e),0)+weight
    return tuple((theta,e,weight) for (theta,e),weight in sorted(merged.items())
                 if weight)

def model(observe=True,can_help=True,known_type=False,known_energy=False,
          future=True,accept_reward=3):
    own=partner(future)
    allowed=tuple(a for a in ACTIONS if can_help or a!='HELP')
    initial=normalize((theta,e,1) for theta,e in product(TYPES,(0,1)))

    def branches(t,belief,public_q,offer):
        grouped={}
        total=sum(w for theta,e,w in belief)
        for theta,e,weight in belief:
            event,own_pay,new_e=own(t,theta,e,public_q,offer)
            if offer=='HELP':new_e=min(2,new_e+1)
            observation=event if observe else 'HIDDEN'
            reward=accept_reward*(event=='ACCEPT')+(offer=='SELF')
            grouped.setdefault(observation,[]).append((theta,new_e,weight,reward))
        outcome=[]
        for observed,group in grouped.items():
            amount=sum(w for _,_,w,_ in group)
            nxt=normalize((theta,e,w) for theta,e,w,_ in group)
            reward=F(sum(w*v for theta,e,w,v in group),amount)
            outcome.append((F(amount,total),reward,nxt,observed))
        return outcome

    @lru_cache(None)
    def dp(t,belief):
        if t==HORIZON:return F(0)
        return sum((max(sum(p*(reward+dp(t+1,next_belief))
                         for p,reward,next_belief,observed in
                         branches(t,belief,q,a)) for a in allowed)
                    for q in (0,1)),F(0))/2

    if known_type:
        score=sum(dp(0,normalize((theta,e,1) for e in (0,1)))
                  for theta in TYPES)/2
    elif known_energy:
        score=sum(dp(0,normalize((theta,e,1) for theta in TYPES))
                  for e in (0,1))/2
    else:score=dp(0,initial)
    return score,dp.cache_info().currsize

def fixed_value(script,own):
    result=F(0)
    for theta,start_e in product(TYPES,(0,1)):
        for qs in product((0,1),repeat=HORIZON):
            e=start_e
            total=0
            for t,(offer,q) in enumerate(zip(script,qs)):
                event,private_pay,e=own(t,theta,e,q,offer)
                if offer=='HELP':e=min(2,e+1)
                total+=3*(event=='ACCEPT')+(offer=='SELF')
            result+=F(total,4*2**HORIZON)
    return result

def reactive_value(rule,own):
    result=F(0)
    for theta,start_e in product(TYPES,(0,1)):
        for qs in product((0,1),repeat=HORIZON):
            e=start_e
            total=0
            last='START'
            for t,q in enumerate(qs):
                offer=rule[last]
                event,private_pay,e=own(t,theta,e,q,offer)
                if offer=='HELP':e=min(2,e+1)
                total+=3*(event=='ACCEPT')+(offer=='SELF')
                last=event
            result+=F(total,4*2**HORIZON)
    return result

def audit():
    root,states=model()
    assert root==F(29,4)                         # R510
    owner=partner()
    fixed={script:fixed_value(script,owner)
           for script in product(ACTIONS,repeat=HORIZON)}
    assert len(fixed)==256
    assert max(fixed.values())==F(25,4)          # R511
    event_names=('START','ACCEPT','REST','OUT')
    reactive={script:reactive_value(dict(zip(event_names,script)),owner)
              for script in product(ACTIONS,repeat=4)}
    assert len(reactive)==256
    assert max(reactive.values())==F(105,16)     # R512
    blind=model(observe=False)[0]
    assert blind==F(25,4)                        # R513
    no_help=model(can_help=False)[0]
    assert no_help==F(115,16)                    # R514
    known_t=model(known_type=True)[0]
    known_e=model(known_energy=True)[0]
    assert known_t==8 and known_e==F(31,4)       # R515
    myopic=model(future=False)[0]
    assert myopic==F(217,32)                     # R516
    low=model(accept_reward=2)[0]
    assert low==F(43,8)                          # R517
    return {
        'schema':'ssnoir.r510-r517.autonomous-scarce-effort-belief.v1',
        'protocol':'Four turns. Partner privately has fixed theta=A/B and initial effort 0/1, equiprobable. Public external opportunity q=0/1 each turn. Player chooses ASK-A, ASK-B, SELF(guaranteed 1), HELP(0 reward, post-partner turn adds 1 effort up to cap 2). Regardless of offer, partner autonomously chooses REST(+1 effort), OUT(spend 1 effort, own utility 2q), or ACCEPT only if offered and has effort (spend 1, own utility 2+2 if offer matches theta). Player earns 3 iff ACCEPT. Partner maximizes own expected total 4-turn utility by backward induction forecasting future public q and future offers A/B/SELF/HELP uniformly independently; tie REST>OUT>ACCEPT. Player can deviate from forecast and optimizes own finite Bayesian DP using publicly revealed q and observed event. Thus NOT a full strategically consistent equilibrium, not an agent or human simulation.',
        'R510_player_adaptive':{'value':root,'belief_states_solved':states},
        'R511_best_of_256_fixed':{'value':max(fixed.values()),'winners':[list(s) for s,v in fixed.items() if v==max(fixed.values())]},
        'R512_best_of_256_last-event-rules':{'value':max(reactive.values()),
            'winners':[dict(zip(event_names,s)) for s,v in reactive.items() if v==max(reactive.values())]},
        'R513_no_partner_event_observation':blind,
        'R514_no_help':no_help,
        'R515_private_state_revelation':{'theta_known':known_t,'energy_known':known_e},
        'R516_myopic_partner':myopic,
        'R517_low_acceptance_reward':low,
        'verdict':'A partner truly chooses REST/OUT/ACCEPT to maximize its own longer-horizon opportunity value; player observes actions rather than scripted orders. Response history is materially useful: 29/4 vs 25/4 no observation/strong fixed, and vs 105/16 best single-last-event rule. However helper transfer adds only 1/16 at the base root, myopic partner changes assumptions, and the partner forecasts random offers rather than the actual player policy. This is an exploratory belief-updating + autonomous scheduling composition, not evidence of a new human social gameplay primitive.'
    }
def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(tuple,list)):return [enc(v) for v in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
