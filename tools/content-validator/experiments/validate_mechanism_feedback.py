#!/usr/bin/env python3
"""Validate human-authored study feedback with no mutation or format coercion."""
from __future__ import annotations

import argparse
import json
from datetime import datetime
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"docs/实验/机制研究"
CONFIG=BASE/"试玩任务.json"
INBOX=BASE/"反馈/inbox"
SCHEMA="ssnoir.mechanism-feedback/v1"
ENVIRONMENTS={"browser-sketch","official-runtime"}
RESPONSE_KEYS=("understanding","replayInterest","decisiveMoment","decisionChange",
               "mechanicalFeeling","friction","ideas")

def check(path:Path, tasks:dict)->dict:
    assert path.suffix.lower()==".json",f"Feedback must be JSON: {path}"
    assert path.stat().st_size<=204800,f"Feedback too large: {path}"
    data=json.loads(path.read_text(encoding="utf-8"))
    assert isinstance(data,dict),f"Feedback must be object: {path}"
    assert data.get("schema")==SCHEMA,f"Invalid schema in {path}"
    study=data.get("studyId")
    assert study in tasks,f"Unknown study {study!r}: {path}"
    assert data.get("environment") in ENVIRONMENTS,f"Missing feedback environment in {path}"
    assert isinstance(data.get("studyTitle"),str) and data["studyTitle"].strip()
    datetime.fromisoformat(data["createdAt"].replace("Z","+00:00"))
    source=data.get("source")
    assert isinstance(source,dict) and isinstance(source.get("studyConfigVersion"),int)
    assert source.get("officialEntry")==tasks[study]["experiment"]
    responses=data.get("responses")
    assert isinstance(responses,dict)
    for key in RESPONSE_KEYS:
        value=responses.get(key,"")
        assert isinstance(value,str) and len(value)<=12000,(key,path)
    assert responses.get("understanding","") in ("","yes","somewhat","no","not-sure")
    assert responses.get("replayInterest","") in ("","yes","maybe","no","not-sure")
    runs=data.get("runs")
    assert isinstance(runs,list) and len(runs)<=20
    if data["environment"]=="official-runtime":
        assert not runs, "Official-runtime JSON cannot include untrusted web-sketch runs"
    for run in runs:
        assert isinstance(run,dict)
        assert run.get("variant")==tasks[study].get("browserVariant")
        # Existing R54 feedback (2026-10-09) does not contain handSize.
        # Its documented four-die semantics remain unchanged.
        hand_size=run.get("handSize",4)
        assert type(hand_size) is int and hand_size in (4,5)
        if hand_size==5:
            assert study=="R54" and run["variant"]=="reactive", (
                "Only R54 browser capacity control permits five dice")
        assert isinstance(run.get("initialDice"),list) and len(run["initialDice"])==hand_size
        assert all(type(d)==int and 1<=d<=6 for d in run["initialDice"])
        assert run.get("skill") in (1,2)
        actions=run.get("actions",[])
        assert isinstance(actions,list) and len(actions)<=hand_size
        assert all(isinstance(a,dict) and a.get("target") in ("A","B") and
                   type(a.get("die"))==int and 1<=a["die"]<=6 for a in actions)
        assert run.get("result") in ("A","B","timeout",None)
        if "presentation" in run:
            assert study=="R50" and run.get("variant")=="echo"
            assert run["presentation"] in ("goal-explicit","legacy-abstract")
    notes=data.get("notes",[])
    assert isinstance(notes,list) and len(notes)<=50
    assert all(isinstance(n,str) and len(n)<12000 for n in notes)
    assert runs or notes or any(responses.get(k,"").strip() for k in RESPONSE_KEYS), (
        f"Empty feedback (no runs/notes/responses) in {path}")
    return {"file":path.name,"study":study,"environment":data["environment"],
            "runs":len(runs),"hasText":bool(notes or any(responses.get(k,"").strip() for k in RESPONSE_KEYS))}

def main():
    p=argparse.ArgumentParser()
    p.add_argument("paths",nargs="*",type=Path)
    args=p.parse_args()
    cfg=json.loads(CONFIG.read_text(encoding="utf-8"))
    studies={task["id"]:task for task in cfg["tasks"]}
    paths=args.paths or sorted(INBOX.glob("*.json"))
    inspected=[check(path,studies) for path in paths]
    print(json.dumps({"checked":len(inspected),"feedback":inspected},ensure_ascii=False,indent=2))
if __name__=="__main__":
    main()
