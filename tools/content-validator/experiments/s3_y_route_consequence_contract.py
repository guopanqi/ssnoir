#!/usr/bin/env python3
"""S3-Y source-level audit of hidden destination and companion/Frank timing.

This is NOT a native session or human comprehension test. It checks the exact
Scheme conditions that govern two routes, post-West Frank, partial progress
credit and Nightingale departure. Changing the authored story requires
updating this research contract, not quietly keeping a stale conclusion.
"""
import json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
WORLD=ROOT/"UnityClient/Assets/Resources/Content/scenes/world/三封信.scm"
def require(s,fragment,label):
    assert fragment in s,(label,fragment)
def main():
    s=WORLD.read_text(encoding="utf-8")
    # Both East and West become playable while Nightingale guides, before Ryan.
    require(s,'(or (warren-open?) (> (east-entry-clk \'current) 0))','East appears when guidance active')
    require(s,'(or (warren-open?) (> (west-stairs-clk \'current) 0))','West appears when guidance active')
    require(s,'(and (not (east-porch-done?))\n                    (not bridge-identified?)','East opening blocked by Ryan result')
    require(s,'(and (east-porch-done?) (not (east-route-done?)) (not bridge-identified?))','East deeper route blocked')
    # West result does NOT permanently remove Frank as person, only guided East.
    require(s,'(if (and bridge-identified? (not union-checked?))\n               (list (node-search-for-frank))','Solo Frank path follows Ryan')
    require(s,'(if (east-porch-done?) (frank-search-clk \'advance! 2) #f)','partial completed-porch credit')
    require(s,'(frank-search-clk \'advance! n)','Frank remains attainable by later action')
    require(s,'(frank \'meet!)','Frank relation can be established')
    require(s,'(set! union-checked? #t)','Frank completion recorded')
    # Distinguish what is observable: East/West route subtitles don't reveal endpoints.
    for text in ['"她把首饰摘了，换了件旧外套"',
                 '"住户共用的地盘，规矩比人情硬。她记得，你得听懂"']:
        require(s,text,"route's pre-reveal subtitle")
    # Timing hypothesis: a solo-Frank subtitle could appear when Ryan is
    # revealed, while companion departure is a later end-turn rule.
    require(s,':subtitle "她已经走了，这一趟只有你自己"','solo Frank subtitle')
    require(s,'(define-turn-rule "离开老街后谈起莱恩"','deferred companion dialogue')
    require(s,"(dismiss-companion! '夜莺)",'companion dismissal on dialogue')
    require(s,'(and bridge-identified? (not (has-flag? \'她说起莱恩)))','deferred dismissal guard')
    # This file documents a potential timing mismatch; without observing
    # actual UI states we DO NOT assert that the player ever sees the text
    # while Nightingale is still companion.
    report={
      "identity":"exact Scheme source-guard review, no native or human validation",
      "preceding_information":{"east_destination_known_to_player":False,
              "west_destination_known_to_player":False,
              "first_route_selection_grounded_in":"visible entrance text / skill / character interest, not hidden final names"},
      "after_west_identity":{"guided_east_cards_disappear":True,
              "frank_relation_permanently_missed":False,
              "solo_frank_search_available_when_not_met":True,
              "only_complete_east_porch_grants_two_progress_in_new_search":True,
              "partial_east_gallery_does_not_convert_to_solo_search":True},
      "deferred_dialogue":{"nightingale_dismissal_is_in_followup_turn_rule":True,
              "solo_frank_card_subtitle_preemptively_says_she_left":True,
              "potential_ui_timing_mismatch_needs_native_observation":True,
              "asserted_live_bug":False},
      "structure":"West success removes a shared investigation style, not the possibility of ever meeting Frank."}
    output=json.dumps(report,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(output,encoding="utf-8")
    else:sys.stdout.write(output)
if __name__=="__main__":main()
