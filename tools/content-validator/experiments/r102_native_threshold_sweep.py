#!/usr/bin/env python3
"""R102: stress the R99 'threshold 2 or 3' claim in actual native P10.

R99's two-policy exact envelope was for a 4-die / 2-round toy model.
R102 executes three fixed threshold scripts under the existing official
3-round/4-dice-per-round C#+Scheme encounter; 12 deterministic seeds.
This is a bounded scripted *portability check*, not a strategic optimizer or
human play study. Action-dependent RNG consumption means even equal seeds
do not create identical counterfactual dice.
"""
import json,sys
from r101_p10_native_three_rounds import run

SEEDS=(3,5,7,11,13,17,19,23,29,31,37,41)
THRESHOLDS=(2,3,4)

def grade(value):
    text=str(value)
    if "'success" in text:return "success"
    if "'timeout" in text:return "timeout"
    return "other"

def main():
    sessions=[]
    for seed in SEEDS:
        for k in THRESHOLDS:
            r=run(seed,k)
            r["ending_class"]=grade(r["ending"])
            assert r["ending_class"] in ("success","timeout","other")
            sessions.append({k:v for k,v in r.items() if k!="actions"})
            print(seed,k,r["ending_class"],r["action_count"],r["round_boundaries"],flush=True)
    summary={}
    for k in THRESHOLDS:
        rows=[r for r in sessions if r["threshold"]==k]
        summary[str(k)]=dict(sessions=len(rows),
            success=sum(x["ending_class"]=="success" for x in rows),
            timeout=sum(x["ending_class"]=="timeout" for x in rows),
            other=sum(x["ending_class"]=="other" for x in rows),
            avg_actions=sum(x["action_count"] for x in rows)/len(rows))
    result=dict(id="R102",scope="36 real native C#+Scheme scripted sessions; nonrandom limited seed set; NOT causal win rates or humans",
      hypothesis="R99 two-policy K2/K3 claim will not automatically transfer to a native three-round scheme with 12 possible actions, composure and more event outcomes",
      seed_semantics="Same integer seeds, divergent RNG draw order after different actions",
      summaries=summary,sessions=sessions,
      no_debug_promotion=True)
    s=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding="utf8")
    else:print(s)
if __name__=="__main__":main()
