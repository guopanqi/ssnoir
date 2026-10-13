#!/usr/bin/env python3
"""NEG-01/02/03: credible compensation, contingent renegotiation and private types.

All figures exact Fraction. Each party maximizes its own payoff at its actual
information set. This is abstract research, NOT a human/NPC-simulation result.
"""
from fractions import Fraction as F
from itertools import product

GROSS = F(12)
SUCCESS = F(1, 2)
PARTNER_OUTSIDE = F(2)
PLAYER_FALLBACK = F(2)


def credible_compensation(outside=PARTNER_OUTSIDE):
    """Partner must choose whether to perform service before the 50% outcome.

    A verbal future payment is unenforceable after the player's benefit arrives.
    UPFRONT/ESCROW both bind at signature and specify that WORK must be done;
    neither lets the partner take the payment and unilaterally leave.
    """
    verbally_promised = outside / SUCCESS
    # At payoff time a selfish player selects DEFAULT (payment=0).
    player_best_end_payment = F(0)
    assert player_best_end_payment < verbally_promised
    # Partner anticipates DEFAULT; positive outside benefit forces refusal.
    assert player_best_end_payment * SUCCESS < outside
    verbal = (PLAYER_FALLBACK, outside)
    # Conditional escrow locks 2*outside at signing and pays only on success.
    escrow_gross = outside / SUCCESS
    escrow = (SUCCESS * (GROSS - escrow_gross), outside)
    # Fair upfront binding transfer pays the same expected sum.
    upfront = (SUCCESS * GROSS - outside, outside)
    assert escrow == upfront
    return {"VERBAL": verbal, "ESCROW": escrow, "UPFRONT": upfront,
            "locked_capital": escrow_gross}


def revealed_outside(renegotiation_cost=F(1), offer_prices=(F(2), F(4))):
    """Two-sided observed outside signal arrives after initial posted offer.

    Worker accepts iff w>=outside, equality accepted; player may revise posted
    wage once, paying renegotiation_cost, then worker responds strategically.
    """
    types = (F(2), F(4))
    outcome = {}
    for first in offer_prices:
        branches = {}
        for outside in types:
            legal = [(("HOLD", first), GROSS-first if first >= outside else F(0))]
            for revised in offer_prices:
                if revised != first:
                    legal.append((("REVISE", revised),
                                  GROSS-revised-renegotiation_cost
                                  if revised >= outside else -renegotiation_cost))
            action, profit = max(legal, key=lambda x: x[1])
            branches[outside] = (profit, action)
        outcome[first] = (sum(x[0] for x in branches.values())/2, branches)
    strong_static = max(sum(GROSS-w if w >= out else F(0) for out in types)/2
                        for w in offer_prices)
    contingent_full = sum(GROSS-out for out in types)/2
    best = max(outcome.values(), key=lambda x: x[0])[0]
    return {"start": outcome, "best_renegotiation": best,
            "best_static": strong_static,
            "best_verifiable_state_contract": contingent_full}


def private_outside_strategic(cost=F(1)):
    """The posted low salary is public; partner privately knows outside 2/4.

    If w0=2, proposer may promise/plan to raise it to 4 after a rejection.
    Anticipating a raise, even the low-outside partner rejects the initial 2.
    A low type accepting the first offer is NOT incentive compatible.
    """
    low, high = F(2), F(4)
    assert low < high
    initial_low = low
    second = high
    low_accept_initial_utility = initial_low
    low_reject_expected_utility = second
    assert low_reject_expected_utility > low_accept_initial_utility
    # Both types reject first offer 2, so rejection no longer identifies high.
    pooled_rejection_payoff = GROSS-second-cost
    # If the player falsely assumed low would accept 2, their prediction:
    naive_profit = ((GROSS-low)+(GROSS-second-cost))/2
    # Offer 4 on the first turn: both types accept (equality accepted).
    pooled_first_offer_profit = GROSS-high
    # A refusal that forces outside exit instead of a revision:
    # low is indifferent accept2/outside2, high exits -> just half of 10.
    no_revision_credible_profit = (GROSS-low)/2
    # Costless verifiable type: can post 2 for low and 4 for high before action.
    verified_complete_menu_profit = ((GROSS-low)+(GROSS-high))/2
    assert pooled_rejection_payoff == F(7)
    assert naive_profit == F(17, 2)
    assert pooled_first_offer_profit == F(8)
    assert no_revision_credible_profit == F(5)
    assert verified_complete_menu_profit == F(9)
    assert pooled_first_offer_profit > pooled_rejection_payoff
    return {
        "naive_low_acceptance_prediction": naive_profit,
        "actual_pool_reject_then_counter": pooled_rejection_payoff,
        "strategic_best_initial_pool": pooled_first_offer_profit,
        "credible_no-counter_threat_lower_value": no_revision_credible_profit,
        "verifiably_contingent_ideal": verified_complete_menu_profit,
    }


def main():
    p = credible_compensation()
    print("NEG-01", {k: tuple(map(str,v)) if isinstance(v,tuple) else str(v)
                     for k,v in p.items()})
    assert p["VERBAL"] == (F(2), F(2))
    assert p["ESCROW"] == p["UPFRONT"] == (F(4),F(2))
    assert p["locked_capital"] == 4
    assert credible_compensation(outside=F(4))["ESCROW"] == (
        F(2), F(4))

    n2 = revealed_outside()
    print("NEG-02", {str(w): (str(v[0]),
                         {str(k):(str(x[0]),x[1]) for k,x in v[1].items()})
                    for w,v in n2["start"].items()})
    assert n2["best_static"] == F(8)
    assert n2["best_renegotiation"] == F(17,2)
    assert n2["best_verifiable_state_contract"] == F(9)
    assert n2["start"][F(2)][1][F(4)] == (F(7),("REVISE",F(4)))
    assert n2["start"][F(4)][1][F(2)] == (F(9),("REVISE",F(2)))
    # Delayed cost is the entire gap to a precommitted complete contingent menu.
    zero_cost = revealed_outside(renegotiation_cost=F(0))
    assert zero_cost["best_renegotiation"] == F(9)
    assert zero_cost["best_verifiable_state_contract"] == F(9)

    n3 = private_outside_strategic()
    print("NEG-03", {k:str(v) for k,v in n3.items()})
    assert n3["naive_low_acceptance_prediction"] > n3["strategic_best_initial_pool"]
    assert n3["strategic_best_initial_pool"] == F(8)
    # Regime fixed: the private partner sees both offers and maximizes its own
    # wage; replacing him with a naive threshold bot invents extra utility.
    print("PASS credible offers, renegotiation, and strategic rejection controls")


if __name__ == "__main__":
    main()
