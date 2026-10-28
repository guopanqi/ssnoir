#!/usr/bin/env python3
"""S3-Y: actual content text contract + small exact-Fraction *paper* counterexamples.

NO game changes, no native C# execution, no human verdict. The program checks
that the current source still exposes the factual affordances used by the
research report. Its independent mathematical section is conditional on the
invented equal-probability example, not the real social/knowledge skill dice.
"""
from fractions import Fraction as F
from pathlib import Path
import json,re,sys

ROOT=Path(__file__).resolve().parents[3]
WORLD=ROOT/"UnityClient/Assets/Resources/Content/scenes/world/三封信.scm"
LYON=ROOT/"UnityClient/Assets/Resources/Content/scenes/scenarios/巷子.scm"
ITEM='半包「老金牌」香烟'

def block(source,name):
    m=re.search(r"(?m)^\s*\(define \("+re.escape(name)+r"\)",source)
    assert m, f"missing Scheme function {name}"
    begin=m.start()
    # Next top-level/local define ends this node function. Nested lambdas aren't defines.
    following=re.search(r"(?m)^\s*\(define \(",source[m.end():])
    assert following, name
    return source[begin:m.end()+following.start()]

def require(text,needle,context):
    assert needle in text,(context,needle)

def audit():
    w=WORLD.read_text(encoding="utf-8")
    l=LYON.read_text(encoding="utf-8")
    east=block(w,"node-east-entry")
    west=block(w,"node-west-stairs")
    east_deep=block(w,"node-east-gallery")
    west_deep=block(w,"node-west-bridge")
    counter=block(w,"node-show-cigs-at-counter")
    final_west=block(w,"finish-bridge-inquiry!")
    final_east=block(w,"finish-union-inquiry!")
    lyon_cigs=block(l,"node-cigs")
    letters=block(l,"node-letters")
    her=block(l,"node-her-now")
    act2=block(l,"act2-nodes")
    fail=block(l,"thrown-out!")
    # PRE-REVELATION front card names/subtitles cannot reveal hidden destination.
    for name,x in [("east",east),("west",west),("east_gallery",east_deep),("west_bridge",west_deep)]:
        # This checks only the node text block, not live event popups nor human interpretation.
        assert '莱恩' not in x and '弗兰克' not in x,(name,"premature named solution")
    require(east,":resolve (roll 'social",'east public skill')
    require(west,":resolve (roll 'knowledge",'west public skill')
    require(final_west,'莱恩', 'west endpoint')
    require(final_east,'弗兰克','east endpoint')
    require(counter,'东侧回廊推进两格','counter public gain')
    require(counter,'烟盒他留下，你不能再带走','counter public cost')
    require(counter,'(req-item cigs-item 1)','counter item guard')
    require(counter,'(advance-east-gallery! 2 #f)','counter effect')
    require(lyon_cigs,'(req-item "'+ITEM+'" 1)','lyon same physical resource')
    require(lyon_cigs,'(advance-lyon! 2)','lyon gain')
    require(lyon_cigs,':disabled cigs-used?','lyon once-only')
    require(lyon_cigs,':resolve (instant','lyon no die')
    require(letters,'"摊开那两封信"','low-risk alternative')
    require(letters,":resolve (roll 'knowledge",'low-risk roll')
    require(letters,'(spend-composure! 1)','low-risk failure penalty')
    require(letters,'(advance-lyon! 1)','low-risk mid success')
    require(letters,'(advance-lyon! 2)','low-risk high success')
    require(her,'"提她现在的样子"','high-risk alternative')
    require(her,":resolve (roll 'social",'high-risk roll')
    require(her,'(spend-composure! 2) (crowd+ 1)','high-risk fail pressure')
    require(her,'(advance-lyon! 2)','high-risk mid')
    require(her,'(advance-lyon! 3)','high-risk high')
    require(act2,'(= form 1) (list (node-letters) (node-cigs) (node-her-now))','form1 contains choices')
    require(act2,'(= form 2) (list (node-listen) (node-that-line) (node-strip) (node-workbench))','form2 removes item choice')
    require(fail,"(end-scenario '被赶出去)",'crowd failure is real')
    require(l,'(define lyon-stage-max 5)','progress cap')
    return {
      "source":"Official Scheme static contract only",
      "source_lengths":{"world":len(w),"alley":len(l)},
      "before_reveal":{"east_card_hides_endpoint":True,"west_card_hides_endpoint":True,
                       "east_endpoint_from_actual_story":"弗兰克","west_endpoint_from_actual_story":"莱恩",
                       "east_first_card_uses_skill":"social",
                       "west_first_card_uses_skill":"knowledge",
                       "pre_reveal_route_choice_has_public_skill_risk":True},
      "counter":{"item":ITEM,"cost":"1 unique packet","action_dice":0,"east_gallery_progress":2,
                 "public_subtitle_warns_packet_taken":True},
      "lyon_form_one":{"packet_required":1,"dice_cost":0,"progress":2,"one_time":True,
          "other_low_risk":"letters / knowledge: bad composure -1, mid progress +1, high +2",
          "other_high_risk":"nightingale now / social: bad composure -2 and crowd pressure+1, mid +2, high +3",
          "form_two_excludes_packet":True,"progress_goal":5,
          "crowd_zero_can_end_scenario":"被赶出去"}}

def mathematics():
    # Strictly paper examples with equal p for low/medium/high, invented.
    outcomes={"letters":((0,-1,0),(1,0,0),(2,0,0)),
              "her_now":((0,-2,1),(2,0,0),(3,0,0)),
              "packet":((2,0,0),)}
    cases=[]
    for remaining in (1,2,3,4,5):
        row={"remaining_progress":remaining}
        for act,results in outcomes.items():
            n=len(results)
            row[act]={"one_action_completion_prob":str(F(sum(p>=remaining for p,_,__ in results),n)),
                      "expected_progress":str(sum((F(p,n) for p,_,__ in results),F(0))),
                      "expected_composure_delta":str(sum((F(cp,n) for _,cp,__ in results),F(0))),
                      "expected_crowd_penalty":str(sum((F(cr,n) for _,__,cr in results),F(0))),
                      "action_dice":0 if act=="packet" else 1}
        cases.append(row)
    # Among equal success probabilities when one progress remains, the high-risk
    # move brings additional danger; no inferred universal domination when skill
    # distributions differ or progress exceeds the one-stage cap.
    one=cases[0]
    assert one["letters"]["one_action_completion_prob"]==one["her_now"]["one_action_completion_prob"]=="2/3"
    two=cases[1]
    assert two["letters"]["one_action_completion_prob"]=="1/3"
    assert two["her_now"]["one_action_completion_prob"]=="2/3"
    assert two["packet"]["one_action_completion_prob"]=="1"
    # Do NOT rescore current and future progress as fungible: counter East+2
    # and Lyon stage+2 are distinct goals and have no common official utility.
    decision=[]
    for immediate,future_bound in ((F(2),F(0)),(F(2),F(1)),(F(2),F(3)),(F(2),F(5))):
        # Assumption: q*later payoff unknown in [0,bound].
        now_dominates=immediate>=future_bound
        later_can_win=future_bound>immediate
        decision.append({"hypothetical_present_utility":str(immediate),
                         "hypothetical_future_value_upper_bound":str(future_bound),
                         "use_now_weakly_dominates_under_this_bound":now_dominates,
                         "possible_preference_reversal":later_can_win,
                         "minimax_regret_use_now":str(max(F(0),future_bound-immediate)),
                         "minimax_regret_keep":str(immediate)})
    assert any(x["possible_preference_reversal"] for x in decision)
    assert any(x["use_now_weakly_dominates_under_this_bound"] for x in decision)
    return {"identity":"independent conditional Fraction paper; invented equal three-outcome probabilities; not live skill odds nor human",
            "same_state_first_form_action_contrasts":cases,
            "unknown_future_example_bounds":decision,
            "knowledge_limit":"Later future payoff and its probability are not public at counter; author threshold e>=q*l is not itself a player-visible decision rule",
            "nonfungibility_limit":"East Gallery +2 and Lyon Form1 +2 are different progress clocks, never assumed same value."}

def main():
    r={"stage":"S3-Y Y1/Y2/Y3","static":audit(),"paper":mathematics(),
       "prior_native":"S3-X strict original world three fates already tested; no new original-world native proof"}
    text=json.dumps(r,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(text,encoding="utf-8")
    else:sys.stdout.write(text)

if __name__=="__main__":main()
