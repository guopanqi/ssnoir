#!/usr/bin/env python3
"""R438-R445: an offstage actor works independently even when player acts elsewhere.
Frozen four-turn, two-action coverage model, not an empirical social agent.
"""
from fractions import Fraction as F
from itertools import product
import json

T = 4
ACTIONS = 'AB'

def other_action(theta, turn, agenda):
    if agenda == 'switch' and turn >= 2:
        return 'B' if theta == 'A' else 'A'
    return theta

def outcome(player, other):
    return len({player, other}) if player in ACTIONS else 1

def fixed_value(actions, agenda):
    assert len(actions) == T
    return sum(sum(outcome(actions[t], other_action(theta,t,agenda))
                   for t in range(T)) for theta in ACTIONS) * F(1,2)

def best_fixed(agenda):
    values = {''.join(a):fixed_value(a,agenda)
              for a in product(ACTIONS, repeat=T)}
    maximum = max(values.values())
    return maximum, sorted(a for a,x in values.items() if x == maximum)

def adaptive_witness(agenda):
    # Turn 0 chooses A, and the other acts regardless; its public action
    # unambiguously reveals theta. The player thereafter complements it.
    paths = {}
    for theta in ACTIONS:
        actions = 'A'+''.join('B' if other_action(theta,t,agenda)=='A' else 'A'
                            for t in range(1,T))
        paths[theta]={'player':actions,
                      'other':''.join(other_action(theta,t,agenda) for t in range(T)),
                      'reward':sum(outcome(actions[t],other_action(theta,t,agenda))
                                    for t in range(T))}
    return sum(p['reward'] for p in paths.values())*F(1,2),paths

def audit():
    static_opt,static_paths=adaptive_witness('static')
    changed_opt,changed_paths=adaptive_witness('switch')
    st_fixed,st_fixed_ways=best_fixed('static')
    ch_fixed,ch_fixed_ways=best_fixed('switch')
    assert len(st_fixed_ways)==len(ch_fixed_ways)==16
    assert static_opt==changed_opt==F(15,2)                 # R438 / R443
    assert st_fixed==ch_fixed==F(6)                        # R439
    # Before first report, choosing either A or B gets 3/2 expected union;
    # after observing its independent move the upper bound is 2 each turn.
    assert static_opt == F(3,2)+3*2                        # R440
    # Spending turn zero only probing still lets the other do one action.
    probe=F(1)+3*2
    assert probe==7 and probe<static_opt                   # R441
    # If partner report is held until after turn 1, two uncertain rounds.
    lag=2*F(3,2)+2*2
    assert lag==7                                         # R442
    # Fresh unrelated private preference every turn removes learning.
    iid=4*F(3,2)
    assert iid==6                                         # R444
    player_alone=F(4);partner_alone=F(4)
    assert static_opt>player_alone and static_opt<2*T      # R445
    return {'schema':'ssnoir.r438-r445.offstage-contribution.v1',
      'protocol':'4 turns. Each turn player chooses A or B (or may spend the action to PROBE). Independently, an offstage actor always performs its own selected A/B, including during PROBE. Each distinct A/B completed in the turn counts one reward; two duplicate activities count just one. Partner privately starts with theta=A/B equiprobable, public partner action revealed after each turn. STATIC actor repeats theta; SWITCH actor is constrained by a two-turn agenda to switch from theta to other task on turns 2 and 3. The actor does NOT observe player choices nor rationally negotiate; it has a scripted independent agenda. No invented human/agent behavior.',
      'R438_static_adaptive_optimum':static_opt,
      'R439_all_fixed_16':{'best':st_fixed,'winners':st_fixed_ways},
      'R440_simple_complement_policy':static_paths,
      'R441_early_probe_vs_act':{'probe_then_complement':probe,'act_then_complement':static_opt},
      'R442_one_full_turn_lag':lag,
      'R443_switching_longer_agenda':{'adaptive':changed_opt,'fixed':ch_fixed,'witness':changed_paths},
      'R444_no_stable_type_fresh_each_turn':iid,
      'R445_capacity_control':{'player_alone':player_alone,'partner_alone':partner_alone,'joint_adaptive':static_opt,
                               'two_task_slots_upper_bound':F(8)},
      'verdict':'A genuine exogenous independent action exists and feedback can change the player allocation, but one sentence (act once, infer type, then complement) exactly reaches optimal 15/2; all fixed schedules score 6. An autonomous actor has not automatically created a novel interpersonal gameplay activity. Capacity and R350-style type learning explain this case.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {k:enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
