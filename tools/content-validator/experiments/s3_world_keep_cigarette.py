#!/usr/bin/env python3
"""S3-X same-world keep-the-golden-cigarette counterfactual.

Original world and original story, no --item, Eval, stage mutation or debug.
This is an AUTHOR-DIRECTED path to exercise a reachable branch, not blind
player behavior and not an identical RNG counterfactual: different actions
consume randomness differently even when the initial seed is the same.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run

def main():
    x=run(spend_counter=False)
    x["study"]="S3-X original world keep cigarette -> Lyon; early-spend opposite path already in S3-W"
    x["limits"]=["no fixture item or altered world stage",
        "author knew the scripted path; this is a technical reachability check, not human choice",
        "same seed with a different set of actions does not guarantee paired random draws",
        "an early cigarette shortcut and a later confrontation advance different clocks and narrative states"]
    text=json.dumps(x,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(text,encoding="utf8")
    else:print(text)
if __name__=="__main__":main()
