#!/usr/bin/env python3
"""R50 动作回声：上一手的目标身份，可逆地改变下次判定难度。

有限模型：A达到3立刻得1，B达到5立刻得2，没完成得0；
四骰、公共奖赏、所有判定坏/中/好分别推进0/1/2。
第一手无修正，连续攻击同目标下一手准备值 -1，换目标 +1。
对照：不受上一手影响（0）。不是官方 Content，也不是玩家体验。
"""
from __future__ import annotations

import json
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement, product
from math import factorial


def odds(die: int, skill: int, offset: int) -> tuple[F, F, F]:
    prepared = die + skill + offset
    bad, mid = ((3, 3) if prepared <= 1 else
                {2: (2, 3), 3: (1, 3), 4: (1, 2),
                 5: (0, 2), 6: (0, 1)}.get(prepared, (0, 0)))
    return F(bad, 6), F(mid, 6), F(6 - bad - mid, 6)


def initial_weight(dice: tuple[int, ...]) -> F:
    result = F(factorial(4), 6 ** 4)
    for value in set(dice):
        result /= factorial(dice.count(value))
    return result


def analyze(skill: int, mode: str):
    @lru_cache(None)
    def value(a: int, b: int, previous: int, dice: tuple[int, ...],
              fixed_order: tuple[int, ...]) -> F:
        if a >= 3:
            return F(1)
        if b >= 5:
            return F(2)
        if not dice:
            return F(0)
        best = []
        for ix, die in enumerate(dice):
            remaining = dice[:ix] + dice[ix + 1:]
            for goal in ((fixed_order[0],) if fixed_order else (0, 1)):
                if previous < 0 or mode == 'no-recent-effect':
                    mod = 0
                else:
                    mod = 1 if goal != previous else -1
                tail = fixed_order[1:] if fixed_order else ()
                expectation = sum(p * value(min(3, a + (gain if goal == 0 else 0)),
                                             min(5, b + (gain if goal == 1 else 0)),
                                             goal, remaining, tail)
                                  for gain, p in enumerate(odds(die, skill, mod)))
                best.append(expectation)
        return max(best)

    free, fixed = F(0), F(0)
    witnesses = []
    for dice in combinations_with_replacement(range(1, 7), 4):
        adaptive = value(0, 0, -1, dice, ())
        best_plan = max(value(0, 0, -1, dice, order)
                        for order in product(range(2), repeat=4))
        weight = initial_weight(dice)
        free += weight * adaptive
        fixed += weight * best_plan
        if adaptive > best_plan and len(witnesses) < 6:
            witnesses.append({'dice': list(dice), 'adaptive': float(adaptive),
                              'fixed_goal_order': float(best_plan)})
    assert free >= fixed
    return {'skill': skill, 'mode': mode,
            'adaptive': float(free), 'best_fixed_goal_order': float(fixed),
            'extra_from_adapting_goals': float(free - fixed),
            'exact_adaptive': str(free), 'exact_fixed': str(fixed),
            'strict_witnesses': witnesses}


def main():
    output = []
    for skill in (1, 2, 3):
        changed = analyze(skill, 'last-action-switch')
        control = analyze(skill, 'no-recent-effect')
        output.append({'skill': skill, 'changed': changed, 'control': control,
                       'incremental_dynamic_gap':
                           changed['extra_from_adapting_goals'] -
                           control['extra_from_adapting_goals']})
    assert output[0]['incremental_dynamic_gap'] > 0
    print(json.dumps({'identity': 'exact finite screening only, not Scheme/Unity playtest',
                      'runs': output}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
