#!/usr/bin/env python3
"""R44独立字面概率小树。只用本文件数据，不读取父模型。"""
import hashlib
import itertools
import json
import time
from fractions import Fraction as F
from pathlib import Path

HERE = Path(__file__).resolve().parent
# 技能1，每颗骰对应六个等概率命运面；0/1/2是坏/中/好。
FACES = {
    1: (0, 0, 1, 1, 1, 2),
    2: (0, 1, 1, 1, 2, 2),
    3: (0, 1, 1, 2, 2, 2),
    4: (1, 1, 2, 2, 2, 2),
    5: (1, 2, 2, 2, 2, 2),
    6: (2, 2, 2, 2, 2, 2),
}


def reward(needs, bundle):
    n = needs.count(0)
    return (0, 1, bundle)[n]


def actions(needs, hand, family="free", risk_only=False):
    targets = [t for t in range(2) if needs[t] > 0]
    if family == "short" and targets:
        least = min(needs[t] for t in targets)
        targets = [t for t in targets if needs[t] == least]
    elif family in ("A_first", "B_first") and targets:
        pref = 0 if family == "A_first" else 1
        targets = [pref if needs[pref] else 1 - pref]
    return [(t, d, mode) for t in targets for d in sorted(set(hand))
            for mode in (("risk",) if risk_only else ("risk", "steady"))]


def move(needs, t, grade, mode):
    gain = grade if mode == "risk" else int(grade == 2)
    out = list(needs)
    out[t] = max(0, out[t] - gain)
    return tuple(out)


def action_value(needs, hand, bundle, action, family, risk_only):
    t, die, mode = action
    rest = list(hand)
    rest.remove(die)
    # 字面枚举第一颗骰的六面，再独立求下一颗骰。
    return sum((value(move(needs, t, grade, mode), tuple(rest), bundle,
                      family, risk_only) for grade in FACES[die]), F(0)) / 6


def action_values(needs, hand, bundle, family="free", risk_only=False):
    return {a: action_value(needs, hand, bundle, a, family, risk_only)
            for a in actions(needs, hand, family, risk_only)}


def value(needs, hand, bundle, family="free", risk_only=False):
    if not hand or needs == (0, 0):
        return F(reward(needs, bundle))
    av = action_values(needs, hand, bundle, family, risk_only)
    return max(av.values())


def completion(need, dice):
    # 顺序固定亦足够：无害贡献之和，骰耗尽后的完成指示。
    return F(sum(sum(fs) >= need for fs in itertools.product(
        *(FACES[d] for d in dice))), 6 ** len(dice))


def partition_value(needs, hand, bundle):
    best = F(-1)
    for bits in itertools.product((0, 1), repeat=len(hand)):
        pa = completion(needs[0], tuple(d for d, t in zip(hand, bits) if t == 0))
        pb = completion(needs[1], tuple(d for d, t in zip(hand, bits) if t == 1))
        score = pa + pb + (bundle - 2) * pa * pb
        best = max(best, score)
    return best


def s(v):
    return f"{v.numerator}/{v.denominator}"


def preferred_targets(needs, hand, bundle):
    av = action_values(needs, hand, bundle, risk_only=True)
    if not av:
        return [], {}
    by_target = {t: max(v for a, v in av.items() if a[0] == t)
                 for t in range(2) if needs[t] > 0}
    best = max(by_target.values())
    return [t for t, v in by_target.items() if v == best], by_target


def main():
    start = time.perf_counter()
    hands = list(itertools.combinations_with_replacement(range(1, 7), 2))
    hands += [(d,) for d in range(1, 7)]
    cases = []
    flips, ties_changed, shortest_gap, finish_gap, partition_gap = [], [], [], [], []
    symmetry = risk_equal = 0
    for hand in hands:
        for needs in itertools.product(range(4), repeat=2):
            for bundle in (2, 3):
                full = value(needs, hand, bundle)
                risk = value(needs, hand, bundle, risk_only=True)
                assert full == risk, (needs, hand, bundle, full, risk)
                risk_equal += 1
                mirror = value(needs[::-1], hand, bundle, risk_only=True)
                assert mirror == risk
                symmetry += 1
                short = value(needs, hand, bundle, "short", True)
                finish = max(value(needs, hand, bundle, "A_first", True),
                             value(needs, hand, bundle, "B_first", True))
                part = partition_value(needs, hand, bundle)
                assert risk >= short and risk >= finish and risk >= part
                row = {"needs": needs, "dice": hand, "bundle_value": bundle,
                       "free": s(risk), "short": s(short),
                       "best_fixed_target_order": s(finish), "partition": s(part)}
                cases.append(row)
                for name, v, out in (("short", short, shortest_gap),
                                     ("finish", finish, finish_gap),
                                     ("partition", part, partition_gap)):
                    if v < risk:
                        out.append({**row, "gap": s(risk - v)})
            if all(needs):
                pref2, vals2 = preferred_targets(needs, hand, 2)
                pref3, vals3 = preferred_targets(needs, hand, 3)
                if pref2 != pref3:
                    ex = {"needs": needs, "dice": hand, "preferred_targets_2": pref2,
                          "preferred_targets_3": pref3,
                          "target_values_2": {str(t): s(v) for t, v in vals2.items()},
                          "target_values_3": {str(t): s(v) for t, v in vals3.items()}}
                    (flips if set(pref2).isdisjoint(pref3) else ties_changed).append(ex)
    result = {
        "status": "passed", "scope": "独立一/两骰字面六面小树；不读取父模型；非四骰全局复核",
        "cold_bound": "最多4次行动各耗1与回合税1，初始冷静5，无伤势",
        "conditions": len(cases), "hands": len(hands),
        "fast_vs_full_action_exact": risk_equal, "symmetry_exact": symmetry,
        "strict_target_flip_count": len(flips), "strict_target_flips": flips,
        "target_tie_set_change_count": len(ties_changed), "target_tie_set_changes": ties_changed,
        "shortest_gap_count": len(shortest_gap), "shortest_gaps": shortest_gap,
        "fixed_finish_gap_count": len(finish_gap), "fixed_finish_gaps": finish_gap,
        "partition_gap_count": len(partition_gap), "partition_gaps": partition_gap,
        "cases": cases, "literal_fate": FACES,
        "source_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "elapsed_seconds": time.perf_counter() - start,
    }
    (HERE / "审查两骰结果.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({k: result[k] for k in ("status", "conditions", "strict_target_flip_count",
          "target_tie_set_change_count", "shortest_gap_count", "fixed_finish_gap_count", "partition_gap_count",
          "elapsed_seconds")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
