#!/usr/bin/env python3
"""R54: auditable optimal-policy forward path audit, distinct from root-value DP.

Report exact probability mass of a specific *behavior* rather than assuming that
free-vs-fixed payoff gaps prove responsive adversaries matter. All randomness is
standard FateStrip d6 + initial four-die hands; no human subjects.

Policy tie-break: among value-optimal actions, prefer B target, then last/highest
remaining die. Opponent attacks higher-progress lane on actions 2 and 4; ties A.
"""
from __future__ import annotations

import argparse
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement

from r50_recent_action import odds, initial_weight


def analyze(skill: int) -> dict:
    @lru_cache(None)
    def value(a: int, b: int, turn: int, hand: tuple[int, ...]) -> F:
        if a >= 3:
            return F(1)
        if b >= 5:
            return F(2)
        if not hand:
            return F(0)
        return max(action_value(a, b, turn, hand, goal, i)
                   for goal in (0, 1) for i in range(len(hand)))

    def action_value(a: int, b: int, turn: int, hand: tuple[int, ...],
                     goal: int, index: int) -> F:
        die = hand[index]
        rest = hand[:index] + hand[index + 1:]
        expectation = F(0)
        for gain, probability in enumerate(odds(die, skill, 0)):
            aa = min(3, a + (gain if goal == 0 else 0))
            bb = min(5, b + (gain if goal == 1 else 0))
            if aa < 3 and bb < 5 and turn in (1, 3):
                if aa >= bb:
                    aa = max(0, aa - 2)
                else:
                    bb = max(0, bb - 2)
            expectation += probability * value(aa, bb, turn + 1, rest)
        return expectation

    @lru_cache(None)
    def forward(a: int, b: int, turn: int, hand: tuple[int, ...],
                diverted: bool, positive_counterfactual: bool) -> tuple[F, F, F, F]:
        if a >= 3:
            return F(diverted), F(positive_counterfactual), F(0), F(0)
        if b >= 5:
            return F(diverted), F(positive_counterfactual), F(diverted), F(1)
        if not hand:
            return F(diverted), F(positive_counterfactual), F(0), F(0)

        candidates = [(action_value(a, b, turn, hand, target, index), target, index)
                      for target in (0, 1) for index in range(len(hand))]
        best = max(row[0] for row in candidates)
        _, goal, index = max(row for row in candidates if row[0] == best)
        remaining = hand[:index] + hand[index + 1:]
        accum = [F(0), F(0), F(0), F(0)]

        for gain, probability in enumerate(odds(hand[index], skill, 0)):
            aa = min(3, a + (gain if goal == 0 else 0))
            bb = min(5, b + (gain if goal == 1 else 0))
            diverted_now = False
            beneficial_now = False
            if aa < 3 and bb < 5 and turn in (1, 3):
                if aa >= bb:
                    # Only diversion after ACTION TWO can protect subsequent decisions.
                    diverted_now = turn == 1 and aa > 0 and bb > 0
                    if diverted_now:
                        hit_a = value(max(0, aa - 2), bb, turn + 1, remaining)
                        hypothetical_hit_b = value(aa, max(0, bb - 2),
                                                   turn + 1, remaining)
                        beneficial_now = hit_a > hypothetical_hit_b
                    aa = max(0, aa - 2)
                else:
                    bb = max(0, bb - 2)
            result = forward(aa, bb, turn + 1, remaining,
                             diverted or diverted_now,
                             positive_counterfactual or beneficial_now)
            for k in range(4):
                accum[k] += probability * result[k]
        return tuple(accum)

    means = [F(0), F(0), F(0), F(0)]
    for hand in combinations_with_replacement(range(1, 7), 4):
        outcomes = forward(0, 0, 0, hand, False, False)
        for i, fraction in enumerate(outcomes):
            means[i] += initial_weight(hand) * fraction
    assert F(0) < means[1] <= means[0] <= 1
    assert F(0) < means[2] <= means[0]
    assert means[2] <= means[3]
    names = ("attack_diverted_to_A_while_B_has_progress",
             "diversion_strictly_better_than_hypothetical_hit_B",
             "diversion_then_B_completion", "all_B_completions")
    return {"skill": skill, "exact_weighted_initial_hands": 126,
            "root_value": str(sum(initial_weight(hand) * value(0, 0, 0, hand)
                                  for hand in combinations_with_replacement(range(1, 7), 4))),
            "probability_masses": {
                key: {"exact": str(v), "decimal": round(float(v), 9)}
                for key, v in zip(names, means)},
            "policy_tie_break": "optimal expected payoff, then B before A, then highest die",
            "scope": "mathematical model; chosen optimal policy paths, not player behavior"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output")
    args = ap.parse_args()
    result = {"hypothesis": "Can A's public progress divert the hit and preserve B?",
              "results": [analyze(1), analyze(2)]}
    payload = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        from pathlib import Path
        Path(args.output).write_text(payload, encoding="utf-8")
    else:
        print(payload)


if __name__ == "__main__":
    main()
