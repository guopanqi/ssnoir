#!/usr/bin/env python3
"""S2-D real C# + Scheme observation of R122 authored semantic contract.

The paper model uses 0/1/2 uniform; this isolated native test uses one
guaranteed progress per die and ONLY proves that the actor actually sees both
actions after a revelation and can choose either, with real closure and
day-end behavior. Not mathematical distribution parity or human enjoyment.
"""
from subprocess import Popen,PIPE
import json,sys
from probe_four_scenario_causality import ROOT,CMD,recv,act,clock,finish

ENTRY="scenarios/研究·线索两选"
def start():
    p=Popen(CMD+["--session",ENTRY,"--seed","19","--growth","1"],
            cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
    return p,recv(p)
def names(obs):
    return {v.get("Card") for v in obs["Operations"] if v.get("Card")}
def prepare(p,obs):
    assert {"调查信件","帮艾迪"}.issubset(names(obs)),(names(obs),obs)
    assert "沿线追查" not in names(obs) and "交付信件" not in names(obs)
    obs=act(p,obs,"调查信件")
    assert clock(obs,"查信")==1
    assert "沿线追查" not in names(obs)
    obs=act(p,obs,"调查信件")
    assert clock(obs,"查信")==2
    assert {"沿线追查","交付信件","帮艾迪"}.issubset(names(obs)),names(obs)
    assert "调查信件" not in names(obs)
    return obs
def test_respond():
    p,o=start()
    try:
        o=prepare(p,o)
        revealed=names(o)
        o=act(p,o,"沿线追查")
        assert clock(o,"递信踪迹")==1
        assert "交付信件" in names(o)
        assert "沿线追查" in names(o)
        o=act(p,o,"交付信件")
        assert "沿线追查" not in names(o)
        assert "交付信件" not in names(o)
        return {"revealed_choices":sorted(revealed),
                "trace_action_has_effect":True,
                "can_still_commit_after_partial_trace":True,
                "commit_hides_unfinished_trace":True}
    finally:finish(p)
def test_commit_first():
    p,o=start()
    try:
        o=prepare(p,o)
        o=act(p,o,"交付信件")
        assert "沿线追查" not in names(o)
        assert clock(o,"递信踪迹")==0
        return {"commit_before_trace":True,"trace_closed":True}
    finally:finish(p)
def test_day_boundary():
    p,o=start()
    try:
        assert "帮艾迪" in names(o)
        o=act(p,o,end=True)
        assert clock(o,"已过日期")==1
        assert "帮艾迪" not in names(o)
        assert "交付信件" not in names(o)
        assert "调查信件" not in names(o)
        return {"day1_offer_expired":True,"day1_letter_closed":True,"day_progress":1}
    finally:finish(p)
def main():
    result={
      "evidence":"Native SSNoir.ContentValidator C# + Scheme authored scripted actions",
      "scene":ENTRY,"seed":19,"growth":1,
      "scope":"Validates reveal -> two legal actions -> consequence and one day boundary; DOES NOT validate paper probabilities, human comprehension or rewarding strategy",
      "cases":{"trace_first":test_respond(),"commit_first":test_commit_first(),"day_boundary":test_day_boundary()}}
    output=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(output,encoding="utf8")
    else:print(output)
if __name__=="__main__":main()
