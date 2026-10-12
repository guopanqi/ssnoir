#!/usr/bin/env python3
"""R398-R405: does an authored delegation script create gameplay value,
or simply an extra parallel worker?

Each of three turns public input b in {0,1} appears. Actions A and B
yield fixed positive rewards, but only one manual action per turn.
INSTALL chooses one of four public Boolean policies for an automatic
executor, consuming the entire current turn, operational next turn.
In serial mode manual work overrides bot: only ONE action per turn.
In parallel mode installed bot can act alongside one manual task, but
two performances of SAME task do not stack.
A full public-state DP with all available installations and rewrites
is compared to independent closed-form capacity bound and each program.
"""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
import json

PROGRAMS=("A","B","COPY","INVERT")
T=3

def bot(g,b):
    if g=="A":return "A"
    if g=="B":return "B"
    return ("A" if b==0 else "B") if g=="COPY" else ("B" if b==0 else "A")

def step_rewards(rA,rB,mode,program,b):
    r={"A":F(rA),"B":F(rB)}
    if mode=="serial":
        return {"MANUAL-A":r["A"],"MANUAL-B":r["B"],
                "AUTO":r[bot(program,b)] if program else F(0)}
    v={"MANUAL-A":r["A"],"MANUAL-B":r["B"]}
    if program:
        chosen=bot(program,b)
        v["MANUAL-A"]+=r[chosen] if chosen!="A" else 0
        v["MANUAL-B"]+=r[chosen] if chosen!="B" else 0
        v["AUTO"]=r[chosen]
    return v

def solve(rA,rB,mode):
    @lru_cache(None)
    def V(t,program):
        if t==T:return F(0)
        return sum(F(1,2)*max(actions(t,program,b).values()) for b in (0,1))
    def actions(t,program,b):
        x={name:gain+V(t+1,program)
           for name,gain in step_rewards(rA,rB,mode,program,b).items()}
        for p in PROGRAMS:
            if p!=program:
                x["INSTALL-"+p]=V(t+1,p) # takes this action, zero current reward
        return x
    root=V(0,None)
    first={str(b):actions(0,None,b) for b in (0,1)}
    return root,first,V.cache_info().currsize

def independent_fixed_programs(rA,rB,mode):
    # Program install on turn 0. Replay all 2^3 public input tapes and
    # choose the best second/third manual task given output, without
    # consulting optimal controller or current hidden future bit.
    vals={}
    for program in PROGRAMS:
        total=F(0)
        for inputs in product((0,1),repeat=T):
            # No result on installation turn 0.
            scores=[]
            for b in inputs[1:]:
                gains=step_rewards(rA,rB,mode,program,b)
                scores.append(max(gains.values()))
            total+=sum(scores)/8
        vals[program]=total
    return vals

def enc(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):enc(z) for k,z in v.items()}
    if isinstance(v,(list,tuple)):return [enc(z) for z in v]
    return v

def audit():
    rows={}
    for ra,rb in ((1,1),(2,1),(3,1)):
        key=f"{ra}:{rb}"
        basic=T*max(ra,rb)
        case={"manual_only":F(basic)}
        for mode in ("serial","parallel"):
            dp,first,states=solve(ra,rb,mode)
            allfixed=independent_fixed_programs(ra,rb,mode)
            fixed=max(allfixed.values())
            expected=max(F(basic),F(T-1)*(F(ra)+F(rb))) if mode=="parallel" else F(basic)
            assert dp==expected
            assert fixed==(F(T-1)*(F(ra)+F(rb)) if mode=="parallel" else F(T-1)*max(ra,rb))
            assert len(set(allfixed.values()))==1
            case[mode]={"full_public_DP":dp,"one_initial_install_then_manual":fixed,
                        "all_four_programs":allfixed,
                        "root_Q_for_both_inputs":first,"states":states}
        rows[key]=case
    assert (rows["1:1"]["parallel"]["full_public_DP"],
            rows["2:1"]["parallel"]["full_public_DP"],
            rows["3:1"]["parallel"]["full_public_DP"])==(4,6,9)
    assert (rows["1:1"]["serial"]["full_public_DP"],
            rows["2:1"]["serial"]["full_public_DP"],
            rows["3:1"]["serial"]["full_public_DP"])==(3,6,9)
    return enc({"schema":"ssnoir.r398-r405.scripted-delegation-capacity.v1",
      "scope":"three-turn exact abstraction; bot is a tool, NOT a partner with independent preference or private knowledge",
      "rules":"Public input b is revealed each turn. Two positively rewarded task types A/B with fixed rewards rA/rB; manual capacity 1 per turn. INSTALL any rule {constant A,constant B,copy bit,invert bit} costs an entire turn, bot starts next turn. In SERIAL the bot and manual compete for same single action slot; in PARALLEL installed bot can independently perform its rule's task while player does one different task; duplicate tasks score only once. Programs can be overwritten at the same action cost.",
      "R398_serial_equal_tasks":rows["1:1"]["serial"],
      "R399_parallel_equal_tasks":rows["1:1"]["parallel"],
      "R400_serial_skew_2_1":rows["2:1"]["serial"],
      "R401_parallel_skew_2_1":rows["2:1"]["parallel"],
      "R402_high_reward_asymmetry":rows["3:1"],
      "R403_all_four_installable_grammars":{k:{"serial":v["serial"]["all_four_programs"],
                                           "parallel":v["parallel"]["all_four_programs"]} for k,v in rows.items()},
      "R404_capacity_counterfactual":{k:{"manual_only":v["manual_only"],
                "serial":v["serial"]["full_public_DP"],
                "parallel":v["parallel"]["full_public_DP"]} for k,v in rows.items()},
      "R405_verdict":"The mere ability to INSTALL an authored action-selection policy never improves the same one-slot task capacity; MANUAL can always emulate the chosen task after seeing the same public input. Parallel execution raises the equal-task payoff from3 to4, but ALL FOUR different public programs give the same score because the player can manually take the other task. This is extra throughput, NOT evidence that authoring a smarter or personalized algorithm created new player decisions."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
