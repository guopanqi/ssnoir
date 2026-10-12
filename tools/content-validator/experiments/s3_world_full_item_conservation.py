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
def run():
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
                elif get(obs,"action","带她穿过东侧门廊"):
                    stage="east_entry"
                else:obs=end(p,obs,history)
                continue
            if stage=="east_entry":
                obs=move_to(p,obs,"码头居民区",history)
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
                    stage="meet_manager"
                continue
            if stage=="meet_manager":
                obs=move_to(p,obs,"剧院",history)
                if get(obs,"action","见剧院的经理"):
                    obs=perform(p,obs,"见剧院的经理",history)
                    stage="wait_alley"
                elif get(obs,"navigate","经理"):
                    obs=choose_action(p,obs,get(obs,"navigate","经理"),history)
                elif obs["WorldDay"]<7:
                    obs=end(p,obs,history)
                else:
                    # The encounter's author may require a separate report.
                    candidate=next((o for o in obs["Operations"] if o["Kind"]=="action" and
                                "莱恩" in (o.get("Card") or "")),None)
                    if candidate:
                        obs=choose_action(p,obs,candidate,history)
                        stage="wait_alley"
                    else:
                        stage="wait_alley"
                continue
            if stage=="wait_alley":
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
                    assert counter_spent and obs["Inventory"].get(ITEM,0)==0
                    assert not legal,(obs["Inventory"],obs["Operations"])
                    return {"status":"same_original_world_counter_then_lyon_unusable",
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
        status="bounded_180_legal_ops_incomplete"
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
         "settings":{"entry":"world","seed":19,"growth":4,"items":{}},
         "scope":"real world legal operations; author-guided target action, not human or full alley continuation"}
def main():
    x=run()
    s=json.dumps(x,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(s,encoding="utf8")
    else:print(s)
if __name__=="__main__":main()
