#!/usr/bin/env python3
"""R50 复核：用几条可理解的规则解释最优策略的动态价值。

枚举126种无序四骰，分布按 SSNoir FateStrip；行动骰可逐步择优。
所有数值为对两种结束成果(甲3值1/乙5值2)的期望效用。
不是正式C#会话/真人试玩；不得将策略枚举称为可玩性证明。
"""
from __future__ import annotations

import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement, product
from r50_recent_action import odds, initial_weight


def permitted(mode: str, a: int, b: int, previous: int, first: int) -> tuple[int, ...]:
    if mode == "always-A":
        return (0,)
    if mode == "always-B":
        return (1,)
    if previous == -1:
        return (first,)
    if mode == "alternate":
        return (1 - previous,)
    remaining = (3 - a) if previous == 0 else (5 - b)
    if mode == "finish-if-two":
        return (previous if remaining <= 2 else 1 - previous,)
    if mode == "finish-if-one":
        return (previous if remaining <= 1 else 1 - previous,)
    if mode == "value-close":
        if 5 - b <= 2:
            return (1,)
        if 3 - a <= 1:
            return (0,)
        return (1 - previous,)
    raise ValueError(mode)


def evaluate(skill: int) -> dict:
    @lru_cache(None)
    def solve(a: int, b: int, previous: int, dice: tuple[int, ...],
              mode: str, plan: tuple[int, ...], first: int = 1) -> F:
        if a >= 3:
            return F(1)
        if b >= 5:
            return F(2)
        if not dice:
            return F(0)
        goals = ((plan[0],) if mode == "fixed" else (0, 1) if mode == "free"
                 else permitted(mode, a, b, previous, first))
        values = []
        for ix, die in enumerate(dice):
            rest = dice[:ix] + dice[ix+1:]
            for goal in goals:
                mod = 0 if previous == -1 else 1 if goal != previous else -1
                next_plan = plan[1:] if mode == "fixed" else ()
                val = sum(p * solve(min(3, a + (gain if goal == 0 else 0)),
                                    min(5, b + (gain if goal == 1 else 0)),
                                    goal, rest, mode, next_plan, first)
                          for gain, p in enumerate(odds(die, skill, mod)))
                values.append(val)
        return max(values)

    scores = dict(free=F(0), fixed=F(0), understandable=F(0),
                  alternate=F(0), always_b=F(0))
    witnesses = []
    for dice in combinations_with_replacement(range(1, 7), 4):
        vfree = solve(0, 0, -1, dice, "free", ())
        vfixed = max(solve(0, 0, -1, dice, "fixed", order)
                     for order in product((0, 1), repeat=4))
        candidate_values = {
            "always-A": solve(0, 0, -1, dice, "always-A", ()),
            "always-B": solve(0, 0, -1, dice, "always-B", ()),
        }
        for mode in ("alternate", "finish-if-one", "finish-if-two", "value-close"):
            for first in (0, 1):
                candidate_values[f"{mode}-{first}"] = solve(
                    0, 0, -1, dice, mode, (), first)
        understandable = max(candidate_values.values())
        alternate = max(solve(0, 0, -1, dice, "alternate", (), first)
                        for first in (0, 1))
        w = initial_weight(dice)
        for name, value in (("free", vfree), ("fixed", vfixed),
                            ("understandable", understandable),
                            ("alternate", alternate),
                            ("always_b", candidate_values["always-B"])):
            scores[name] += w * value
        assert vfree >= max(vfixed, understandable)
        if vfree > understandable and len(witnesses) < 8:
            witnesses.append({"hand": list(dice), "free": str(vfree),
                              "best_rule": str(understandable),
                              "rule": max(candidate_values, key=candidate_values.get)})
    return {"skill": skill, "expectation": {k: float(v) for k, v in scores.items()},
            "exact": {k: str(v) for k, v in scores.items()},
            "remaining_gap_vs_rules": float(scores["free"]-scores["understandable"]),
            "remaining_gap_vs_fixed": float(scores["free"]-scores["fixed"]),
            "witnesses": witnesses}


def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--output")
    args = parser.parse_args()
    output = {"method": "full Fraction DP; normal four dice; interpretable policy family",
              "rules": [
                  "always-A / always-B",
                  "alternate, first A or B",
                  "stay if remaining to previous goal is at most 1, else switch",
                  "stay if remaining to previous goal is at most 2, else switch",
                  "complete B if <=2, else A if <=1, otherwise switch"
              ],
              "evidence": "independent mathematical simplified model only",
              "results": [evaluate(skill) for skill in (1, 2, 3)]}
    value = json.dumps(output, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        from pathlib import Path
        Path(args.output).write_text(value, encoding="utf-8")
    else:
        print(value)


if __name__ == "__main__":
    main()
