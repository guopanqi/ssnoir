#!/usr/bin/env python3
"""R51–R54: bounded discovery screen of distinct abstract decision structures.

Each experiment uses four d6 action dice, real FateStrip(placedDie, skill, modifier)
from the shared R50 probability helper, exact rational DP and all 126 weighted hands.
No player telemetry, game scene, emotional conclusion, or random Monte Carlo results.

R51: exogenous target window, stronger roll on one randomly favored route.
R52: short side goal grants one new random die on completion.
R53: strong action increases heat, safe action cools; heat penalizes next strong action.
R54: opponent hits whichever lane has more visible progress after actions 2 and 4.
All comparisons use a strong precommitted policy that may *still choose dice adaptively*.
"""
from __future__ import annotations

import argparse
import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement, product

from r50_recent_action import odds, initial_weight

HANDS = tuple(combinations_with_replacement(range(1, 7), 4))


def distribute(hand: tuple[int, ...], index: int) -> tuple[int, ...]:
    return hand[:index] + hand[index + 1:]


def number(value: F) -> dict:
    return {"decimal": round(float(value), 9), "exact": str(value)}


def r51(skill: int, bonus: int = 1) -> dict:
    """Window A/B is revealed before each move and resampled independently."""
    @lru_cache(None)
    def continuation(a: int, b: int, fav: int, dice: tuple[int, ...],
                     policy: str, plan: tuple[int, ...]) -> F:
        if a >= 3:
            return F(1)
        if b >= 5:
            return F(2)
        if not dice:
            return F(0)
        goals = (plan[0],) if policy == "fixed" else (fav,) if policy == "follow" else (0, 1)
        options = []
        for target in goals:
            for i, die in enumerate(dice):
                rest = distribute(dice, i)
                next_plan = plan[1:] if policy == "fixed" else ()
                options.append(sum(
                    prob * continuation(min(3, a + (gain if target == 0 else 0)),
                                        min(5, b + (gain if target == 1 else 0)),
                                        next_fav, rest, policy, next_plan) / 2
                    for gain, prob in enumerate(odds(die, skill, bonus if fav == target else 0))
                    for next_fav in (0, 1)))
        return max(options)

    avg = dict.fromkeys(("free", "fixed", "follow"), F(0))
    for hand in HANDS:
        for fav in (0, 1):
            outcomes = {
                "free": continuation(0, 0, fav, hand, "free", ()),
                "fixed": max(continuation(0, 0, fav, hand, "fixed", order)
                             for order in product((0, 1), repeat=4)),
                "follow": continuation(0, 0, fav, hand, "follow", ())
            }
            assert outcomes["free"] >= outcomes["fixed"] and outcomes["free"] >= outcomes["follow"]
            for k, v in outcomes.items():
                avg[k] += initial_weight(hand) * v / 2
    return {"skill": skill, "window_bonus": bonus,
            "expected_reward": {k: number(v) for k, v in avg.items()},
            "gap_over_strong_fixed": number(avg["free"]-avg["fixed"])}


def r52(skill: int, reward_die: bool) -> dict:
    """A is a two-step side goal, B five steps; values 1 and 2 are additive.
    Completing A once grants a random d6 into remaining action hand.
    """
    @lru_cache(None)
    def continuation(a: int, b: int, dice: tuple[int, ...],
                     policy: str, plan: tuple[int, ...]) -> F:
        if not dice or a >= 2 and b >= 5:
            return F((a >= 2) + 2 * (b >= 5))
        if policy == "free":
            goals = (0, 1)
        elif policy == "A-first":
            goals = (0,) if a < 2 else (1,)
        elif policy == "B-first":
            goals = (1,) if b < 5 else (0,)
        else:
            goals = (plan[0],) if plan else (0, 1)
        opts = []
        for goal in goals:
            if goal == 0 and a >= 2 or goal == 1 and b >= 5:
                continue
            for di, die in enumerate(dice):
                rest = distribute(dice, di)
                value = F(0)
                for gain, probability in enumerate(odds(die, skill)):
                    na = min(2, a + (gain if goal == 0 else 0))
                    nb = min(5, b + (gain if goal == 1 else 0))
                    tail = (tuple(g for g in plan[1:] if g == 0 and na < 2 or g == 1 and nb < 5)
                            if policy == "fixed" else ())
                    if reward_die and a < 2 and na == 2:
                        value += probability * sum(
                            continuation(na, nb, tuple(sorted(rest + (new_die,))), policy, tail)
                            for new_die in range(1, 7)) / 6
                    else:
                        value += probability * continuation(na, nb, rest, policy, tail)
                opts.append(value)
        return max(opts) if opts else F((a >= 2) + 2 * (b >= 5))

    average = dict.fromkeys(("free", "A-first", "B-first", "fixed"), F(0))
    for hand in HANDS:
        results = {
            "free": continuation(0, 0, hand, "free", ()),
            "A-first": continuation(0, 0, hand, "A-first", ()),
            "B-first": continuation(0, 0, hand, "B-first", ()),
            "fixed": max(continuation(0, 0, hand, "fixed", plan)
                         for plan in product((0, 1), repeat=5)),
        }
        assert results["free"] >= max(results.values())
        for k, v in results.items():
            average[k] += initial_weight(hand) * v
    return {"skill": skill, "bonus_die_at_A_completion": reward_die,
            "expected_reward": {k: number(v) for k, v in average.items()},
            "gap_over_strong_fixed": number(average["free"]-average["fixed"])}


def r53(skill: int, heat_penalty: int = 1) -> dict:
    """A public heat gauge: burst +3 on good but adds one heat and takes
    -heat*penalty prepared value; safe +2 on good, reduces heat by one.
    Each action consumes exactly one die; reach six progress within four dice.
    """
    @lru_cache(None)
    def continuation(progress: int, heat: int, hand: tuple[int, ...],
                     policy: str, plan: tuple[int, ...], first: int = 0) -> F:
        if progress >= 6:
            return F(1)
        if not hand:
            return F(0)
        if policy == "fixed":
            actions = (plan[0],)
        elif policy == "heat":
            actions = (1 if heat == 0 else 0,)
        elif policy == "alternate":
            actions = (int(len(hand) % 2 == first),)
        else:
            actions = (0, 1)
        options = []
        for action in actions:
            incs = (0, 1, 3) if action else (0, 1, 2)
            new_heat = min(3, heat + 1) if action else max(0, heat - 1)
            modifier = -heat_penalty * heat if action else 0
            for i, die in enumerate(hand):
                options.append(sum(
                    probability * continuation(min(6, progress + increment),
                                               new_heat, distribute(hand, i),
                                               policy, plan[1:] if policy == "fixed" else (), first)
                    for increment, probability in zip(incs, odds(die, skill, modifier))))
        return max(options)

    means = dict.fromkeys(("free", "fixed", "heat", "alternate"), F(0))
    for hand in HANDS:
        values = {
            "free": continuation(0, 0, hand, "free", ()),
            "fixed": max(continuation(0, 0, hand, "fixed", plan)
                         for plan in product((0, 1), repeat=4)),
            "heat": continuation(0, 0, hand, "heat", ()),
            "alternate": max(continuation(0, 0, hand, "alternate", (), f) for f in (0, 1)),
        }
        assert values["free"] >= max(values.values())
        for k, v in values.items():
            means[k] += initial_weight(hand) * v
    return {"skill": skill, "heat_penalty": heat_penalty,
            "completion_probability": {k: number(v) for k, v in means.items()},
            "gap_over_strong_fixed": number(means["free"] - means["fixed"])}


def r54(skill: int, opponent: str = "reactive", hit: int = 2) -> dict:
    """A3 rewarded 1 / B5 rewarded 2, any completion ends immediately.
    Attacks after actions 2 and 4 if unresolved; target larger current progress,
    tie attacks A. Controls attack always A or always B with same cost.
    """
    @lru_cache(None)
    def continuation(a: int, b: int, turns: int, hand: tuple[int, ...],
                     policy: str, plan: tuple[int, ...]) -> F:
        if a >= 3:
            return F(1)
        if b >= 5:
            return F(2)
        if not hand:
            return F(0)
        goals = ((0, 1) if policy == "free" else
                 (plan[0],) if policy == "fixed" else
                 (0,) if policy == "always-A" else (1,))
        options = []
        for goal in goals:
            for i, die in enumerate(hand):
                rest = distribute(hand, i)
                expectation = F(0)
                for gain, probability in enumerate(odds(die, skill)):
                    na = min(3, a + (gain if goal == 0 else 0))
                    nb = min(5, b + (gain if goal == 1 else 0))
                    if na < 3 and nb < 5 and turns in (1, 3):
                        lane = (0 if na >= nb else 1) if opponent == "reactive" else (
                            0 if opponent == "always-A" else 1)
                        if lane == 0:
                            na = max(0, na-hit)
                        else:
                            nb = max(0, nb-hit)
                    expectation += probability * continuation(
                        na, nb, turns + 1, rest, policy, plan[1:] if policy == "fixed" else ())
                options.append(expectation)
        return max(options)

    averages = dict.fromkeys(("free", "fixed", "always-A", "always-B"), F(0))
    strict_roots = 0
    for hand in HANDS:
        values = {
            "free": continuation(0, 0, 0, hand, "free", ()),
            "fixed": max(continuation(0, 0, 0, hand, "fixed", plan)
                         for plan in product((0, 1), repeat=4)),
            "always-A": continuation(0, 0, 0, hand, "always-A", ()),
            "always-B": continuation(0, 0, 0, hand, "always-B", ())
        }
        assert values["free"] >= max(values.values())
        strict_roots += values["free"] > values["fixed"]
        for k, v in values.items():
            averages[k] += initial_weight(hand) * v
    return {"skill": skill, "opponent_target": opponent, "hit": hit,
            "strict_initial_hands": strict_roots,
            "expected_reward": {k: number(v) for k, v in averages.items()},
            "gap_over_strong_fixed": number(averages["free"] - averages["fixed"])}


def screen():
    results = {
        "model": "Four standard dice; weighted 126 unordered initial hands; exact Fraction DP; frozen abstract variants",
        "identity": "MATHEMATICAL_ONLY: no C# session and no human playtest in these numbers",
        "R51_external_window": [r51(s, 1) for s in (1, 2)],
        "R52_completion_rebate": [r52(s, flag) for s in (1, 2) for flag in (False, True)],
        "R53_reversible_heat": [r53(s, 1) for s in (1, 2)],
        "R54_reactive_opponent": [r54(s, control) for s in (1, 2)
                                  for control in ("reactive", "always-A", "always-B")]
    }
    for row in results["R52_completion_rebate"]:
        if row["bonus_die_at_A_completion"]:
            assert row["gap_over_strong_fixed"]["exact"] == "0", "Bonus-side path no longer universally optimal"
    return results


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", help="UTF-8 JSON evidence path")
    args = ap.parse_args()
    payload = json.dumps(screen(), ensure_ascii=False, indent=2) + "\n"
    if args.output:
        from pathlib import Path
        Path(args.output).write_text(payload, encoding="utf-8")
    else:
        print(payload)


if __name__ == "__main__":
    main()
