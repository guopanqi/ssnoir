#!/usr/bin/env python3
"""S3-Y: reproduce the original-world 'she has left' / active-die discrepancy.

No item or stage fixtures, no Eval. After natural West clue reveal, spend
Nightingale's own *legal* action die in the card labeled '自己走东侧那条回廊'.
This is a narrow C#+Scheme behavioral test, not a new gameplay feature.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run

def actor(data,name):
    return next(x for x in data if x.get("Name")==name)

def main():
    d=run(spend_counter=False,stop_after_west_identity=True,
          execute_nightingale_solo=True)
    outcome=d["nightingale_solo_execution"]
    assert outcome and outcome["legal_original_engine_action_executed"]
    op=outcome["actual_operation"]
    assert "夜莺" in op["Label"] and "自己走东侧那条回廊" in op["Card"]
    before=actor(outcome["actors_before"],"夜莺")
    after=actor(outcome["actors_after"],"夜莺")
    assert before["Dice"],before
    assert len(after["Dice"])<len(before["Dice"]),(before,after)
    assert outcome["world_day_before"]==outcome["world_day_after"]
    d.setdefault("research",{})["nightingale_solo_executed"]=True
    d["native_check"]={
      "before_nightingale_dice":before["Dice"],
      "after_nightingale_dice":after["Dice"],
      "action_label":op["Label"],
      "same_world_day":True,
      "narrative_guard":"The card says '她已经走了' while actor still exists and was legally used.",
      "limitation":"No human testing; only one natural seed/growth path and actual accepted operation"}
    out=json.dumps(d,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(out,encoding="utf-8")
    else:print(out)
if __name__=="__main__":main()
