#!/usr/bin/env python3
"""从结构化结论唯一源生成离线可读的 SSNoir 研究总览；--check 检查同步。"""
import argparse
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"docs/实验/机制研究"
DATA=BASE/"模式库.json"
TEMPLATE=BASE/"研究总览.template.html"
OUTPUT=BASE/"研究总览.html"

def build():
    data=json.loads(DATA.read_text(encoding="utf-8"))
    assert data["version"]==1
    ids=[p["id"] for p in data["patterns"]]
    assert len(ids)==len(set(ids)), "duplicate pattern id"
    assert 1 <= len(ids) <= 12, "精选结构不可当作无上限的实验日志"
    for p in data["patterns"]:
        for key in ("id","name","family","focus","decision","principle","steps","dynamic",
                    "why","counter","boundary","next","evidence","evidenceNote","links","potential"):
            assert key in p and p[key], f"{p.get('id')} missing {key}"
        assert p["evidence"]["math"] or p["evidence"]["native"] or p["evidence"]["human"]
        for link in p["links"]:
            assert link["label"] and link["href"]
            if not link["href"].startswith(("https://","http://","#")):
                assert (BASE/link["href"]).exists(), f"Broken file link: {link['href']}"
    for ref in data["references"]:
        if not ref["href"].startswith(("https://","http://","#")):
            assert (BASE/ref["href"]).exists(), ref["href"]
    template=TEMPLATE.read_text(encoding="utf-8")
    assert template.count("/*ATLAS_DATA*/")==1
    payload=json.dumps(data,ensure_ascii=False,separators=(",",":"))
    assert "</script" not in payload.lower()
    return template.replace("/*ATLAS_DATA*/",payload)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--check",action="store_true")
    args=parser.parse_args()
    expected=build()
    if args.check:
        assert OUTPUT.exists() and OUTPUT.read_text(encoding="utf-8")==expected, (
            "研究总览.html 未与模式库同步；运行 python3 tools/content-validator/experiments/build_mechanism_atlas.py"
        )
        print("Atlas synchronized; structural fields, evidence and links validated.")
    else:
        OUTPUT.write_text(expected,encoding="utf-8")
        print("Rendered",OUTPUT)
if __name__=="__main__":
    main()
