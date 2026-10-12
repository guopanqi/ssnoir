#!/usr/bin/env python3
"""Actual unmodified world from fresh save -> West identity before day-end.

Reuses original S3-X 65-step traversal. Stop at a different observation,
not a Scheme Eval or hand-injected flag. Tests player-visible cards; doesn't
declare a timing bug solely because a static guard might permit it.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run

def main():
    d=run(spend_counter=False,stop_after_west_identity=True)
    assert d["status"]=="original_world_after_west_identity_before_day_end",d.get("obstacles")
    assert d["setting"]["item_fixture"] is False
    assert len(d["native_steps"])>20
    available={x["card"] for x in d["operations"] if x["kind"]=="action"}
    visible={x["name"] for x in d["cards"]}
    # World renderer may expose the new card even when no dice remain;
    # card visible and executable are distinct levels of evidence.
    d["research"]={
        "found_solo_frank_card":"自己走东侧那条回廊" in visible,
        "solo_frank_action_legal":"自己走东侧那条回廊" in available,
        "guided_east_card_still_visible":bool({"带她穿过东侧门廊","带她走东侧回廊"} & visible),
        "subtitle_with_she_has_left":any("她已经走了" in str(c.get("subtitle","")) for c in d["cards"]),
        "companion_property_in_snapshot":list(d["companion_fields"]),
        "actor_state_at_reveal":d["companion_fields"].get("Actors"),
        "solo_frank_legal_operation_details":d.get("solo_frank_legal_operations",[])[:10],
        "scope":"actual snapshot at identity reveal, before end-turn; cannot infer human interpretation or next-day state"}
    d["nonclaims"]=["No fixture item or story-stage skip",
                     "The player's subjective timeline is not tested",
                     "Card existence need not imply a legal action when dice are empty"]
    out=json.dumps(d,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(out,encoding="utf-8")
    else:print(out)
if __name__=="__main__":main()
