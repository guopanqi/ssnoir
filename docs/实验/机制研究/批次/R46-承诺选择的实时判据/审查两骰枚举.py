#!/usr/bin/env python3
"""R46独立字面概率树与局部判据，不读取父模型或父策略。"""
import hashlib
import itertools
import json
import time
from fractions import Fraction as F
from functools import cache
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


def completion_direct(need, hand):
    fs = itertools.product(*(FACES[2][d] for d in hand))
    return F(sum(sum(grades) >= need for grades in fs), 6 ** len(hand))


def completion_poly(need, hand):
    coeff = [1]
    for die in hand:
        new = [0] * (len(coeff) + 2)
        for power, count in enumerate(coeff):
            for grade in FACES[2][die]:
                new[power + grade] += count
        coeff = new
    return F(sum(coeff[max(0, need):]), 6 ** len(hand))


def lock_values(needs, hand, direct=False):
    fn = completion_direct if direct else completion_poly
    return tuple((t+1)*fn(needs[t], hand) for t in range(2))


def move(needs, target, grade, mode):
    out = list(needs)
    out[target] = max(0, out[target] - (grade if mode == "risk" else int(grade == 2)))
    return tuple(out)


def actions(hand):
    # 确定性平手只用于执行，不当作额外偏好或严格差距。
    return [(d, t, mode) for d in sorted(set(hand)) for t in range(2)
            for mode in ("risk", "steady")]


def q_value(needs, hand, action, direct=False):
    die, target, mode = action
    rest = list(hand)
    rest.remove(die)
    scores = []
    for grade in FACES[1][die]:
        nxt = move(needs, target, grade, mode)
        done = terminal(nxt)
        scores.append(done if done is not None else max(lock_values(nxt, tuple(rest), direct)))
    return sum(scores, F(0)) / 6


def choose(needs, hand, policy="rolling"):
    lv = lock_values(needs, hand)
    route = 0 if lv[0] >= lv[1] else 1
    ls = lv[route]
    candidates = actions(hand) if policy == "rolling" else [(min(hand), 1, "risk")]
    av = [(a, q_value(needs, hand, a)) for a in candidates]
    action, q = max(av, key=lambda x: x[1])
    return ("advance", action) if q > ls else ("lock", route)


def policy_value(needs, hand, policy):
    done = terminal(needs)
    if done is not None:
        return done
    if not hand:
        return F(0)
    kind, selected = choose(needs, hand, policy)
    if kind == "lock":
        return lock_values(needs, hand)[selected]
    die, target, mode = selected
    rest = list(hand)
    rest.remove(die)
    return sum((policy_value(move(needs, target, g, mode), tuple(rest), policy)
                for g in FACES[1][die]), F(0)) / 6


@cache
def free_value(needs, hand):
    done = terminal(needs)
    if done is not None:
        return done
    if not hand:
        return F(0)
    av = list(lock_values(needs, hand))
    for die, target, mode in actions(hand):
        rest = list(hand)
        rest.remove(die)
        av.append(sum((free_value(move(needs, target, g, mode), tuple(rest))
                       for g in FACES[1][die]), F(0)) / 6)
    return max(av)


def sf(v):
    return f"{v.numerator}/{v.denominator}"


def main():
    began = time.perf_counter()
    hands = [(d,) for d in range(1,7)] + list(itertools.combinations_with_replacement(range(1,7),2))
    poly_checks = 0
    for hand, need in itertools.product(hands, range(7)):
        assert completion_poly(need, hand) == completion_direct(need, hand)
        poly_checks += 1
    cases, low_gap, omissions, ties = [], [], [], []
    q_checks = 0
    for hand, a, b in itertools.product(hands, range(1,4), range(1,7)):
        needs = (a,b)
        for action in actions(hand):
            assert q_value(needs, hand, action) == q_value(needs, hand, action, True)
            q_checks += 1
        lv = lock_values(needs, hand)
        l = max(lv)
        qs = {action:q_value(needs, hand, action) for action in actions(hand)}
        bestq = max(qs.values())
        lowaction = (min(hand),1,"risk")
        lowq = qs[lowaction]
        free = free_value(needs, hand)
        rolling = policy_value(needs, hand, "rolling")
        lowest = policy_value(needs, hand, "lowest")
        assert l <= lowest <= rolling <= free
        # 至多两骰，下一手只剩一个骰，下一次立即锁已足够；不是四骰结论。
        assert rolling == free
        row = {"needs":needs,"dice":hand,"L":sf(l),"best_Q":sf(bestq),
               "lowest_Q":sf(lowq),"free":sf(free),"rolling":sf(rolling),"lowest":sf(lowest),
               "rolling_decision":choose(needs,hand,"rolling"),
               "lowest_decision":choose(needs,hand,"lowest")}
        cases.append(row)
        if lowest < free:
            low_gap.append({**row,"gap":sf(free-lowest)})
        if bestq > lowq and bestq > l:
            omissions.append({**row,"best_Q_actions":[x for x,v in qs.items() if v==bestq]})
        if bestq == l:
            assert choose(needs,hand,"rolling")[0] == "lock"
            ties.append(row)
    capacity_omissions = [r for r in omissions if sum(r["needs"]) >= 2*len(r["dice"])+1]
    assert not capacity_omissions
    # 单个正常四骰根：扩大此局部见证，不枚举全部126初始手。
    root_needs, root_hand = (3,6), (2,2,5,5)
    root_free = free_value(root_needs, root_hand)
    root_l = max(lock_values(root_needs, root_hand))
    root_q = max(q_value(root_needs, root_hand, act) for act in actions(root_hand))
    assert root_free == F(65,36) and root_l == F(31,18) and root_q == F(5,3)
    root_branch = []
    for grade, expected in enumerate((F(4,3),F(11,6),F(2))):
        needs = move(root_needs,1,grade,"risk")
        fv = free_value(needs,(2,5,5))
        assert fv == expected
        root_branch.append({"first_grade":grade,"needs":needs,"free":sf(fv),
                            "immediate_lock":sf(max(lock_values(needs,(2,5,5))))})
    # 独立直接枚举两颗低骰的36结果：累计>=2锁乙，否则用两颗5保底甲。
    reserve = F(sum(2 if x+y >= 2 else 1 for x,y in
                    itertools.product(FACES[1][2],repeat=2)),36)
    assert reserve == root_free
    root_witness = {"needs":root_needs,"dice":root_hand,"free":sf(root_free),
                    "initial_lock":sf(root_l),"max_Q":sf(root_q),
                    "rolling":sf(policy_value(root_needs,root_hand,"rolling")),
                    "lowest":sf(policy_value(root_needs,root_hand,"lowest")),
                    "first_2_risk_B_branches":root_branch,
                    "two_low_contribution_direct_cases":36,"reserve_policy":sf(reserve)}
    result = {"status":"passed","scope":"独立字面一/两骰树及单个正常四骰根2255；非全初始四骰独立最优",
              "conditions":len(cases),"poly_direct_checks":poly_checks,"Q_direct_checks":q_checks,
              "rolling_equals_free_count":len(cases),"lowest_gap_count":len(low_gap),
              "lowest_gaps":low_gap,"better_probe_omission_count":len(omissions),
              "better_probe_omissions":omissions,"maxQ_L_tie_count":len(ties),
              "capacity_permitted_probe_omission_count":len(capacity_omissions),
              "single_normal_four_dice_witness":root_witness,
              "maxQ_L_ties":ties,"cases":cases,"literal_fate":FACES,
              "source_sha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "elapsed_seconds":time.perf_counter()-began}
    (HERE/"审查两骰结果.json").write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps({k:result[k] for k in ("status","conditions","poly_direct_checks","Q_direct_checks",
         "rolling_equals_free_count","lowest_gap_count","better_probe_omission_count","maxQ_L_tie_count",
         "elapsed_seconds")},ensure_ascii=False))


if __name__ == "__main__":
    main()
