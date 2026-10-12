#!/usr/bin/env python3
"""S2-E native action-budget witnesses, not a mathematical win-rate trial.

Use the SAME original C# + Scheme scenario and actual first-round four dice.
Prove that choosing two different available actions after the clue can lead
to two different irrevocable reachable accomplishments. No new game feature.
"""
import json,sys
from s2_true_choice_native import start,prepare,names
from probe_four_scenario_causality import act,clock,finish

def remaining_dice(obs):
    # This is native public state, not a fake independent action budget.
    return sum(len(x.get("Dice",[])) for x in obs["Actors"])

def secure_letter_path():
    p,obs=start()
    try:
        assert remaining_dice(obs)==4
        obs=prepare(p,obs)
        assert remaining_dice(obs)==2
        assert "交付信件" in names(obs) and "沿线追查" in names(obs)
        obs=act(p,obs,"交付信件")
        assert remaining_dice(obs)==1
        assert "沿线追查" not in names(obs)
        return {"initial_dice":4,"dice_at_first_reveal":2,
                "secured_letter":True,"lead_finished":False,
                "mutual_choice_really_executable":True}
    finally:finish(p)

def follow_to_completion_path():
    p,obs=start()
    try:
        obs=prepare(p,obs)
        assert remaining_dice(obs)==2
        obs=act(p,obs,"沿线追查")
        assert clock(obs,"递信踪迹")==1
        obs=act(p,obs,"沿线追查")
        assert clock(obs,"递信踪迹")==2
        assert remaining_dice(obs)==0
        # A has NOT automatically closed the clue, and no delivery happened.
        assert clock(obs,"查信")==2
        obs=act(p,obs,end=True)
        assert clock(obs,"已过日期")==1
        assert "交付信件" not in names(obs)
        return {"initial_dice":4,"dice_at_first_reveal":2,
                "lead_finished":True,"letter_delivered":False,
                "letter_deadline_expired_after_day1":True}
    finally:finish(p)

def prioritize_fighter_path():
    p,obs=start()
    try:
        assert remaining_dice(obs)==4
        obs=act(p,obs,"帮艾迪")
        obs=act(p,obs,"帮艾迪")
        assert clock(obs,"艾迪请求")==2
        obs=act(p,obs,"调查信件")
        obs=act(p,obs,"调查信件")
        assert clock(obs,"查信")==2
        assert remaining_dice(obs)==0
        obs=act(p,obs,end=True)
        assert clock(obs,"已过日期")==1
        assert "交付信件" not in names(obs)
        return {"help_fighter_completed":True,"lead_revealed":True,
                "letter_not_delivered":True,
                "first_day_budget_exhausted":True}
    finally:finish(p)

def main():
    result={"study":"S2-E capacity witness",
            "evidence":"three actual C# + Scheme scripted sessions; four original dice before end-turn",
            "scope":"Deterministic +1 research native scenario only. Demonstrates different reachable outcomes, NOT 0/1/2 paper distribution, not human choices, not real story canon.",
            "paths":{"submit":secure_letter_path(),
                     "trace":follow_to_completion_path(),
                     "fighter":prioritize_fighter_path()}}
    output=json.dumps(result,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(output,encoding="utf8")
    else:
        print(output)
if __name__=="__main__":main()
