#!/usr/bin/env python3
"""S3 follow-up: east/west ACTUAL first-chapter street-route graph, paper model.

Based on current UnityClient/Assets/Resources/Content/scenes/world/三封信.scm.
East = 4 porch + 4 gallery -> Frank.
West = 4 stairs + barricade 1 action (success 2/3 in simplified roll)
       + 4 bridge -> Ryan identified.
After Ryan known, east route actions disappear and solo Frank search takes
8 progress, or starts at progress 2 if porch was FINISHED. Any partial
east porch/gallery progress otherwise perishes. A single progress gain
0/1/2 uniform is a mathematical proxy, not native skills/composure or
observed player action odds. Script does NOT model cigarettes or companion
composure; results cannot certify current Unity gameplay.
"""
from fractions import Fraction as F
from functools import lru_cache
import json,sys
from pathlib import Path

ROOT=(0,0,0,0,False,0,16) # east entry/gallery, west stair/bridge, cleared, solo Frank, actions
OUT=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def study(no_closure=False,full_preserve=False,remove_barrier=False,include_paths=True):
    def done_f(s):return s[1]==4 or s[5]>=8
    def done_r(s):return s[3]==4
    def legal(s):
        ep,eg,ws,wb,c,f,n=s
        if n==0:return ()
        rs=done_r(s)
        opts=[]
        if (not rs or no_closure) and eg<4:
            if ep<4:opts.append("EastPorch")
            else:opts.append("EastGallery")
        if not rs:
            if ws<4:opts.append("WestStairs")
            elif not c and not remove_barrier:opts.append("Barrier")
            elif wb<4:opts.append("WestBridge")
        if rs and not done_f(s) and f<8:opts.append("SoloFrank")
        return tuple(opts)
    def transitions(s,act):
        ep,eg,ws,wb,cleared,f,n=s
        for gain,p in OUT:
            a,b,w,v,cl,z=ep,eg,ws,wb,cleared,f
            if act=="EastPorch":a=min(4,a+gain)
            if act=="EastGallery":b=min(4,b+gain)
            if act=="WestStairs":w=min(4,w+gain)
            if act=="Barrier":cl=bool(gain>=1)
            if act=="SoloFrank":z=min(8,z+gain)
            if act=="WestBridge":
                v=min(4,v+gain)
                if wb<4 and v==4:
                    if full_preserve:z=min(8,ep+eg)
                    elif ep==4:z=2
                    else:z=0
            yield p,(a,b,w,v,cl,z,n-1)
    def utility(s):return F(3)*F(done_r(s))+F(2)*F(done_f(s))
    @lru_cache(None)
    def V(s):
        choices=legal(s)
        if not choices:return utility(s)
        return max(sum(p*V(t) for p,t in transitions(s,x)) for x in choices)
    def Q(s):return {x:sum(p*V(t) for p,t in transitions(s,x)) for x in legal(s)}
    result={"optimal":str(V(ROOT)),"states_computed":V.cache_info().currsize}
    if not include_paths:return result
    def plan(sequence):
        @lru_cache(None)
        def walk(s):
            opts=legal(s)
            if not opts:return utility(s)
            x=next(a for a in sequence if a in opts)
            return sum(p*walk(t) for p,t in transitions(s,x))
        return walk(ROOT)
    fixed_east=plan(["EastPorch","EastGallery","WestStairs","Barrier","WestBridge","SoloFrank"])
    fixed_west=plan(["WestStairs","Barrier","WestBridge","SoloFrank","EastPorch","EastGallery"])
    assert V(ROOT)>=fixed_east and V(ROOT)>=fixed_west
    tie_order=["WestStairs","Barrier","WestBridge","SoloFrank","EastPorch","EastGallery"]
    mass_early_east=F(0)
    mass_early_west=F(0)
    both_available=F(0)
    strict_vs_best=F(0)
    witnesses=[]
    best_fixed=(fixed_east,"east-first") if fixed_east>=fixed_west else (fixed_west,"west-first")
    best_order=(["EastPorch","EastGallery","WestStairs","Barrier","WestBridge","SoloFrank"] if best_fixed[1]=="east-first" else tie_order)
    def forward(s,mass):
        nonlocal mass_early_east,mass_early_west,both_available,strict_vs_best
        q=Q(s)
        if not q:return
        best=max(q.values())
        action=next(x for x in tie_order if x in q and q[x]==best)
        fb=next(x for x in best_order if x in q)
        if "EastPorch" in q and "WestStairs" in q:
            both_available+=mass
            if q["EastPorch"]>q["WestStairs"] and action=="EastPorch":mass_early_east+=mass
            if q["WestStairs"]>q["EastPorch"] and action=="WestStairs":mass_early_west+=mass
        if action!=fb and q[action]>q[fb]:
            strict_vs_best+=mass
            if len(witnesses)<5:witnesses.append({"state":list(s),"one_history_mass":str(mass),
                "optimal":action,"fixed":fb,"Q":{k:str(v) for k,v in q.items()}})
        for p,t in transitions(s,action):forward(t,mass*p)
    forward(ROOT,F(1))
    result.update({"fixed_east_first":str(fixed_east),"fixed_west_first":str(fixed_west),
        "best_fixed_order":best_fixed[1],"gap_to_best_fixed":str(V(ROOT)-best_fixed[0]),
        "both_entry_routes_legal_expected_visits":str(both_available),
        "strict_east_before_west_expected_visits":str(mass_early_east),
        "strict_west_before_east_expected_visits":str(mass_early_west),
        "strict_switch_vs_best_fixed_expected_visits":str(strict_vs_best),
        "normal_witnesses":witnesses})
    return result

def main():
    core=study()
    no_close=study(no_closure=True,include_paths=False)
    preserve=study(full_preserve=True,include_paths=False)
    no_bar=study(remove_barrier=True,include_paths=False)
    assert F(no_close["optimal"])>=F(core["optimal"])
    assert F(preserve["optimal"])>=F(core["optimal"])
    assert F(no_bar["optimal"])>=F(core["optimal"])
    report={"stage":"S3-live-source east/west route ordering",
        "identity":"Synthetic Exact Fraction model, anchored to actual 4+4 East and West progress source; no source item, risk, stamina, day-specific reveal, player subjective valuation or native execution.",
        "main_first_name_reward":"3","frank_contact_reward":"2",
        "initial_window":"16 paper actions AFTER Nightingale joins; not actual chapter deadline",
        "core":core,"ablations":{"do_not_close_east_after_ryan":no_close,
          "preserve_all_east_partial_progress_in_solo_search":preserve,
          "remove_west_stairs_barricade":no_bar},
        "deterministic_work_cost_witnesses":{"east_full_then_west":16,
          "west_full_then_frank_solo":16,
          "east_porch_full_then_west_then_frank_solo":18,
          "east_porch_partial_1_then_west_then_frank_solo":17,
          "east_porch_full_and_gallery_progress_2_then_west_then_frank_solo":20},
        "source_notes":["west bridge identifies Ryan, immediately removes East route cards",
          "east to Frank two legs 4+4; after West optional solo Frank takes 8, receives only +2 if East porch completed",
          "West stair barricade offers Social/Violence options; model uses abstract success2/3 and no injury",
          "the letter/cigarette item can add 2 gallery progress at the cost of consuming a unique item, deliberately excluded"]}
    payload=json.dumps(report,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(payload,encoding="utf8")
    else:print(payload)
if __name__=="__main__":main()
