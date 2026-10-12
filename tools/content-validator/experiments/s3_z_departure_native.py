#!/usr/bin/env python3
"""S3-Z native departure boundary, original legal full-world run.

This runs the normal world to Ryan's identity reveal then one real end-turn,
not any direct story flag or companion mutation.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run

def names(actor_list):
    return [a.get("Name") for a in actor_list]

def main():
    d=run(spend_counter=False,stop_after_west_identity=True,end_day_after_west=True)
    assert d["status"]=="original_world_after_west_identity_before_day_end"
    e=d["next_day_result"]
    assert e is not None
    assert '夜莺' in names(e["actors_before"])
    assert e["world_day_after"]==e["world_day_before"]+1
    d["native_end_day_check"]={
      "day_before":e["world_day_before"],
      "day_after":e["world_day_after"],
      "actor_names_before":names(e["actors_before"]),
      "actor_names_after":names(e["actors_after"]),
      "nightingale_left_after_day_end":'夜莺' not in names(e["actors_after"]),
      "body":"Same original world session after West reveal, one public end-turn; no chapter/item fixture"
    }
    output=json.dumps(d,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(output,encoding="utf-8")
    else:print(output)
if __name__=="__main__":main()
