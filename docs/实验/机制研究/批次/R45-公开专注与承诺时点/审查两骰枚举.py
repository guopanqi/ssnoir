#!/usr/bin/env python3
"""R45独立字面一/两骰树：不读取父模型。"""
import hashlib
import itertools
import json
import time
from fractions import Fraction as F
from pathlib import Path

HERE = Path(__file__).resolve().parent
FACES = {
    1: {1: (0, 0, 1, 1, 1, 2), 2: (0, 1, 1, 1, 2, 2),
        3: (0, 1, 1, 2, 2, 2), 4: (1, 1, 2, 2, 2, 2),
        5: (1, 2, 2, 2, 2, 2), 6: (2, 2, 2, 2, 2, 2)},
    2: {1: (0, 1, 1, 1, 2, 2), 2: (0, 1, 1, 2, 2, 2),
        3: (1, 1, 2, 2, 2, 2), 4: (1, 2, 2, 2, 2, 2),
        5: (2, 2, 2, 2, 2, 2), 6: (2, 2, 2, 2, 2, 2)},
}


def terminal(needs):
    if needs[0] == 0:
        return F(1)
    if needs[1] == 0:
        return F(2)
    return None


def move(needs, target, grade, mode):
    gain = grade if mode == "risk" else int(grade == 2)
    out = list(needs)
    out[target] = max(0, out[target] - gain)
    return tuple(out)


def one_route(need, hand, payout, skill=2, onlyrisk=False):
    if need == 0:
        return F(payout)
    if not hand:
        return F(0)
    vals = []
    for die in sorted(set(hand)):
        rest = list(hand)
        rest.remove(die)
        for mode in (("risk",) if onlyrisk else ("risk", "steady")):
            vals.append(sum((one_route(max(0, need - (g if mode == "risk" else int(g == 2))),
                         tuple(rest), payout, skill, onlyrisk)
                         for g in FACES[skill][die]), F(0)) / 6)
    return max(vals)


def advance_values(needs, hand, family="free", onlyrisk=False):
    if terminal(needs) is not None or not hand:
        return {}
    vals = {}
    for t, die in itertools.product(range(2), sorted(set(hand))):
        rest = list(hand)
        rest.remove(die)
        for mode in (("risk",) if onlyrisk else ("risk", "steady")):
            nxt_family = "free" if family == "free" else "never"
            vals[(t, die, mode)] = sum((value(move(needs, t, g, mode), tuple(rest),
                                     nxt_family, onlyrisk)
                                     for g in FACES[1][die]), F(0)) / 6
    return vals


def lock_values(needs, hand):
    return {t: one_route(needs[t], hand, t + 1, onlyrisk=True) for t in range(2)}


def value(needs, hand, family="free", onlyrisk=False):
    done = terminal(needs)
    if done is not None:
        return done
    if not hand:
        return F(0)
    vals = list(advance_values(needs, hand, family, onlyrisk).values())
    if family == "free":
        vals += list(lock_values(needs, hand).values())
    return max(vals)


def first_feedback(needs, hand):
    # 开局锁，或第一手后锁/选择以后永不锁。只有两骰时已覆盖全部延迟时点。
    vals = list(lock_values(needs, hand).values())
    for t, die in itertools.product(range(2), sorted(set(hand))):
        rest = list(hand)
        rest.remove(die)
        for mode in ("risk", "steady"):
            branch = []
            for g in FACES[1][die]:
                nxt = move(needs, t, g, mode)
                done = terminal(nxt)
                branch.append(done if done is not None else
                              max(value(nxt, tuple(rest), "never"),
                                  *lock_values(nxt, tuple(rest)).values()))
            vals.append(sum(branch, F(0)) / 6)
    return max(vals)


def sf(v):
    return f"{v.numerator}/{v.denominator}"


def main():
    started = time.perf_counter()
    hands = [(d,) for d in range(1, 7)] + list(itertools.combinations_with_replacement(range(1, 7), 2))
    cases, delay, unlocked_mode, pointwise_steady = [], [], [], []
    locked_equal = []
    for hand in hands:
        for a, b in itertools.product(range(1, 4), range(1, 7)):
            needs = (a, b)
            free = value(needs, hand)
            never = value(needs, hand, "never")
            firstlock = max(lock_values(needs, hand).values())
            first = first_feedback(needs, hand)
            risk = value(needs, hand, onlyrisk=True)
            assert free >= first >= firstlock and free >= never
            assert free == first, (needs, hand, free, first)
            for t in range(2):
                full = one_route(needs[t], hand, t + 1)
                fast = one_route(needs[t], hand, t + 1, onlyrisk=True)
                assert full == fast
                locked_equal.append((needs, hand, t))
            row = {"needs": needs, "dice": hand, "free": sf(free), "never": sf(never),
                   "initial_lock": sf(firstlock), "first_feedback": sf(first),
                   "free_risk_only": sf(risk)}
            cases.append(row)
            if free > firstlock:
                delay.append({**row, "gap": sf(free-firstlock)})
            if free > risk:
                unlocked_mode.append(row)
            av = advance_values(needs, hand)
            for t, die in itertools.product(range(2), sorted(set(hand))):
                if av[(t, die, "steady")] > av[(t, die, "risk")]:
                    pointwise_steady.append({**row, "target": t, "die": die,
                        "steady_action_value": sf(av[(t, die, "steady")]),
                        "risk_action_value": sf(av[(t, die, "risk")])})
    # 由初始总需求9、四次最大2推出的必要可达条件，不等于最优前缀可达。
    capacity_steady = [r for r in pointwise_steady
                       if sum(r["needs"]) >= 2 * len(r["dice"]) + 1]
    assert not capacity_steady
    out = {"status": "passed", "scope": "独立字面一/两骰小树，非全初始四骰独立最优",
           "conditions": len(cases), "hands": len(hands), "locked_risk_equal_conditions": len(locked_equal),
           "free_vs_initial_lock_gap_count": len(delay), "delay_witnesses": delay,
           "free_full_vs_risk_only_gap_count": len(unlocked_mode), "unlocked_mode_witnesses": unlocked_mode,
           "advance_steady_beats_risk_count": len(pointwise_steady), "pointwise_steady_witnesses": pointwise_steady,
           "capacity_permitted_steady_beats_risk_count": len(capacity_steady),
           "capacity_permitted_delay_count": sum(sum(r["needs"]) >= 2 * len(r["dice"]) + 1 for r in delay),
           "first_feedback_equals_free_count": len(cases), "literal_fate": FACES, "cases": cases,
           "elapsed_seconds": time.perf_counter()-started,
           "source_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    (HERE / "审查两骰结果.json").write_text(json.dumps(out, ensure_ascii=False, indent=2)+"\n")
    print(json.dumps({k: out[k] for k in ("status", "conditions", "locked_risk_equal_conditions",
        "free_vs_initial_lock_gap_count", "free_full_vs_risk_only_gap_count",
        "advance_steady_beats_risk_count", "first_feedback_equals_free_count", "elapsed_seconds")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
