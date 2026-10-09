#!/usr/bin/env python3
"""Native first-decision causal witnesses for four authored scenario families.

Unlike one-turn launch smoke, this performs actual actions and verifies that
important consequences happen in official C#/Scheme. Not a human playtest.
"""
from pathlib import Path
import subprocess,json
ROOT=Path(__file__).resolve().parents[3]
CMD=["dotnet","run","--no-build","--project","tools/content-validator/SSNoir.ContentValidator.csproj","--"]
def start(name):
    p=subprocess.Popen(CMD+["--session","encounters/实验·"+name,"--seed","19","--growth","1"],
      cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    return p,recv(p)
def recv(p):
    line=p.stdout.readline()
    if not line:raise AssertionError("Native process returned no response: "+str(p.poll()))
    msg=json.loads(line)
    if msg.get("type")=="error":raise AssertionError(msg)
    assert msg.get("observation"),msg
    return msg["observation"]
def act(p,obs,card=None,end=False):
    ops=obs["Operations"]
    if end:
        eligible=[v for v in ops if v.get("Kind")=="end-turn"]
    else:
        eligible=[v for v in ops if v.get("Card")==card]
    assert eligible,(card,[v.get("Card") for v in ops])
    selected=min(eligible,key=lambda v:(v.get("DieValue") or 0,str(v.get("Id"))))
    command={"command":"act","version":obs["Version"],"operationId":selected["Id"],"reason":"原生因果关系反证"}
    p.stdin.write(json.dumps(command,ensure_ascii=False)+"\n");p.stdin.flush()
    return recv(p)
def clock(obs,name):
    return next(c["Current"] for c in obs["Clocks"] if c["Label"]==name)
def finish(p):
    try:
        p.stdin.write('{"command":"quit"}\n');p.stdin.flush()
        p.communicate(timeout=12)
    finally:
        if p.poll() is None:p.kill()
def main():
    # Deliberate retreat: +2 evidence and +1 lie, then trade one evidence for
    # deleting the lie. This validates a *real regression in progress* can help.
    p,o=start("证词掺水")
    try:
        assert (clock(o,"口供"),clock(o,"假话"))==(0,0)
        o=act(p,o,"逼他说名字")
        assert (clock(o,"口供"),clock(o,"假话"))==(2,1)
        o=act(p,o,"核对车牌")
        assert (clock(o,"口供"),clock(o,"假话"))==(1,0)
        print("P17 PASS: knowingly sacrifice progress to remove contamination",flush=True)
    finally:finish(p)
    # The irreversible block removes the dangerous staircase and presents
    # a new ladder-repair action. It is *not* just +1 on a clock.
    p,o=start("封死退路")
    try:
        o=act(p,o,"楔死楼梯门")
        names={x.get("Card") for x in o["Operations"]}
        assert "楔死楼梯门" not in names
        assert "接好逃生梯" in names
        print("P20 PASS: closing pursuit irreversibly changes available escape actions",flush=True)
    finally:finish(p)
    # The offer really expires: courier appears before the later buyer.
    p,o=start("等买主现身")
    try:
        assert clock(o,"来客")==0
        o=act(p,o,end=True)
        assert clock(o,"来客")==1
        o=act(p,o,end=True)
        assert clock(o,"来客")==2
        print("P19 PASS: mutually exclusive immediate and later opportunities have different timing",flush=True)
    finally:finish(p)
    # The opponent follows last route rather than a globally fixed lane.
    p,o=start("枪口认路")
    try:
        o=act(p,o,"冲下栈桥")
        assert "底片已被海水毁掉" in json.dumps(o,ensure_ascii=False)
        o=act(p,o,end=True)
        assert "当前盯着栈桥" in json.dumps(o,ensure_ascii=False)
        print("P18 PASS: exposed route destroys collateral and redirects opponent next turn",flush=True)
    finally:finish(p)
if __name__=="__main__":main()
