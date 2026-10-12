#!/usr/bin/env python3
"""S2-D R122-124. Exact Fraction, independent paper model, not C#+Scheme.

Semantic change: investigating the letter opens a lead at progress 2; handing
in the letter is a separate, player-selected, one-action commitment that closes
the unfinished lead. Both investigate and hand-in must happen before day-one
deadline. Fighter help also expires then; the lead may continue on day two.
No gain roll can reveal AND close the lead in the same action.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import permutations, product
import json
import sys

def study(bonus=F(2), no_lead=False, no_close=False, no_gate=False, no_deadline=False):
    n_total, last_day = 7, 3             # four actions day1, three day2
    def reward(a, b, c, submitted):
        return 2*F(submitted) + bonus*F(b >= 2) + 2*F(c >= 2)
    def options(a, b, c, submitted, n):
        if n == 0: return ()
        choices = []
        if a < 2 and not submitted and (no_deadline or n > last_day):
            choices.append("investigate")
        if a == 2 and not submitted and (no_deadline or n > last_day):
            choices.append("submit")
        if b < 2 and n > last_day: choices.append("help")
        if not no_lead and c < 2 and (no_gate or a == 2) and (no_close or not submitted):
            choices.append("lead")
        return tuple(choices)
    def move(a,b,c,submitted,n,x):
        if x == "submit":
            assert a == 2 and not submitted
            return ((F(1),a,b,c,True,n-1),)
        rows = []
        for gain in range(3):
            rows.append((F(1,3),
                         min(2,a+gain) if x=="investigate" else a,
                         min(2,b+gain) if x=="help" else b,
                         min(2,c+gain) if x=="lead" else c,
                         submitted,n-1))
        return tuple(rows)
    @lru_cache(None)
    def value(a,b,c,submitted,n):
        possible=options(a,b,c,submitted,n)
        if not possible:return reward(a,b,c,submitted)
        return max(sum(ch*value(aa,bb,cc,ss,nn) for ch,aa,bb,cc,ss,nn in move(a,b,c,submitted,n,x))
                   for x in possible)
    def qs(state):
        a,b,c,submitted,n=state
        return {x:sum(ch*value(aa,bb,cc,ss,nn) for ch,aa,bb,cc,ss,nn in move(*state,x))
                for x in options(*state)}
    def eval_policy(chooser):
        @lru_cache(None)
        def recurse(a,b,c,s,n):
            acts=options(a,b,c,s,n)
            if not acts:return reward(a,b,c,s)
            chosen=chooser((a,b,c,s,n),acts)
            assert chosen in acts,(chosen,acts)
            return sum(pr*recurse(aa,bb,cc,ss,nn) for pr,aa,bb,cc,ss,nn in move(a,b,c,s,n,chosen))
        return recurse(0,0,0,False,n_total)
    fixed = [(eval_policy(lambda state,available,order=order:
                          next(x for x in order if x in available)),order)
             for order in permutations(("investigate","submit","help","lead"))]
    fixed_best=max(fixed)
    # Public decision-rule family, never consults hidden DP Q-values.
    simple = []
    for help_progress,help_deadline,lead_cutoff,lead_started in product(range(3),range(3,7),range(1,7),(False,True)):
        def choose(state, acts, hp=help_progress,hd=help_deadline,lc=lead_cutoff,ls=lead_started):
            a,b,c,submitted,n=state
            if "help" in acts and (b>=hp or n<=hd):return "help"
            if "lead" in acts and ("submit" not in acts or n>lc or ls and c>0):
                return "lead"
            if "submit" in acts:return "submit"
            if "investigate" in acts:return "investigate"
            return next(iter(acts))
        simple.append((eval_policy(choose),help_progress,help_deadline,lead_cutoff,lead_started))
    simple_best=max(simple)
    total_both=F(0)
    strict_lead=F(0)
    strict_submit=F(0)
    strict_other=F(0)
    example={}
    weighted_visits={}
    # Tie precedence is predetermined; forward distributions include only
    # states on this single publicly tie-broken optimal policy.
    def traverse(state, mass, saw_lead=False, saw_submit=False):
        nonlocal total_both, strict_lead, strict_submit, strict_other
        poss=options(*state)
        if not poss:
            return mass*F(saw_lead),mass*F(saw_submit)
        weighted_visits[state]=weighted_visits.get(state,F(0))+mass
        qvals=qs(state)
        best=max(qvals.values())
        action=next(x for x in ("help","lead","submit","investigate")
                    if x in qvals and qvals[x]==best)
        if "lead" in qvals and "submit" in qvals:
            total_both+=mass
            if action=="lead" and qvals["lead"]>max(v for x,v in qvals.items() if x!="lead"):
                strict_lead+=mass
                saw_lead=True
                if "lead" not in example:
                    example["lead"]={"state":list(state),"path_mass_at_first_visit":str(mass),
                                     "values":{k:str(v) for k,v in qvals.items()}}
            elif action=="submit" and qvals["submit"]>max(v for x,v in qvals.items() if x!="submit"):
                strict_submit+=mass
                saw_submit=True
                if "submit" not in example:
                    example["submit"]={"state":list(state),"path_mass_at_first_visit":str(mass),
                                       "values":{k:str(v) for k,v in qvals.items()}}
            else:
                strict_other+=mass
        result=[F(0),F(0)]
        for pr,aa,bb,cc,ss,nn in move(*state,action):
            child=traverse((aa,bb,cc,ss,nn),mass*pr,saw_lead,saw_submit)
            result[0]+=child[0];result[1]+=child[1]
        return tuple(result)
    path_prob=traverse((0,0,0,False,n_total),F(1))
    full=value(0,0,0,False,n_total)
    assert full>=fixed_best[0] and full>=simple_best[0]
    return {"fighter_reward":str(bonus),
      "optimal":str(full),"best_fixed_priority":str(fixed_best[0]),
      "fixed_order":list(fixed_best[1]),"gap_to_fixed":str(full-fixed_best[0]),
      "best_public_rule":{"score":str(simple_best[0]),"gap":str(full-simple_best[0]),
          "help_progress_threshold":simple_best[1],"help_remaining_threshold":simple_best[2],
          "lead_remaining_threshold":simple_best[3],"continue_partially_started_lead":simple_best[4]},
      "lead_and_submit_both_legal_expected_visits":str(total_both),
      "strict_lead_expected_visits":str(strict_lead),
      "strict_submit_expected_visits":str(strict_submit),
      "probability_any_strict_lead":str(path_prob[0]),
      "probability_any_strict_submit":str(path_prob[1]),
      "strict_examples":example,
      "normal_public_states":len(weighted_visits)}

def main():
    rows=[study(v) for v in (F(1),F(3,2),F(2),F(3))]
    core=next(x for x in rows if x["fighter_reward"]=="2")
    assert core["optimal"]=="6598/2187",core
    assert core["strict_lead_expected_visits"]=="1/9",core
    assert core["strict_submit_expected_visits"]=="8/27",core
    assert F(core["probability_any_strict_lead"])>0
    assert F(core["probability_any_strict_submit"])>0
    assert F(core["best_public_rule"]["gap"])==0
    free=F(core["optimal"])
    ablations={"disable_lead":study(F(2),no_lead=True)["optimal"],
               "disable_closure":study(F(2),no_close=True)["optimal"],
               "disable_prerequisite":study(F(2),no_gate=True)["optimal"],
               "disable_day1_letter_deadline":study(F(2),no_deadline=True)["optimal"]}
    assert free>F(ablations["disable_lead"])
    assert F(ablations["disable_closure"])>free
    assert F(ablations["disable_prerequisite"])>=free
    assert F(ablations["disable_day1_letter_deadline"])>=free
    print(json.dumps({"batch":"S2-D true choice, R122-R124",
      "scope":"Exact Fraction 7-action, two-day 3-objective paper; no native C# Scheme or human experience",
      "model":"Investigation unlocks lead; distinct submit closes unfinished lead; both jobs actionable after reveal.",
      "rows":rows,"ablations":ablations},ensure_ascii=False,indent=2))

if __name__=="__main__":main()
