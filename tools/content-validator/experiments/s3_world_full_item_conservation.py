#!/usr/bin/env python3
"""Bounded complete-world attempt: new save -> chase -> counter -> Lyon encounter.

No --item, no arbitrary Scheme Eval, no debug-finish or story-stage writes.
All operations are selected from actual public SessionObservation. The
targeted route uses knowledge of authored names for test COVERAGE; it is
not a blind-play human decision sample or a performance benchmark.
The report can end BLOCKED without asserting full-route success.
"""
from pathlib import Path
from subprocess import Popen,PIPE
import json,sys
from probe_four_scenario_causality import ROOT,CMD,recv,finish

ITEM="半包「老金牌」香烟"
TARGET="把烟盒给柜台后的人看"
MAXSTEPS=260

def get(obs,kind=None,card=None):
    choices=[x for x in obs["Operations"] if (kind is None or x["Kind"]==kind)
            and (card is None or x.get("Card")==card)]
    return max(choices,key=lambda x:(x.get("Prepared") or x.get("DieValue") or -99),default=None)
def choose_action(p,obs,op,history):
    assert op,(obs["Focus"],obs["WorldDay"],[(x["Kind"],x.get("Card")) for x in obs["Operations"]])
    command={"command":"act","version":obs["Version"],"operationId":op["Id"],"reason":"S3-W从新档正常前进的测试覆盖"}
    history.append({"day":obs["WorldDay"],"scene":obs["Scene"],"focus":obs["Focus"],
        "kind":op["Kind"],"card":op.get("Card"),"inventory":obs["Inventory"].get(ITEM,0)})
    p.stdin.write(json.dumps(command,ensure_ascii=False)+"\n");p.stdin.flush()
    return recv(p)
def move_to(p,obs,place,history):
    # Walk out of currently focused world place and enter an original place.
    for _ in range(6):
        if obs["IsInEncounter"]:raise RuntimeError("Cannot navigate worlds inside encounter")
        if obs["Focus"]==place:return obs
        o=get(obs,"enter-place",place)
        if o:return choose_action(p,obs,o,history)
        o=get(obs,"back")
        if o:obs=choose_action(p,obs,o,history)
        else:break
    raise RuntimeError(f"Cannot enter {place}; focus {obs['Focus']}; ops "+str([(x["Kind"],x.get("Card")) for x in obs["Operations"]]))
def perform(p,obs,card,history):
    op=get(obs,"action",card)
    if not op:raise RuntimeError(f"Action {card} missing at {obs['Focus']}, ops="+str([(x["Kind"],x.get("Card")) for x in obs["Operations"]]))
    return choose_action(p,obs,op,history)
def end(p,obs,history):
    return choose_action(p,obs,get(obs,"end-turn"),history)
def run(spend_counter=True, let_chase_escape=False, stop_after_west_identity=False):
    p=Popen(CMD+["--session","world","--seed","19","--growth","4"],
       cwd=ROOT,stdin=PIPE,stdout=PIPE,stderr=PIPE,text=True)
    obs=recv(p);history=[];stage="wait_commission";cig_obtained=False;counter_spent=False;obstacles=[]
    try:
        for i in range(MAXSTEPS):
            if stage=="wait_commission":
                obs=move_to(p,obs,"家",history)
                if get(obs,"action","有人敲门"):
                    obs=perform(p,obs,"有人敲门",history);stage="mail_preparation"
                else:obs=end(p,obs,history)
                continue
            if stage=="mail_preparation":
                obs=move_to(p,obs,"码头",history)
                for card in ("观察邮箱街角","查看退路"):
                    if get(obs,"action",card):
                        obs=perform(p,obs,card,history)
                        break
                else:
                    if get(obs,"action","去码头盯着邮箱"):
                        obs=perform(p,obs,"去码头盯着邮箱",history)
                        stage="mail_encounter"
                    else:obs=end(p,obs,history)
                continue
            if stage=="mail_encounter":
                if let_chase_escape and obs["IsInEncounter"]:
                    # Valid public end-turns instead of a chase-item fixture.
                    # If the actual session requires a mandatory response this
                    # route fails with its real public state (no state mutation).
                    op=get(obs,"end-turn")
                    if not op:raise RuntimeError("Cannot decline chase using a public end-turn")
                    obs=choose_action(p,obs,op,history)
                    continue
                if not obs["IsInEncounter"]:
                    cig_obtained=obs["Inventory"].get(ITEM,0)==1
                    stage="post_chase_home"
                    continue
                # The target is visually the uniformed younger courier. This
                # is an AUTHOR-ROUTE replay, never human choice evidence.
                if get(obs,"navigate","穿制服的年轻人"):
                    obs=choose_action(p,obs,get(obs,"navigate","穿制服的年轻人"),history)
                elif get(obs,"action","对照投递规矩"):
                    obs=perform(p,obs,"对照投递规矩",history)
                elif get(obs,"action","看那身制服"):
                    obs=perform(p,obs,"看那身制服",history)
                elif get(obs,"action","绕过去"):
                    obs=perform(p,obs,"绕过去",history)
                elif get(obs,"action","贴着货堆推进"):
                    obs=perform(p,obs,"贴着货堆推进",history)
                elif get(obs,"action","喊住他"):
                    obs=perform(p,obs,"喊住他",history)
                elif get(obs,"action","从货栈边门包抄"):
                    obs=perform(p,obs,"从货栈边门包抄",history)
                elif get(obs,"back"):
                    obs=choose_action(p,obs,get(obs,"back"),history)
                else:obs=end(p,obs,history)
                continue
            if stage=="post_chase_home":
                obs=move_to(p,obs,"家",history)
                if get(obs,"action","她来看你"):
                    obs=perform(p,obs,"她来看你",history)
                    stage="alone_street"
                else:
                    if obs["WorldDay"]>22:
                        raise RuntimeError("Post-chase home visit not reachable before day 22")
                    obs=end(p,obs,history)
                continue
            if stage=="alone_street":
                obs=move_to(p,obs,"码头居民区",history)
                if get(obs,"action","独自在老街打听"):
                    obs=perform(p,obs,"独自在老街打听",history)
                elif let_chase_escape and get(obs,"action","陪她上西侧楼梯"):
                    stage="west_street"
                elif get(obs,"action","带她穿过东侧门廊"):
                    stage="east_entry"
                else:obs=end(p,obs,history)
                continue
            if stage=="east_entry":
                obs=move_to(p,obs,"码头居民区",history)
                if not spend_counter and get(obs,"action",TARGET):
                    assert obs["Inventory"].get(ITEM,0)==1,obs["Inventory"]
                    # Declining an offered choice is not an engine state edit.
                    stage="west_street"
                    continue
                if get(obs,"action",TARGET):
                    before=obs["Inventory"].get(ITEM,0)
                    obs=perform(p,obs,TARGET,history)
                    after=obs["Inventory"].get(ITEM,0)
                    assert before==1 and after==0,(before,after)
                    counter_spent=True
                    stage="west_street"
                    continue
                if get(obs,"action","带她穿过东侧门廊"):
                    obs=perform(p,obs,"带她穿过东侧门廊",history)
                else:
                    if not cig_obtained:
                        return {"status":"no_cigarette_from_original_chase","item_naturally_acquired":False,
                            "counter_spent":False,"native_steps":history,
                            "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
                            "obstacles":[],"scope":"original chase did not yield item, not yet later alley"}
                    obs=end(p,obs,history)
                continue
            if stage=="west_street":
                obs=move_to(p,obs,"码头居民区",history)
                for name in ("陪她上西侧楼梯","跟横椅子的人说","硬挤过去","跟她走上层桥廊"):
                    if get(obs,"action",name):
                        obs=perform(p,obs,name,history)
                        break
                else:
                    west_cards={"陪她上西侧楼梯","跟横椅子的人说","硬挤过去","跟她走上层桥廊"}
                    if any(card["Name"] in west_cards for card in obs["Cards"]):
                        # Visible-but-unusable after spending all today's dice
                        # is not equivalent to "route complete".
                        if obs["RestBlockers"]:
                            stage="meet_manager"
                        else:
                            obs=end(p,obs,history)
                    else:
                        if stop_after_west_identity:
                            # Stop at the FIRST official observation after
                            # the West clue reveal; do not advance day to
                            # trigger delayed Nightingale dismissal yet.
                            cards=[{"name":c.get("Name"),"subtitle":c.get("Subtitle"),
                                    "description":c.get("Description")}
                                   for c in obs.get("Cards",[])]
                            next_ops=[{"kind":o.get("Kind"),"card":o.get("Card")}
                                      for o in obs.get("Operations",[])]
                            transcript_path="/tmp/s3-y-west-identity-native.json"
                            p.stdin.write(json.dumps({"command":"save","path":transcript_path},ensure_ascii=False)+"\n")
                            p.stdin.flush()
                            msg=json.loads(p.stdout.readline())
                            assert msg.get("type")=="saved",msg
                            return {
                              "status":"original_world_after_west_identity_before_day_end",
                              "setting":{"session":"world","seed":19,"growth":4,"item_fixture":False},
                              "transcript":transcript_path,
                              "world_day":obs["WorldDay"],"focus":obs["Focus"],
                              "scene":obs.get("Scene"),"cards":cards,"operations":next_ops,
                              "companion_fields":{
                                  k:v for k,v in obs.items()
                                  if ("companion" in k.lower() or "actor" in k.lower())},
                              "item_quantity":obs.get("Inventory",{}).get(ITEM,0),
                              "already_spent_at_counter":counter_spent,
                              "native_steps":history,
                              "evidence":"Actual official world C#+Scheme snapshot, not human interpretation",
                            }
                        stage="meet_manager"
                continue
            if stage=="meet_manager":
                # If focused inside the Manager container, act BEFORE moving to
                # the theatre place; otherwise backtracking would lose focus.
                if obs["Focus"]!="经理":
                    obs=move_to(p,obs,"剧院",history)
                if get(obs,"action","报告莱恩身份"):
                    obs=perform(p,obs,"报告莱恩身份",history)
                    stage="wait_alley"
                elif get(obs,"action","见剧院的经理"):
                    obs=perform(p,obs,"见剧院的经理",history)
                    stage="west_street"
                elif get(obs,"navigate","经理"):
                    obs=choose_action(p,obs,get(obs,"navigate","经理"),history)
                elif obs["WorldDay"]<=17:
                    # Name has been obtained; official "离开老街后谈起莱恩"
                    # runs on end-day, only afterwards is reporting legal.
                    obs=end(p,obs,history)
                else:
                    raise RuntimeError("No '报告莱恩身份' was public before day17")
                continue
            if stage=="wait_alley":
                if obs["WorldDay"]>25:
                    raise RuntimeError("No original alley entry before day25; west inquiry or manager report may be unfinished")
                obs=move_to(p,obs,"码头",history)
                if get(obs,"action","今晚去货栈后面"):
                    obs=perform(p,obs,"今晚去货栈后面",history)
                    stage="alley"
                else:
                    obs=end(p,obs,history)
                continue
            if stage=="alley":
                if not obs["IsInEncounter"]:
                    raise RuntimeError("Returned to world before original Lyon form1")
                cards={c["Name"] for c in obs["Cards"]}
                if "把烟盒扔到桌上" in cards:
                    legal=bool(get(obs,"action","把烟盒扔到桌上"))
                    if let_chase_escape:
                        assert not cig_obtained
                        assert obs["Inventory"].get(ITEM,0)==0 and not legal
                        alternative_ops={name:bool(get(obs,"action",name))
                            for name in ("摊开那两封信","提她现在的样子")}
                        selected=next((name for name,ok in alternative_ops.items() if ok),None)
                        alternative_executed=False
                        if selected:
                            obs=perform(p,obs,selected,history)
                            assert obs["Inventory"].get(ITEM,0)==0
                            alternative_executed=True
                        transcript_path="/tmp/s3-x-no-cigarette-world-to-lyon.json"
                        p.stdin.write(json.dumps({"command":"save","path":transcript_path},ensure_ascii=False)+"\n")
                        p.stdin.flush()
                        msg=json.loads(p.stdout.readline())
                        assert msg.get("type")=="saved",msg
                        return {"status":"same_original_world_no_cigarette_reached_lyon",
                            "transcript":transcript_path,
                            "item_naturally_acquired":False,"counter_spent":False,
                            "lyon_card_visible":True,"lyon_item_action_legal":False,
                            "lyon_inventory":0,"alternative_actions":alternative_ops,
                            "alternative_action_selected":selected,
                            "alternative_action_executed":alternative_executed,
                            "native_steps":history,
                            "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
                            "obstacles":[],
                            "scope":"real end-turns in first chase, original west inquiry and Lyon; no item fixtures, not proven equal story outcome or human"}
                    if not spend_counter:
                        assert not counter_spent and cig_obtained
                        assert obs["Inventory"].get(ITEM,0)==1 and legal
                        before_clocks=obs.get("Clocks",[])
                        obs=perform(p,obs,"把烟盒扔到桌上",history)
                        assert obs["Inventory"].get(ITEM,0)==0
                        after_clocks=obs.get("Clocks",[])
                        transcript_path="/tmp/s3-w-retained-world-to-lyon.json"
                        p.stdin.write(json.dumps({"command":"save","path":transcript_path},ensure_ascii=False)+"\n")
                        p.stdin.flush()
                        msg=json.loads(p.stdout.readline())
                        assert msg.get("type")=="saved",msg
                        return {"status":"same_original_world_saved_for_lyon_spent",
                            "transcript":transcript_path,
                            "item_naturally_acquired":cig_obtained,"counter_spent":False,
                            "counter_option_declined":True,
                            "lyon_card_visible":True,"lyon_item_action_legal":True,
                            "lyon_item_executed":True,
                            "lyon_inventory_before":1,"lyon_inventory_after":0,
                            "lyon_clock_before":before_clocks,"lyon_clock_after":after_clocks,
                            "native_steps":history,
                            "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
                            "obstacles":[],
                            "scope":"one original world new save; no counter use, real Lyon item consumption, not human"}
                    assert counter_spent and obs["Inventory"].get(ITEM,0)==0
                    assert not legal,(obs["Inventory"],obs["Operations"])
                    transcript_path="/tmp/s3-w-same-world-to-lyon.json"
                    p.stdin.write(json.dumps({"command":"save","path":transcript_path},ensure_ascii=False)+"\n")
                    p.stdin.flush()
                    save_response=json.loads(p.stdout.readline())
                    assert save_response.get("type")=="saved",save_response
                    return {"status":"same_original_world_counter_then_lyon_unusable",
                        "transcript":transcript_path,
                        "item_naturally_acquired":cig_obtained,"counter_spent":counter_spent,
                        "lyon_card_visible":True,"lyon_item_action_legal":False,
                        "lyon_worldday":obs["WorldDay"],"lyon_inventory":obs["Inventory"].get(ITEM,0),
                        "native_steps":history,
                        "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
                        "obstacles":[],"scope":"One unmodified new world session continuously passed early counter to the original Lyon encounter form1; author-known operation choices not human"}
                for name in ("保证不用经理压他","说清谁付的钱","从头讲起","把她算回来","只拿信和底片"):
                    if get(obs,"action",name):
                        obs=perform(p,obs,name,history)
                        break
                else:
                    for name in ("你替谁办事？","这为何归你管？","问完以后怎么办？"):
                        if get(obs,"navigate",name):
                            obs=choose_action(p,obs,get(obs,"navigate",name),history)
                            break
                    else:
                        if get(obs,"back"):
                            obs=choose_action(p,obs,get(obs,"back"),history)
                        else:
                            obs=end(p,obs,history)
                continue
            raise RuntimeError("Unexpected phase "+stage)
        status="bounded_260_iterations_incomplete"
    except Exception as exc:
        obstacles.append(str(exc))
        status="blocked_by_actual_public_route"
    finally:
        last={"stage":stage,"scene":obs.get("Scene"),"focus":obs.get("Focus"),
              "day":obs.get("WorldDay"),"item":obs.get("Inventory",{}).get(ITEM,0),
              "operations":[{"kind":x["Kind"],"card":x.get("Card")} for x in obs.get("Operations",[])[:30]]}
        finish(p)
    return {"status":status,"item_naturally_acquired":cig_obtained,
         "counter_spent":counter_spent,"native_steps":history,
         "last_public_observation":last,"obstacles":obstacles,
         "milestones":[h for h in history if h.get("card") in ("带她穿过东侧门廊","把烟盒给柜台后的人看","陪她上西侧楼梯","跟横椅子的人说","硬挤过去","跟她走上层桥廊","见剧院的经理","报告莱恩身份","今晚去货栈后面")][-38:],
         "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
         "scope":"real world legal operations; author-guided target action, not human or full alley continuation"}
def main():
    x=run()
    s=json.dumps(x,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(s,encoding="utf8")
    else:print(s)
if __name__=="__main__":main()
