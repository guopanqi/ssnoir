#!/usr/bin/env python3
"""Native C# / Scheme first-turn admission screening for the structural 20.

This does NOT grant game Debug menu access or assert human fun. It proves
the scene loads in the official runner and presents a nonempty choice set.
"""
import json
import subprocess
import sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
PORT=ROOT/"docs/实验/机制研究/结构候选20.json"
CMD=["dotnet","run","--no-build","--project",
     "tools/content-validator/SSNoir.ContentValidator.csproj","--"]

def check(item):
    name=Path(item["source"]).stem
    proc=subprocess.run(CMD+["--session","encounters/"+name,"--seed","19","--growth","1"],
                        cwd=ROOT,input='{"command":"quit"}\n',encoding="utf-8",
                        capture_output=True,timeout=35)
    lines=proc.stdout.splitlines()
    if proc.returncode!=0 or not lines:
        raise RuntimeError(f"{item['id']} native failure exit={proc.returncode}: "+proc.stderr[-800:])
    opening=json.loads(lines[0])
    if opening.get("type")=="error" or not opening.get("observation"):
        raise AssertionError(f"{item['id']} did not open: {opening}")
    obs=opening["observation"]
    options=obs.get("Operations") or []
    cards={str(op.get("Card") or op.get("Kind")) for op in options}
    if len(options)<2 or len(cards)<2:
        raise AssertionError(f"{item['id']} first turn lacks two distinct actions: {sorted(cards)}")
    if obs.get("EncounterResult") is not None:
        raise AssertionError(f"{item['id']} completed before player decision")
    return {"id":item["id"],"scene":name,"first_turn_operations":len(options),
            "distinct_action_categories":len(cards),"native_start":"PASS"}

def main():
    cfg=json.loads(PORT.read_text(encoding="utf8"))
    results=[]
    for item in cfg["items"]:
        row=check(item);results.append(row)
        print(f"{row['id']}: {row['scene']} {row['first_turn_operations']} options",
              flush=True)
    assert len(results)==20
    payload={"schema":"ssnoir.native-candidate-admission/v1",
            "result":"20 native encounter sessions opened and presented at least two distinct actions",
            "boundary":"Start-state technical smoke only, NOT human playtest or strategy-quality proof",
            "results":results}
    output=Path(sys.argv[1]) if len(sys.argv)>1 else None
    if output:output.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    else:print(json.dumps(payload,ensure_ascii=False,indent=2))
if __name__=="__main__":main()
