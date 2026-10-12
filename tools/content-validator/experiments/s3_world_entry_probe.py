#!/usr/bin/env python3
"""Bounded ordinary original-world entrance audit, without story-stage spoof.

Just observes legal world moves and tries to begin the first detective
commission in a normal initial save. Outputs an INCOMPLETE trace if no
robust route, never calls arbitrary Scheme expression or debug shortcut.
"""
import json,sys
from pathlib import Path
from subprocess import Popen,PIPE
from probe_four_scenario_causality import CMD,ROOT,recv,finish
ENTRY="world"
def step(p,o,chosen):
    p.stdin.write(json.dumps({"command":"act","version":o["Version"],
      "operationId":chosen["Id"],"reason":"S3-W世界第一章自然入口探针"},ensure_ascii=False)+"\n")
    p.stdin.flush()
    return recv(p)
def brief(o):
    return {"scene":o["Scene"],"focus":o["Focus"],"day":o["WorldDay"],
      "item":o["Inventory"].get("半包「老金牌」香烟",0),
      "operations":[{"kind":x["Kind"],"card":x.get("Card"),"label":x["Label"]} for x in o["Operations"][:35]],
      "dossier":o["Dossier"][:3],
      "blockers":o["RestBlockers"]}
def audit():
    p=Popen(CMD+["--session",ENTRY,"--seed","19","--growth","4"],cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
    o=recv(p);trace=[{"t":0,"observation":brief(o)}];status="ordinary_initial"
    try:
        for i in range(18):
            ops=o["Operations"]
            acts=[x for x in ops if x["Kind"]=="action"]
            commission=next((x for x in acts if "她" in (x.get("Card") or "") or "开门" in (x.get("Card") or "") or "接" in (x.get("Card") or "")),None)
            if commission:
                chosen=commission
            elif o["WorldDay"]<=3:
                # No story-forcing: rest until the visitor shows up, look at home only.
                chosen=next((x for x in ops if x["Kind"]=="enter-place" and x.get("Card")=="家"),None) \
                  or next((x for x in ops if x["Kind"]=="end-turn"),None)
            else:
                status="ordinary_progression_requires_authored_choices"
                break
            if chosen is None:
                status="no_matching_legal_action";break
            o=step(p,o,chosen)
            trace.append({"t":i+1,"chosen":{"kind":chosen["Kind"],"label":chosen["Label"]},
                   "observation":brief(o)})
            if any("勒索" in text for text in o["Dossier"]):
                status="commission_dossier_visible"
                break
        return {"source":"unmodified original world starting at new game",
          "initial_fixture":{"seed":19,"growth":4,"items":{}},
          "status":status,"steps":trace,
          "not_claimed":"This is not a full cigarette acquisition or stage2-to-stage3 run."}
    finally:finish(p)
def main():
    result=audit()
    payload=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(payload,encoding="utf8")
    else:print(payload)
if __name__=="__main__":main()
