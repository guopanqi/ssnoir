#!/usr/bin/env python3
"""S3-W bounded analytical counterfactual: exactly one item, two later contexts.

Numbers below are NOT official game payoffs or empirical future probabilities.
The official content happens to advance two different clocks by 2 but their
utility/urgency is not interchangeable. This is an exact illustrative bound.
"""
from fractions import Fraction as F
import json,sys
from pathlib import Path
from itertools import product

def solve(early, late, chance):
    early,late,chance=F(early),F(late),F(chance)
    assert 0<=chance<=1 and early>=0 and late>=0
    consume_now=early
    reserve=chance*late
    baseline=max(consume_now,reserve)
    public_rule="现在交出" if consume_now>=reserve else "留到以后"
    # Thought experiment: signal perfectly reveals later visit BEFORE acting.
    clairvoyant=(1-chance)*early+chance*max(early,late)
    # With 2 independent copies the either/or competition disappears.
    two_copies=early+chance*late
    # No later use -> simple early if present; no early -> reserve if later.
    assert baseline>=early and baseline>=reserve
    assert clairvoyant>=baseline and two_copies>=baseline
    return {"early":str(early),"late":str(late),"future_chance":str(chance),
       "simple_public_policy":public_rule,
       "consume_early":str(consume_now),"save_for_later":str(reserve),
       "single_copy_optimum":str(baseline),
       "perfect_future_signal":str(clairvoyant),
       "signal_marginal":str(clairvoyant-baseline),
       "two_independent_copies":str(two_copies),
       "no_later_use":str(early),"no_early_use":str(chance*late)}
def main():
    samples=[
        solve(2,2,F(1,2)),  # both clocks 2 but no special future value
        solve(2,4,F(1,4)),
        solve(2,4,F(1,2)),
        solve(2,4,F(3,4)),
        solve(2,1,F(1)),
    ]
    assert samples[0]["simple_public_policy"]=="现在交出"
    assert samples[0]["signal_marginal"]=="0"
    assert samples[1]["simple_public_policy"]=="现在交出"
    assert samples[3]["simple_public_policy"]=="留到以后"
    assert samples[2]["simple_public_policy"]=="现在交出" # tie
    assert samples[2]["signal_marginal"]=="1"
    report={"identity":"Pure symbolic two-decision EXAMPLE, not actual SSNoir rewards or random chances",
      "single_option_rule":"Spend now iff marginal_early >= P(future accessible) * marginal_late",
      "value_of_secret_information":"Only an omniscient author can use the perfect future signal; players have no such knowledge",
      "controls":["delete late use: spend now weakly dominates",
       "delete early use: reserve",
       "two copies: consume in both places and conflict disappears",
       "make outcome known before first choice: no exploration uncertainty"],
      "cases":samples,"scope":"Illustrative paper inequalities, not native/probability parity and not human engagement."}
    payload=json.dumps(report,ensure_ascii=False,indent=2)+"\n"
    if len(sys.argv)>1:Path(sys.argv[1]).write_text(payload,encoding="utf8")
    else:print(payload)
if __name__=="__main__":main()
