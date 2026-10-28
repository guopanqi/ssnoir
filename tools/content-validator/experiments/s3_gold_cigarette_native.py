#!/usr/bin/env python3
"""S3-W: actual unmodified 'scenarios/巷子' C#+Scheme session, with --item fixture.

This checks a real authored Lyon item action, NOT full first-chapter
world progression nor official way to acquire/cash the item at east counter.
--item is the documented session TEST setup, not a player-facing grant.
"""
import json,sys
from pathlib import Path
from subprocess import Popen, PIPE
from probe_four_scenario_causality import ROOT,CMD,recv,finish

ITEM="半包「老金牌」香烟"
ENTRY="scenarios/巷子"
SEED=19
GROWTH=4

def action(p,obs,op,reason="S3-W 原生装备守恒"):
    p.stdin.write(json.dumps({"command":"act","version":obs["Version"],
        "operationId":op["Id"],"reason":reason},ensure_ascii=False)+"\n")
    p.stdin.flush()
    return recv(p)

def get_op(obs,kind=None,card=None):
    found=[op for op in obs["Operations"]
           if (kind is None or op["Kind"]==kind) and (card is None or op.get("Card")==card)]
    if not found:return None
    # Prefer highest observed prepared chance while holding the same legal set.
    return max(found,key=lambda op:(op.get("Prepared") or op.get("DieValue") or -99))

def inventory(obs):
    return int(obs.get("Inventory",{}).get(ITEM,0))

def name_cards(obs):
    return {x.get("Name",x.get("Title","")) for x in obs.get("Cards",[])}

def save_transcript(p,qty):
    path=f"/tmp/s3-w-lyon-item-{qty}.json"
    p.stdin.write(json.dumps({"command":"save","path":path})+"\n")
    p.stdin.flush()
    msg=json.loads(p.stdout.readline())
    assert msg.get("type")=="saved",(path,msg)
    return path

def test_with_fixture(qty):
    p=Popen(CMD+["--session",ENTRY,"--seed",str(SEED),"--growth",str(GROWTH),
                 "--item",f"{ITEM}={qty}"],cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
    obs=recv(p)
    history=[]
    initial_scene=obs["Scene"]
    seen_guarantee=False
    try:
        assert inventory(obs)==qty,(inventory(obs),qty)
        for step in range(110):
            avail={x.get("Card") for x in obs["Operations"] if x["Kind"]=="action"}
            cards=name_cards(obs)
            if "把烟盒扔到桌上" in cards:
                before_count=inventory(obs)
                can_use="把烟盒扔到桌上" in avail
                assert can_use==(qty==1),(qty,avail,cards)
                if qty:
                    chosen=get_op(obs,"action","把烟盒扔到桌上")
                    obs=action(p,obs,chosen)
                    after_count=inventory(obs)
                    assert after_count==0,(before_count,after_count)
                    assert "把烟盒扔到桌上" not in {
                        x.get("Card") for x in obs["Operations"] if x["Kind"]=="action"}
                    return {"fixture_qty":qty,"initial_inventory":qty,
                      "phase2_reached_in_original_scenario":True,"card_present_before":True,
                      "usable_before":True,"before_count":before_count,"after_count":after_count,
                      "original_lyon_item_action_executed":True,
                      "item_can_be_spent_again":False,
                      "transcript":save_transcript(p,qty),
                      "steps_to_phase2":len(history),"history":history}
                return {"fixture_qty":qty,"initial_inventory":qty,
                    "phase2_reached_in_original_scenario":True,
                    "card_present_before":True,"usable_before":False,
                    "before_count":before_count,"after_count":inventory(obs),
                    "original_lyon_item_action_executed":False,
                    "transcript":save_transcript(p,qty),
                    "steps_to_phase2":len(history),"history":history}
            focus=obs.get("Focus","")
            # Current root shows the three employer question containers and
            # optional promises. Franks' trust moves by 2 per finished topic.
            proposal=[]
            if not seen_guarantee:
                proposal=["保证不用经理压他"]
            proposal+=["说清谁付的钱","把她算回来","只拿信和底片"]
            chosen=None
            for name in proposal:
                if name in avail:
                    chosen=get_op(obs,"action",name)
                    if name=="保证不用经理压他":seen_guarantee=True
                    break
            if chosen is None:
                target_nav=["三项保证"] if not seen_guarantee else []
                target_nav+=["你替谁办事？","这为何归你管？","问完以后怎么办？"]
                for n in target_nav:
                    chosen=get_op(obs,"navigate",n)
                    if chosen:break
            if chosen is None and get_op(obs,"back"):
                chosen=get_op(obs,"back")
            if chosen is None:
                chosen=get_op(obs,"end-turn")
            assert chosen is not None,(qty,focus,obs.get("Scene"),history[-10:])
            history.append({"step":step,"focus":focus,"kind":chosen["Kind"],
                "card":chosen.get("Card"),"label":chosen["Label"],
                "trust":next((c["Current"] for c in obs.get("Clocks",[])
                               if c["Label"]=="弗兰克的判断"),None),
                "inventory":inventory(obs)})
            try:
                obs=action(p,obs,chosen)
            except Exception as exc:
                raise AssertionError(f"Native action error after {len(history)} commands: {history[-12:]}: {exc}") from exc
            if obs.get("Scene")!=initial_scene:
                raise AssertionError("Left original scenario before Lyon phase: "+str(history[-12:]))
        raise AssertionError("Could not reach original Lyon phase in 110 genuine operations "+str(history[-15:]))
    finally:
        finish(p)

def main():
    no_item=test_with_fixture(0)
    owned=test_with_fixture(1)
    assert owned["original_lyon_item_action_executed"]
    assert owned["before_count"]==1 and owned["after_count"]==0
    assert not no_item["usable_before"]
    assert no_item["card_present_before"]
    report={"identity":"Actual unmodified SSNoir C# + Scheme scenarios/巷子 item action, via explicit --item test fixture; NOT normal world first-to-third-stage progression.",
      "seed":SEED,"growth":GROWTH,"item":ITEM,
      "controls":{"unowned":no_item,"owned":owned},
      "nonclaims":["Does not execute official east-side counter", "Does not show player naturally acquired cigarette",
       "Does not prove cross-stage normal story reachability", "No human experience"] }
    output=json.dumps(report,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(output,encoding="utf8")
    else:print(output)
if __name__=="__main__":main()
