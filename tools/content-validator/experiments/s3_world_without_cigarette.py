#!/usr/bin/env python3
"""S3-X no-item natural-world negative control.

Start world from fresh seed, no --item/forced stage. Deliberately decline
the initial chase using the ORIGINAL public end-turn command; investigate
west side normally. Failure is recorded rather than worked around.
Not equivalent to a random bad roll, a real human run, or a chance estimate.
"""
import json,sys
from pathlib import Path
from s3_world_full_item_conservation import run
def main():
    x=run(spend_counter=True,let_chase_escape=True)
    x["study"]="S3-X no cigarette in original world, legal chase-decline negative control"
    x["limits"]=[
        "No forced item or story-stage mutation",
        "Player could instead lose the chase through bad results, not estimated here",
        "Original Lyon other action card existence does not certify identical success ending",
        "Author knew world story progression while scripting public actions"]
    output=json.dumps(x,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(output,encoding="utf8")
    else:print(output)
if __name__=="__main__":main()
