#!/usr/bin/env python3
"""S3-AA original-world day-after companion departure: legitimate Neil solo action.

No --item, no Eval, no story flags/companion injection; full first chapter replay.
The S3-Z evidence already tests day9 'solo' using Nightingale's die.
This tests the *different*, day10 path after the official end-turn dismissal.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run

def get_actor(actors,name):
    return next(a for a in actors if a.get("Name")==name)

def main():
    d=run(spend_counter=False,stop_after_west_identity=True,
          end_day_after_west=True,probe_next_day_solo=True)
    assert d["setting"]["item_fixture"] is False
    e=d["next_day_result"]
    assert e["world_day_before"]==9
    assert e["world_day_after"]==10
    assert any(a.get("Name")=="夜莺" for a in e["actors_before"])
    assert not any(a.get("Name")=="夜莺" for a in e["actors_after"])
    n=e["solo_next_day"]
    op=n["legal_original_niil_operation"]
    assert "尼尔" in op["Label"] and op["Card"]=="自己走东侧那条回廊"
    assert n["world_day_before"]==n["world_day_after"]==10
    assert not any(a.get("Name")=="夜莺" for a in n["after_actors"])
    before=get_actor(n["before_actors"],"尼尔")
    after=get_actor(n["after_actors"],"尼尔")
    assert len(after.get("Dice",[]))<len(before.get("Dice",[])),(before,after)
    d["aa_evidence"]={
        "day9_reveal_then_public_end_turn":True,
        "day10_original_legal_card":op["Card"],
        "day10_actual_actor":"尼尔",
        "day10_actor_list":[a.get("Name") for a in n["before_actors"]],
        "before_neil_dice":before.get("Dice",[]),
        "after_neil_dice":after.get("Dice",[]),
        "same_day_action":True,
        "scope":"one official seed19/growth4 path, not a human playtest, not a strategy win-rate"
    }
    output=json.dumps(d,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(output,encoding="utf-8")
    else:print(output)

if __name__=="__main__":
    main()
