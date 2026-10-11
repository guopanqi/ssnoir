#!/usr/bin/env python3
"""R88/R90: additional NATIVE C# + Scheme semantic consequences.

These are executed decisions in the real content runner, not merely static
source inspection; however they are *not* human playtests or proof that
strategies meaningfully reverse.
"""
import json,sys
from probe_four_scenario_causality import start,act,clock,finish

def r88():
    # Branch A: do not close staircase, two opponent turns take witness.
    p,obs=start("封死退路")
    try:
        first=clock(obs,"楼梯追兵")
        obs=act(p,obs,end=True)
        mid=clock(obs,"楼梯追兵")
        obs=act(p,obs,end=True)
        last=clock(obs,"楼梯追兵")
        result=obs.get("EncounterResult")
        assert (first,mid,last)==(0,1,2),(first,mid,last)
        assert result is not None,"Unsealed deadline should really close encounter"
        unsealed={"pursuit":[first,mid,last],"ended":True}
    finally:finish(p)
    # Branch B: sealing costs a die, prevents future pursuit, and makes
    # the cheap staircase unavailable; ladder repair becomes mandatory
    # unless the player chooses a composure-expensive jump.
    p,obs=start("封死退路")
    try:
        obs=act(p,obs,"楔死楼梯门")
        assert clock(obs,"楼梯追兵")==0
        names={x.get("Card") for x in obs["Operations"]}
        assert "接好逃生梯" in names and "楔死楼梯门" not in names
        after_closure=[]
        for _ in range(2):
            obs=act(p,obs,end=True)
            after_closure.append(clock(obs,"楼梯追兵"))
            assert obs.get("EncounterResult") is None
        assert after_closure==[0,0],after_closure
        sealed={"pursuit":after_closure,"encounter_open":True,
                "new_ladder_action":True}
    finally:finish(p)
    return {"id":"R88","scene":"实验·封死退路",
      "unsealed":unsealed,"sealed":sealed,
      "invariant":"A die spent to seal prevents ALL future pursuit ticks, but adds ladder/jump exit requirement.",
      "warning":"Shows native causal tradeoff only; cannot claim that unsealed and sealed paths are both optimal. Infinite unthreatened waiting after sealing may actually weaken the tension."}

def r90():
    observations={}
    for route,aim in (("穿过货棚","货棚"),("冲下栈桥","栈桥")):
        p,obs=start("枪口认路")
        try:
            opening=json.dumps(obs,ensure_ascii=False)
            assert "当前盯着货棚" in opening
            obs=act(p,obs,route)
            if route=="冲下栈桥":
                assert "底片已被海水毁掉" in json.dumps(obs,ensure_ascii=False)
            obs=act(p,obs,end=True)
            body=json.dumps(obs,ensure_ascii=False)
            assert "当前盯着"+aim in body
            observations[aim]={"aim_updated":True,
                "evidence_destroyed":route=="冲下栈桥",
                "pursuit_fire":clock(obs,"火力")}
        finally:finish(p)
    assert observations["货棚"]["pursuit_fire"]==observations["栈桥"]["pursuit_fire"]
    return {"id":"R90","scene":"实验·枪口认路",
      "two_decision_branches":observations,
      "invariant":"Official Scheme correctly redirects aim to last route and pier sacrifices evidence.",
      "warning":"A functional reaction is not proof of a rational reversal. R85 mathematical tracking-vs-static effect remains near zero; do not promote to Debug without stronger option-value proof."}

def main():
    result={"evidence":"real SSNoir.ContentValidator session actions; seed19/growth1",
      "scope":"Native semantic probes, not scripted optimality or human play",
      "results":[r88(),r90()]}
    content=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(content,encoding="utf8")
    else:print(content)
if __name__=="__main__":main()
