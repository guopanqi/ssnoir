#!/usr/bin/env python3
"""Exact counterfactuals for endogenous follow-up goals and non-dominated decoys.

Abstract research only: no human experience, official game engine or narrative.
Python standard library; four-day stochastic DP and deterministic three-action tree.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product, permutations


def followup(values=(4, 4, 5, 6), referral_reward=7, target=4, allow_referral=True):
    days = len(values)

    @lru_cache(None)
    def dp(day, progress, ticket):
        if day == days:
            return F(9 if progress >= target else 0)
        return (best(day, progress, ticket, True)[1] +
                best(day, progress, ticket, False)[1]) / 2

    def legal(day, progress, ticket, offer):
        yield "B", min(target, progress + 2), 0, 0
        if offer:
            yield "U", progress, 1 if allow_referral else 0, values[day]
        if ticket and allow_referral:
            yield "R", progress, 0, referral_reward

    def best(day, progress, ticket, offer):
        choices = [
            (action, F(reward) + dp(day + 1, next_progress, next_ticket))
            for action, next_progress, next_ticket, reward
            in legal(day, progress, ticket, offer)
        ]
        return max(choices, key=lambda x: x[1])

    def evaluate(rule):
        @lru_cache(None)
        def walk(day, progress, ticket):
            if day == days:
                return F(9 if progress >= target else 0)
            expected = []
            for offered in (True, False):
                actions = tuple(legal(day, progress, ticket, offered))
                available = tuple(a[0] for a in actions)
                decision = rule(day, progress, ticket, offered, available)
                selected = next((a for a in actions if a[0] == decision), None)
                assert selected is not None, (day, progress, ticket, offered, decision)
                _, p, t, r = selected
                expected.append(F(r) + walk(day + 1, p, t))
            return sum(expected) / 2
        return walk(0, 0, 0)

    return dp, best, legal, evaluate


def best_day_only_priorities(model, days=4):
    """A strong but readable policy: one priority list per day, using availability."""
    choices = tuple(permutations(("B", "U", "R")))
    best = (F(-1), None)
    for priority_by_day in product(choices, repeat=days):
        def rule(day, progress, ticket, offered, available):
            return next(a for a in priority_by_day[day] if a in available)
        score = model[3](rule)
        if score > best[0]:
            best = (score, priority_by_day)
    return best


def optimal_reachable_states(model, days=4):
    """All 2**days legitimate offer histories, following the resolved optimal rule."""
    _, best, legal, _ = model
    reached = set()
    for offers in product((False, True), repeat=days):
        progress = ticket = 0
        for day, offered in enumerate(offers):
            reached.add((day, progress, ticket, offered))
            action, _ = best(day, progress, ticket, offered)
            _, progress, ticket, _ = next(
                item for item in legal(day, progress, ticket, offered)
                if item[0] == action
            )
    return reached


def decoy(start_b=1, allow_decoy=True, auto_close=True):
    """3 action slots; AFTER action 2 the marked lane loses 2 progress.

    End result A>=2 pays 8; WORK_B making B>=2 closes early and pays 3
    if auto_close=True. WORK_B and DECOY_B mark the same enemy target.
    This is a conditional ENTRY STATE (B=0 or B=1), not a full campaign.
    """
    @lru_cache(None)
    def solve(t, a, b, last):
        if t == 3:
            return (8 if a >= 2 else 0, ())
        labels = ["WORK_A", "WORK_B"] + (
            ["DECOY_A", "DECOY_B"] if allow_decoy else [])
        choices = []
        for label in labels:
            na, nb = a, b
            mark = label[-1]
            if label == "WORK_A":
                na = min(2, na + 1)
            if label == "WORK_B":
                nb = min(2, nb + 1)
            if label == "WORK_B" and nb >= 2 and auto_close:
                choices.append((3, (label,)))
                continue
            if t == 1:
                if mark == "A":
                    na = max(0, na - 2)
                else:
                    nb = max(0, nb - 2)
            reward, tail = solve(t + 1, na, nb, mark)
            choices.append((reward, (label,) + tail))
        return max(choices, key=lambda x: x[0])

    return solve(0, 0, start_b, "A")


def main():
    model = followup()
    no_followup = followup(allow_referral=False)
    root = model[0](0, 0, 0)
    simple = best_day_only_priorities(model)
    no_ref = no_followup[0](0, 0, 0)
    print("F1 dynamic:", root, "strongest daily priority:", simple[0],
          "without follow-up objective:", no_ref)
    print("F1 priority policy:", simple[1])
    assert (root, simple[0], no_ref) == (F(159, 8), F(39, 2), F(131, 8))

    observed = optimal_reachable_states(model)
    assert len(observed) == 20
    # At day 4, B completed and current U offered: the same visible B progress
    # can accompany an earned R or no R, both with positive-probability histories.
    assert (3, 4, 1, True) in observed
    assert (3, 4, 0, True) in observed
    assert model[1](3, 4, 1, True)[0] == "R"
    assert model[1](3, 4, 0, True)[0] == "U"
    print("F1 reachable conditional switch: day4 B=4 offer=U, R-ticket 0=>U / 1=>R")

    for reward, expected_opt, expected_fixed in (
        (4, F(17), F(129, 8)),
        (9, F(177, 8), F(87, 4)),
    ):
        variant = followup(referral_reward=reward)
        optimal = variant[0](0, 0, 0)
        simple_value = best_day_only_priorities(variant)[0]
        print("F1 transfer reward", reward, "optimal", optimal,
              "fixed", simple_value)
        assert (optimal, simple_value) == (expected_opt, expected_fixed)

    for b in (0, 1):
        for auto in (False, True):
            with_decoy = decoy(b, True, auto)
            no_decoy = decoy(b, False, auto)
            print("D1 initial B:", b, "auto-close:", auto,
                  "with DECOY:", with_decoy, "without:", no_decoy)

    assert decoy(1, True, True) == (8, ("WORK_A", "DECOY_B", "WORK_A"))
    assert decoy(1, False, True)[0] == 3
    for b, auto in ((0, True), (0, False), (1, False)):
        assert decoy(b, True, auto)[0] == decoy(b, False, auto)[0] == 8
    # Full encounter variant: another participant completed 0/1 side-progress
    # before this three-slot segment; both public entry states have probability 1/2.
    mixed_decoy = (F(decoy(0, True, True)[0]) +
                   F(decoy(1, True, True)[0])) / 2
    mixed_no_decoy = (F(decoy(0, False, True)[0]) +
                      F(decoy(1, False, True)[0])) / 2
    print("D1 public exogenous partial-progress mixture:",
          mixed_decoy, "vs", mixed_no_decoy)
    assert (mixed_decoy, mixed_no_decoy) == (8, F(11, 2))
    print("PASS: exact finite witnesses and counterfactual controls")


if __name__ == "__main__":
    main()
