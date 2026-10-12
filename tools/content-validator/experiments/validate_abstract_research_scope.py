#!/usr/bin/env python3
"""SSNoir abstract research focus regression guard (2026-10-12).

Only checks the CURRENT default research entrypoints. Historical S3 and R
experiments remain intentionally untouched, with their entire previous
frontier archived. This checker must not treat narrative concepts as new
mechanisms merely because a branch has mathematical evidence.
"""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"docs/实验/机制研究"
SKILL=ROOT/"skills/research-mechanisms/SKILL.md"
ARCH=BASE/"归档/2026-10-12-方向校准前"

def read(path):return path.read_text(encoding="utf-8")

def main():
    for name in ("README.md","研究定位与评价.md","研究队列.md",
                 "当前状态.md","研究约定.md","实验结构与维护契约.md","抽象探索地图-20261012校准.md"):
        assert (BASE/name).is_file(),name
    for name in ("README.md","当前状态.md","研究队列.md",
                 "SKILL-旧版.md","自主研究记忆.json",
                 "模式库.json","试玩任务.json","研究约定.md"):
        assert (ARCH/name).is_file(),"historical evidence must not be discarded: "+name

    skill=read(SKILL);queue=read(BASE/"研究队列.md")
    status=read(BASE/"当前状态.md");intro=read(BASE/"README.md")
    assert 500<len(skill)<9000 and 400<len(queue)<7000 and 300<len(status)<5000
    for name,text in (("skill",skill),("queue",queue),("status",status),("readme",intro)):
        assert "抽象" in text and "决策" in text,name
    assert "唯一默认" in skill and "去情景" in queue
    assert "Skill" in read(BASE/"实验结构与维护契约.md")
    for old_path in (ARCH/"当前状态.md",ARCH/"研究队列.md"):
        assert len(read(old_path))>len(queue)*2

    memo=json.loads(read(BASE/"自主研究记忆.json"))
    old=json.loads(read(ARCH/"自主研究记忆.json"))
    assert memo["schema"]==old["schema"]
    assert isinstance(memo.get("roundsSinceCalibration"),int) and memo["roundsSinceCalibration"]>=0
    assert memo["totalArchivedRounds"]==old["totalArchivedRounds"]+memo["roundsSinceCalibration"],(
       "rounds cannot change without explicit post-calibration research count")
    assert len(memo["frontier"])>=3
    assert len(read(BASE/"自主研究记忆.json"))<len(read(ARCH/"自主研究记忆.json"))/5
    frontier=memo["frontier"]
    assert len({entry["id"] for entry in frontier})==len(frontier)
    for entry in frontier:
        assert all(entry.get(key) for key in ("id","family","question"))
        assert not any(k in (entry["id"]+" "+entry["question"]) for k in ("S3-","夜莺","香烟","莱恩","下一个NPC"))
    assert len(memo["history"])<=3,"compact past batches into a durable grouped summary"
    for entry in memo["history"][1:]:
        records=entry.get("records") or ([entry["record"]] if "record" in entry else [])
        assert records and entry["rounds"]>0,"real experiments need archived evidence"
        assert all((ROOT/path).is_file() for path in records),"archived batch records must exist"

    library=json.loads(read(BASE/"模式库.json"))
    tasks=json.loads(read(BASE/"试玩任务.json"))
    before_library=json.loads(read(ARCH/"模式库.json"))
    before_tasks=json.loads(read(ARCH/"试玩任务.json"))
    assert [p["id"] for p in library["patterns"]]==[p["id"] for p in before_library["patterns"]]
    assert [t["id"] for t in tasks["tasks"]]==[t["id"] for t in before_tasks["tasks"]]
    assert len(library["patterns"])==6
    assert "抽象" in library["summary"] and "抽象" in tasks["headline"]
    for text in (library["summary"],tasks["headline"],*tasks["current"]):
        assert not any(s in text for s in ("夜莺","莱恩","烟盒","S3-X","S3-Z")),(
            "narrative follow-up leaked into active workbench",text)
    assert all(x["id"] not in ("R149","R157","R166") for x in memo["frontier"])
    print(json.dumps({
       "status":"PASS",
       "scope":"abstract-decision-patterns",
       "legacy_memory_archived":True,
       "past_rounds_preserved":memo["totalArchivedRounds"],
       "selected_patterns_unchanged":len(library["patterns"]),
       "human_playtest_tasks_unchanged":len(tasks["tasks"]),
       "live_frontiers":len(memo["frontier"]),
       "active_documents_compacted":True
    },ensure_ascii=False,indent=2))

if __name__=="__main__":main()
