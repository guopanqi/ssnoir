#!/usr/bin/env python3
"""Validate manual Debug menu allowlists, without launching Unity.

Debug UI is a deliberate *subset* of encounter scripts. Experiment creation
must never implicitly promote a scene. Both menus consume one C# catalog.
"""
from pathlib import Path
import json,re

ROOT=Path(__file__).resolve().parents[3]
RES=ROOT/"UnityClient/Assets/Resources"
ENCOUNTERS=RES/"Content/scenes/encounters"
SRC=ROOT/"UnityClient/Assets/Scripts/Runtime/IMGUI/Components"
STORY=RES/"DemoEncounters.json"
RESEARCH=RES/"DebugResearchPlaytests.json"
CATALOG=SRC/"DebugEncounterCatalog.cs"

def main():
    a=json.loads(STORY.read_text(encoding="utf-8"))["encounters"]
    b=json.loads(RESEARCH.read_text(encoding="utf-8"))["encounters"]
    assert len(a)>=4, "Keep narrative encounter debug access"
    assert 1<=len(b)<=6, "Selected human playtests should be a small shortlist"
    scene_names=[x["scene"] for x in a+b]
    assert len(scene_names)==len(set(scene_names)), "Duplicate scene in whitelist"
    for item in a:
        assert not item["scene"].startswith(("研究·","实验·")), "Research scenes cannot bypass the review manifest"
        assert item.get("label") and item["scene"] and (ENCOUNTERS/(item["scene"]+".scm")).is_file()
    for item in b:
        assert item["scene"].startswith("研究·"), "Curated research must stay in the reviewed category"
        assert item.get("approval")=="human-playtest-requested" and item.get("reason")
        assert (ENCOUNTERS/(item["scene"]+".scm")).is_file(),item["scene"]
        evidence=ROOT/item["evidence"]
        assert evidence.is_file(),f"No human-facing evidence for {item['scene']}: {evidence}"
    catalog=CATALOG.read_text(encoding="utf-8")
    assert 'Append("DemoEncounters"' in catalog
    assert 'Append("DebugResearchPlaytests"' in catalog
    assert 'Resources.Load<TextAsset>("Content/scenes/encounters/" + scene)' in catalog
    for name in ("DebugPanelDrawer.cs","SceneDropdownDrawer.cs"):
        s=(SRC/name).read_text(encoding="utf-8")
        assert "DebugEncounterCatalog.Load()" in s
        assert 'Resources.LoadAll<TextAsset>("Content/scenes/encounters")' not in s
    all_research=list(ENCOUNTERS.glob("研究·*.scm"))
    included=sum(name.startswith("研究·") for name in scene_names)
    excluded=len(all_research)-included
    assert excluded>20, "Do not accidentally expose entire research corpus"
    assert "研究·动作回声" not in scene_names, "Rejected baseline should not remain in playtest menu"
    print(json.dumps({"status":"PASS","narrative_debug":len(a),"curated_research_debug":len(b),
                      "research_hidden":excluded,"research_total":len(all_research),
                      "preview_names":scene_names},ensure_ascii=False,indent=2))
if __name__=="__main__":main()
