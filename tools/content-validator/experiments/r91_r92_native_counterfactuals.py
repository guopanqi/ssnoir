#!/usr/bin/env python3
"""R91/R92: native counterfactuals from existing paired research scenes.

R91 toggles ONLY partial-progress cleanup at round boundary.
R92 toggles ONLY nonlinear versus linear payout for an identical reserve.
Neither test is a human choice study; both challenge assumptions about
what the extra rule actually changes.
"""
import json,sys
from probe_four_scenario_causality import start,recv,clock,finish

def pick(p,obs,card):
    options=[x for x in obs["Operations"] if x.get("Card")==card]
    assert options,(card,[x.get("Card") for x in obs["Operations"]])
    selected=max(options,key=lambda x:(x.get("DieValue") or 0,str(x.get("Id"))))
    cmd={"command":"act","version":obs["Version"],"operationId":selected["Id"],
         "reason":"同一骰面下原生因果消融"}
    p.stdin.write(json.dumps(cmd,ensure_ascii=False)+"\n");p.stdin.flush()
    return recv(p)
def end(p,obs):
    options=[x for x in obs["Operations"] if x.get("Kind")=="end-turn"]
    assert options,"No end-turn available"
    x=options[0];cmd={"command":"act","version":obs["Version"],
         "operationId":x["Id"],"reason":"验证公开回合截止规则"}
    p.stdin.write(json.dumps(cmd,ensure_ascii=False)+"\n");p.stdin.flush()
    return recv(p)
def r91():
    rows=[]
    for scene,clear in (("整段完成",True),("分段保留对照",False)):
        # research scene (not an authored '实验·' encounter).
        from subprocess import Popen,PIPE
        from probe_four_scenario_causality import CMD,ROOT
        p=Popen(CMD+["--session","encounters/研究·"+scene,"--seed","19","--growth","1"],
                cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
        try:
            obs=recv(p)
            assert clock(obs,"目标A")==0
            obs=pick(p,obs,"快做A")
            before=clock(obs,"目标A")
            assert 0<before<5,(scene,before)
            obs=end(p,obs)
            after=clock(obs,"目标A")
            assert after==(0 if clear else before),(scene,before,after)
            rows.append({"scene":scene,"clear_unfinished":clear,
                         "before_round_end":before,"after_round_end":after})
        finally:finish(p)
    assert rows[0]["before_round_end"]==rows[1]["before_round_end"]
    return {"id":"R91","comparison":rows,
            "conclusion":"The per-round cleanup rule has a real, independent native effect on unfinished effort. No evidence yet that this leads to better player decisions."}
def r92():
    from subprocess import Popen,PIPE
    from probe_four_scenario_causality import CMD,ROOT
    def run(seed,scene,nonlinear):
        p=Popen(CMD+["--session","encounters/研究·"+scene,"--seed",str(seed),"--growth","1"],
                cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
        try:
            obs=recv(p)
            for _ in range(2):
                obs=pick(p,obs,"备料")
            reserve=clock(obs,"储备")
            if reserve<=0:return None
            before=clock(obs,"目标")
            obs=pick(p,obs,"兑现")
            after=clock(obs,"目标")
            assert clock(obs,"储备")==0
            expected=reserve*(reserve+1)//2 if nonlinear else 2*reserve
            assert after-before==expected,(scene,seed,reserve,after-before,expected)
            return {"scene":scene,"nonlinear":nonlinear,"seed":seed,
                    "reserve":reserve,"payout":after-before}
        finally:finish(p)
    # A payoff curve cannot be credited to the candidate based on one root.
    # Find reproducible native examples on both sides of k=3.
    examples={}
    for seed in [19]+list(range(1,33)):
        a=run(seed,"分段兑现",True)
        if a is None:continue
        tier="low" if a["reserve"]<3 else "high" if a["reserve"]>3 else "equal"
        if tier in examples:continue
        b=run(seed,"兑现线性",False)
        assert b and a["reserve"]==b["reserve"],(seed,a,b)
        examples[tier]={"nonlinear":a,"linear":b,"delta":a["payout"]-b["payout"]}
        if all(k in examples for k in ("low","equal","high")):break
    assert all(k in examples for k in ("low","equal","high")),examples
    assert examples["low"]["delta"]<0
    assert examples["equal"]["delta"]==0
    assert examples["high"]["delta"]>0
    return {"id":"R92","three_reachable_native_cases":examples,
      "conclusion":"Nonlinearity has a native CATEGORICAL consequence only across reserve thresholds: below3 linear wins, at3 ties, above3 nonlinear wins. Existence of a reward crossover is not yet evidence that a player voluntarily changes when to cash out."}
def main():
    results=[r91(),r92()]
    text=json.dumps({"scope":"Native C# Scheme seeded intervention; not player preference",
                     "results":results},ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(text,encoding="utf-8")
    else:print(text)
if __name__=="__main__":main()
