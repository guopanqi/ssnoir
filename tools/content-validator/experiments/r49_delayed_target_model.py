#!/usr/bin/env python3
"""R49 独立有限模型：先准备两次，再以1/2、1/4、1/4揭示唯一真目标。

不调用游戏私有状态；仅复用公开 FateStrip 的 skill=1 概率表。
纯结构筛选：不包含伤势、冷静、物品和回合转换。
"""
from __future__ import annotations

import json
from fractions import Fraction
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial

F = Fraction
THRESHOLDS = (4, 4, 4)
PRIORS = (F(1, 2), F(1, 4), F(1, 4))


def probs(die: int, skill: int = 1) -> tuple[Fraction, ...]:
    prepared = die + skill
    fail, middle = (
        (3, 3) if prepared <= 1 else
        {2: (2, 3), 3: (1, 3), 4: (1, 2), 5: (0, 2), 6: (0, 1)}.get(prepared, (0, 0))
    )
    return F(fail, 6), F(middle, 6), F(6 - fail - middle, 6)


@lru_cache(None)
def completion(need: int, die_a: int, die_b: int, skill: int) -> Fraction:
    """Remaining two dice both go into the one revealed target."""
    if need <= 0:
        return F(1)
    return sum(a * b for i, a in enumerate(probs(die_a, skill))
               for j, b in enumerate(probs(die_b, skill)) if i + j >= need)


def remove_one(dice: tuple[int, ...], index: int) -> tuple[int, ...]:
    return dice[:index] + dice[index + 1:]


def root_weight(root: tuple[int, ...]) -> Fraction:
    weight = F(factorial(len(root)), 6 ** len(root))
    for die in set(root):
        weight /= factorial(root.count(die))
    return weight


def evaluate(skill: int = 1):
    @lru_cache(None)
    def after(prepared: tuple[int, ...], remaining: tuple[int, ...]) -> Fraction:
        return sum(PRIORS[i] * completion(max(0, THRESHOLDS[i] - prepared[i]),
                                         remaining[0], remaining[1], skill)
                   for i in range(3))

    @lru_cache(None)
    def optimal(prepared: tuple[int, ...], remaining: tuple[int, ...]) -> Fraction:
        if len(remaining) == 2:
            return after(prepared, remaining)
        return max(
            sum(prob * optimal(tuple(prepared[k] + (gain if k == goal else 0)
                                     for k in range(3)), remove_one(remaining, di))
                for gain, prob in enumerate(probs(die, skill)))
            for di, die in enumerate(remaining) for goal in range(3)
        )

    @lru_cache(None)
    def fixed(prepared: tuple[int, ...], remaining: tuple[int, ...],
              plan: tuple[tuple[int, int], ...]) -> Fraction:
        if not plan:
            return after(prepared, remaining)
        goal, die = plan[0]
        index = remaining.index(die)
        rest = remove_one(remaining, index)
        return sum(prob * fixed(tuple(prepared[k] + (gain if k == goal else 0)
                                      for k in range(3)), rest, plan[1:])
                   for gain, prob in enumerate(probs(die, skill)))

    def best_fixed(root: tuple[int, ...]) -> Fraction:
        return max(
            fixed((0, 0, 0), root, ((g1, d1), (g2, d2)))
            for idx, d1 in enumerate(root)
            for d2 in remove_one(root, idx)
            for g1 in range(3) for g2 in range(3)
        )

    def focus(root: tuple[int, ...]) -> Fraction:
        return max(fixed((0, 0, 0), root, ((0, d1), (0, d2)))
                   for idx, d1 in enumerate(root) for d2 in remove_one(root, idx))

    def split(root: tuple[int, ...]) -> Fraction:
        return max(fixed((0, 0, 0), root, ((0, d1), (g, d2)))
                   for idx, d1 in enumerate(root) for d2 in remove_one(root, idx)
                   for g in (1, 2))

    roots = list(combinations_with_replacement(range(1, 7), 4))
    metrics = {'adaptive': F(0), 'best_fixed': F(0), 'focus': F(0), 'split': F(0)}
    witnesses = []
    for root in roots:
        w = root_weight(root)
        values = {'adaptive': optimal((0, 0, 0), root),
                  'best_fixed': best_fixed(root),
                  'focus': focus(root), 'split': split(root)}
        for key, value in values.items():
            metrics[key] += w * value
        if values['adaptive'] > values['best_fixed'] and len(witnesses) < 8:
            witnesses.append({'dice': list(root), 'adaptive': float(values['adaptive']),
                              'best_fixed': float(values['best_fixed'])})

    assert sum(map(root_weight, roots)) == 1
    assert metrics['adaptive'] >= metrics['best_fixed'] >= metrics['focus']
    assert metrics['best_fixed'] >= metrics['split']
    return {'skill': skill, 'thresholds': THRESHOLDS,
            'priors': [str(x) for x in PRIORS], 'initial_hands': len(roots),
            'metrics': {key: float(value) for key, value in metrics.items()},
            'exact_metrics': {key: str(value) for key, value in metrics.items()},
            'strict_witnesses': witnesses,
            'evidence': 'exact finite model; not native game run or human playtest'}


def main():
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output')
    args = parser.parse_args()
    results = {'model': 'R49 delayed target, two preparation dice, two execution dice',
               'results': [evaluate(1), evaluate(2)]}
    payload = json.dumps(results, ensure_ascii=False, indent=2)
    if args.output:
        from pathlib import Path
        Path(args.output).write_text(payload + '\n', encoding='utf-8')
    else:
        print(payload)


if __name__ == '__main__':
    main()
