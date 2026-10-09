#!/usr/bin/env python3
"""Build/check the standalone human field station from two grounded JSON sources.

- 模式库.json: reviewed research conclusions (not canonical game rules)
- 试玩任务.json: explicit current human playtest queue and prompts
- 研究总览.template.html: interface and independent browser play sketches
- 研究总览.html: generated fully self-contained human-facing artifact

No network, credentials, or script execution needed to open the generated HTML.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/"docs/实验/机制研究"
ATLAS=BASE/"模式库.json"
TASKS=BASE/"试玩任务.json"
TEMPLATE=BASE/"研究总览.template.html"
CLOUD=ROOT/"tools/content-validator/experiments/mechanism_cloud_feedback.js"
WORKBENCH_CSS=ROOT/"tools/content-validator/experiments/mechanism_workbench.css"
WORKBENCH_JS=ROOT/"tools/content-validator/experiments/mechanism_workbench.js"
OUTPUT=BASE/"研究总览.html"


def local_href_exists(href:str)->bool:
    if href.startswith(("https://","http://","#")):
        return True
    return (BASE/href).exists()


def validate_library(data:dict)->None:
    assert data["version"] == 1
    patterns=data["patterns"]
    assert 1<=len(patterns)<=12, "Human-facing curated patterns should remain selective"
    ids=[p["id"] for p in patterns]
    assert len(ids)==len(set(ids)), "Duplicate pattern id"
    for p in patterns:
        for key in ("id","name","family","focus","decision","principle","steps",
                    "dynamic","why","counter","boundary","next","evidence","evidenceNote",
                    "links","potential"):
            assert key in p and p[key], f"Pattern {p.get('id')} lacks {key}"
        assert set(p["evidence"])=={"math","native","human"}
        assert all(type(p["evidence"][key]) is bool for key in p["evidence"])
        assert any(p["evidence"].values()), f"Pattern {p['id']} has no evidence"
        for link in p["links"]:
            assert link["label"] and local_href_exists(link["href"]), (
                f"Broken pattern source: {p['id']} {link['href']}")
    for ref in data["references"]:
        assert local_href_exists(ref["href"]), f"Broken research reference {ref['href']}"


def validate_tasks(data:dict,library:dict)->None:
    assert data["version"]==1
    queue=data["tasks"]
    assert 1<=len(queue)<=16
    assert [t["rank"] for t in queue]==list(range(1,len(queue)+1)), (
        "Human priorities need explicit sorted ranks")
    ids=[t["id"] for t in queue]
    assert len(ids)==len(set(ids)), "Duplicate human study id"
    selected={p["id"] for p in library["patterns"]}
    for t in queue:
        assert t["mode"] in ("browser-demo","official-only")
        if t["patternId"] is not None:
            assert t["patternId"] in selected, t["patternId"]
        for key in ("id","title","badge","stage","intro","estimatedMinutes",
                    "experiment","official","record","askBefore","observe","askAfter","purpose","officialNote"):
            assert t[key],f"Study {t.get('id')} lacks {key}"
        assert (ROOT/t["official"]).is_file(),t["official"]
        assert local_href_exists(t["record"]),t["record"]
        if t["mode"]=="browser-demo":
            assert t["browserVariant"] in ("echo","reactive")
        assert isinstance(t["observe"],list) and len(t["observe"])>=2
        assert isinstance(t["askAfter"],list) and len(t["askAfter"])>=2
    ids=[t["id"] for t in data["feedbackFields"]]
    assert len(ids)==len(set(ids))
    assert set(("decisiveMoment","decisionChange","mechanicalFeeling","friction","ideas"))<=set(ids)
    for f in data["feedbackFields"]:
        assert f["label"] and f["placeholder"]
    assert local_href_exists(data["protocol"])
    assert (BASE/"反馈/inbox").is_dir()


def build()->str:
    atlas=ATLAS.read_text(encoding="utf-8").strip()
    tasks=TASKS.read_text(encoding="utf-8").strip()
    data=json.loads(atlas)
    config=json.loads(tasks)
    validate_library(data)
    validate_tasks(config,data)
    template=TEMPLATE.read_text(encoding="utf-8")
    assert template.count("/*ATLAS_DATA*/")==1
    assert template.count("/*TASK_DATA*/")==1
    assert template.count("/*CLOUD_FEEDBACK_SCRIPT*/")==1
    assert template.count("/*WORKBENCH_CSS*/")==1
    assert template.count("/*WORKBENCH_JS*/")==1
    cloud=CLOUD.read_text(encoding="utf-8")
    wb_css=WORKBENCH_CSS.read_text(encoding="utf-8")
    wb_js=WORKBENCH_JS.read_text(encoding="utf-8")
    assert "</script" not in atlas.lower() and "</script" not in tasks.lower()
    assert "</script" not in cloud.lower(), "Embedded cloud code must not close its script"
    assert "</script" not in wb_js.lower() and "</style" not in wb_css.lower()
    return (template.replace("/*ATLAS_DATA*/",atlas)
                    .replace("/*TASK_DATA*/",tasks)
                    .replace("/*CLOUD_FEEDBACK_SCRIPT*/",cloud)
                    .replace("/*WORKBENCH_CSS*/",wb_css)
                    .replace("/*WORKBENCH_JS*/",wb_js))


def main()->None:
    parser=argparse.ArgumentParser()
    parser.add_argument("--check",action="store_true")
    args=parser.parse_args()
    expected=build()
    if args.check:
        assert OUTPUT.exists() and OUTPUT.read_text(encoding="utf-8")==expected, (
            "Human field station not synchronized. Run "
            "python3 tools/content-validator/experiments/build_mechanism_atlas.py")
        print("PASS: human field station, two JSON sources, cloud sync code, references and HTML parity")
    else:
        OUTPUT.write_text(expected,encoding="utf-8")
        print("Generated human field station:",OUTPUT)


if __name__=="__main__":
    main()
