#!/usr/bin/env python3
"""Non-exclusive pattern map integrity; not a gameplay quality evaluator."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"experiments/抽象决策关系研究"

def main():
    data=json.loads((BASE/"模式语言知识地图.json").read_text(encoding="utf-8"))
    assert data["schema"]=="ssnoir.pattern-language-map/v1"
    nodes=data["nodes"]
    ids=[n["id"] for n in nodes]
    assert len(ids)==len(set(ids)), "duplicate ids"
    names=set(ids)
    for n in nodes:
        assert n["name"] and n["layer"] and n["stage"] and n["sources"]
        for path in n["sources"]:
            assert (ROOT/path).is_file(),(n["id"],path)
    for e in data["edges"]:
        assert e["from"] in names and e["to"] in names
        assert e["relation"] and e["reason"]
    legacy=json.loads((BASE/"结构候选20.json").read_text(encoding="utf-8"))
    assert {v["id"] for v in legacy["items"]}<=names
    display=json.loads((BASE/"模式库.json").read_text(encoding="utf-8"))
    assert len([v for v in nodes if v.get("curatedDisplay")])==len(display["patterns"])
    assert len([v for v in nodes if v["id"].startswith("CS2-")])==9
    print(json.dumps({"status":"PASS","nodes":len(nodes),"edges":len(data["edges"])},ensure_ascii=False))

if __name__=="__main__": main()
