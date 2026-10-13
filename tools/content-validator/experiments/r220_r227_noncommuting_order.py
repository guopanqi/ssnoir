#!/usr/bin/env python3
"""R220-R227: sequence order as a player decision, not a resource allocation.

Two noncommuting instructions must both be used exactly once. The requested
result becomes public before the first instruction; all probability statements
are exact. Independent explicit permutations + mathematical order witnesses.
"""
import json
from itertools import permutations
from fractions import Fraction as F

def apply(op,x):
    if op=="+1":return x+1
    if op=="-1":return x-1
    if op=="+2":return x+2
    if op=="*2":return 2*x
    raise ValueError(op)

def output(start,plan):
    value=start
    for op in plan:value=apply(op,value)
    return value

def all_orders(start,inventory):
    assert len(inventory)==2 and inventory[0]!=inventory[1]
    # IMPORTANT: both plans consume exactly the same inventory.
    return {" > ".join(plan):output(start,plan)
            for plan in permutations(inventory)}

def best_fixed_probability(start,inventory,targets):
    routes=all_orders(start,inventory)
    return max(sum(pr*int(out==goal) for goal,pr in targets.items())
               for out in routes.values())

def best_feedback_probability(start,inventory,targets):
    routes=all_orders(start,inventory)
    return sum(pr*int(any(out==goal for out in routes.values()))
               for goal,pr in targets.items())

def independently_composed(start,inventory):
    # Two functions are applied in the two possible syntactic compositions.
    left=apply(inventory[1],apply(inventory[0],start))
    right=apply(inventory[0],apply(inventory[1],start))
    return (left,right)

def audit():
    main_start=2
    inventory=("+1","*2")
    targets={5:F(1,2),6:F(1,2)}
    orders=all_orders(main_start,inventory)
    assert set(orders.values())=={5,6}
    assert set(orders.values())==set(independently_composed(main_start,inventory))
    informed=best_feedback_probability(main_start,inventory,targets)
    blind=best_fixed_probability(main_start,inventory,targets)
    assert (informed,blind)==(1,F(1,2))
    # Knowing the request only AFTER the first operation is too late to
    # change the order: the second operation is then forced by inventory.
    reveal_after_first=blind
    commute=all_orders(2,("+1","+2"))
    assert set(commute.values())=={5} and len(set(commute.values()))==1
    commute_informed=best_feedback_probability(2,("+1","+2"),targets)
    assert commute_informed==F(1,2)
    # An independent transfer task: new start, new operation language.
    transfer=all_orders(3,("-1","*2"))
    assert set(transfer.values())=={4,5}
    transfer_targets={4:F(1,2),5:F(1,2)}
    assert best_feedback_probability(3,("-1","*2"),transfer_targets)==1
    assert best_fixed_probability(3,("-1","*2"),transfer_targets)==F(1,2)
    # Three complete repeated episodes with fresh targets (no extra progress
    # or scoring rule) just repeat the same short "order based on target".
    # Independence lets us exactly multiply probabilities.
    episodes=3
    triple_feedback=informed**episodes
    triple_fixed=blind**episodes
    assert triple_feedback==1 and triple_fixed==F(1,8)
    return {
      "schema":"ssnoir.r220-r227.noncommuting-order.v1",
      "evidence":"complete both permutations + independent operator composition, exact target probabilities",
      "rules":{"input":"start 2; each operator used exactly once",
               "inventory":["+1","*2"],"request":"desired output 5 or 6, 50/50",
               "when_visible":"before choosing the order",
               "choice":"sequence the same two operations, not acquire/allocate more operations"},
      "R220_two_orders_same_inventory":orders,
      "R221_request_public_before_order":{"adaptive":str(informed),"best_fixed":str(blind)},
      "R222_request_after_first_operation":{"best":str(reveal_after_first),"gap_from_timely":str(informed-reveal_after_first)},
      "R223_commutative_control":{"routes":commute,"best_adaptive":str(commute_informed)},
      "R224_transferred_operator_and_input":{"routes":transfer,"adaptive":1,"fixed":str(F(1,2))},
      "R225_order_changes_result_without_changing_inventory":list(independently_composed(main_start,inventory)),
      "R226_three_independent_episodes":{"all_correct_with_public_request":str(triple_feedback),
                  "best_precommitted_sequences_all_correct":str(triple_fixed)},
      "R227_novelty_boundary":"Operation order is irreducible to resource quantity for fixed inventory, and timely request matters. But a single if-target-5-reverse rule solves every episode; evidence is a minimal ordering puzzle, not a certified deep programmable game."
    }
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
