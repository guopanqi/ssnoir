#!/usr/bin/env python3
"""Govern the 20-mode design shortlist separately from the Unity Debug UI.

A mathematical idea, an authored native scene, a screened design candidate
and a HUMAN PLAYTEST-APPROVED debug encounter are different quality levels.
The candidate inventory is a review artifact, never an input to Debug.
"""
from pathlib import Path
import json

ROOT=Path(__file__).resolve().parents[3]
PORT=ROOT/"docs/实验/机制研究/结构候选20.json"
APPROVAL=ROOT/"UnityClient/Assets/Resources/DebugResearchPlaytests.json"

def main():
    data=json.loads(PORT.read_text(encoding="utf-8"))
    items=data["items"]
    assert len(items)==data["target"]==20
    ids=[r["id"] for r in items]
    groups=[r["family"] for r in items]
    assert len(ids)==len(set(ids))
    assert len(groups)==len(set(groups)), "A second title for the same abstract relation should not count twice"
    approved_names=set()
    for r in items:
        assert r["state"]=="structural-candidate",r["id"]
        for key in ("name","family","decision","reversalHypothesis","knownCounterexample","source"):
            assert r[key] and len(r[key])>=4,(r["id"],key)
        assert (ROOT/r["source"]).is_file(),(r["id"],r["source"])
        assert r["source"].endswith(".scm")
        if r["playtestApproval"]:
            approved_names.add(Path(r["source"]).stem)
    manifest=json.loads(APPROVAL.read_text(encoding="utf-8"))
    approved_scene_names={x["scene"] for x in manifest["encounters"]}
    assert approved_names==approved_scene_names, (
        f"Portfolio to Debug approval mismatch: {approved_names ^ approved_scene_names}")
    assert len(approved_names)<=6, "Twenty promising candidates do not imply 20 Unity Debug entries"
    print(json.dumps({"status":"PASS","reviewed_structural_candidates":len(items),
        "unique_causal_families":len(groups),"debug_approved_only":sorted(approved_names),
        "other_candidates_kept_out_of_debug":len(items)-len(approved_names)},ensure_ascii=False,indent=2))
if __name__=="__main__":main()
