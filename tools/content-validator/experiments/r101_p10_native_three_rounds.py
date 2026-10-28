#!/usr/bin/env python3
"""R101: native P10 three-round, four-die-per-round *technical* trial.

Runs actual C# + Scheme research "分段兑现", unchanged. Compare deterministic
public threshold scripts (not human players). The paper R98/R99 4-die
two-round toy is NOT substituted for this 3-round native session.
Identical random seeds do not imply identical post-branch random results.
"""
import json
import sys
from subprocess import PIPE,Popen
from probe_four_scenario_causality import ROOT,CMD,recv,finish,clock

SCENE="scenarios/研究·分段兑现"

def send(p,obs,op,reason):
    command={"command":"act","version":obs["Version"],
             "operationId":op["Id"],"reason":reason}
    p.stdin.write(json.dumps(command,ensure_ascii=False)+"\n")
    p.stdin.flush()
    return recv(p)

def run(seed,k):
    p=Popen(CMD+["--session",SCENE,"--seed",str(seed),"--growth","1"],
            cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
    rows=[]
    try:
        obs=recv(p)
        assert clock(obs,"目标")==0
        assert clock(obs,"储备")==0
        assert clock(obs,"已过回合")==0
        for step in range(35):
            if obs.get("ScenarioResult") is not None:
                return {"seed":seed,"threshold":k,"actions":rows,
                        "ending":obs["ScenarioResult"],
                        "status":"ended","action_count":sum(x["card"]!="end-turn" for x in rows),
                        "round_boundaries":sum(x["card"]=="end-turn" for x in rows)}
            ops=obs["Operations"]
            available={x.get("Card") or x.get("Kind") for x in ops}
            before={name:clock(obs,name) for name in ("目标","储备","已过回合")}
            active_dice=[x for x in ops if x.get("Card")=="备料"]
            if not active_dice:
                card="end-turn"
                group=[x for x in ops if x.get("Kind")=="end-turn"]
            elif before["目标"]==17 and "稳进" in available:
                card="稳进";group=[x for x in ops if x.get("Card")==card]
            elif before["储备"]>0 and (before["目标"]+before["储备"]*(before["储备"]+1)//2>=18
                                        or before["储备"]>=k or len(active_dice)==1):
                card="兑现";group=[x for x in ops if x.get("Card")==card]
            else:
                card="备料";group=active_dice
            assert group,(seed,k,step,available,before)
            # Every scripted decision observes public resources and the dice;
            # choose the strongest remaining die for rolled preparation, and
            # weakest for deterministic cash/guaranteed progress.
            if card=="备料":
                chosen=max(group,key=lambda x:(x.get("DieValue") or 0,str(x.get("Id"))))
            else:
                chosen=min(group,key=lambda x:(x.get("DieValue") or 0,str(x.get("Id"))))
            next_obs=send(p,obs,chosen,"R101 scripted threshold native session; not authored or human play")
            after=None
            if next_obs.get("ScenarioResult") is None:
                after={name:clock(next_obs,name) for name in ("目标","储备","已过回合")}
                if card=="end-turn":
                    assert after["已过回合"]==before["已过回合"]+1
                    assert after["储备"]==max(0,before["储备"]-2)
                if card=="兑现":
                    assert after["储备"]==0
                    assert after["目标"]-before["目标"]==min(
                        18-before["目标"],before["储备"]*(before["储备"]+1)//2)
            rows.append({"card":card,"die":chosen.get("DieValue"),"before":before,
                         "after":after,"terminal":next_obs.get("ScenarioResult") is not None})
            obs=next_obs
        raise AssertionError(f"R101 native session exceeded 35 operations for seed={seed}, threshold={k}")
    finally:
        finish(p)

def main():
    # Six actual native sessions; keeps runtime bounded. These are NOT six
    # independent blinded player assessments.
    results=[run(seed,k) for seed in (7,19,31) for k in (2,4)]
    assert len(results)==6
    assert all(r["action_count"]<=12 for r in results),results
    assert all(r["status"]=="ended" for r in results)
    assert any(r["round_boundaries"]>=1 for r in results)
    result={"id":"R101","scope":"6 actual C# + Scheme sessions; 3-round source with target18, reserve cap5 and decay2. Technical scripted policies, NOT human feedback.",
            "policies":"cash as soon as r>=2 vs r>=4, or if redeem completes target / only one die remains; otherwise prepare, low die for guaranteed actions.",
            "warning":"After different actions, same seed's RNG streams can diverge. Do not attribute cross-policy outcome difference solely to cash threshold.",
            "sessions":results}
    s=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding="utf8")
    else:print(s)
if __name__=="__main__":main()
