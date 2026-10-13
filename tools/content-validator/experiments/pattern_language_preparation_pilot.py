#!/usr/bin/env python3
"""Two-timescale preparation vs on-site enable: exact tiny deterministic enumeration.

The unknown field state is revealed *after* a public forecast and preparation.
This proves local option timing, not player enjoyment.
"""
from functools import lru_cache

PRE_CHOICES = ('EARN', 'PREP_RED', 'PREP_BLUE')
THETA = ('RED', 'BLUE')


@lru_cache(None)
def field_best(theta, prepared, timed_window, tick=0, enabled=None, used=False):
    if tick == 2:
        return 0, ((),)
    options = [('NORMAL', 1, enabled, used)]
    if not enabled:
        options += [(f'ENABLE_{color}', 0, color, used) for color in THETA]
    if not used and (prepared == theta or enabled == theta) and (tick == 0 or not timed_window):
        options.append(('SPECIAL', 4, enabled, True))
    variants = []
    for name, points, next_enabled, next_used in options:
        val, paths = field_best(theta, prepared, timed_window, tick + 1, next_enabled, next_used)
        variants.extend((points + val, (name,) + path) for path in paths)
    top = max(value for value, _ in variants)
    return top, tuple(route for value, route in variants if value == top)


def pre_outcome(pre, theta, timed_window):
    prepared = pre.removeprefix('PREP_') if pre.startswith('PREP_') else None
    pre_reward = 2 if pre == 'EARN' else 0
    field_score, paths = field_best(theta, prepared, timed_window)
    return pre_reward + field_score, paths


def expectation(pre, p_red, timed_window):
    return p_red * pre_outcome(pre, 'RED', timed_window)[0] + \
           (1 - p_red) * pre_outcome(pre, 'BLUE', timed_window)[0]


def main():
    for timed_window in (True, False):
        for p in (0.5, 0.8):
            values = {pre: round(expectation(pre, p, timed_window), 8) for pre in PRE_CHOICES}
            best = max(values.values())
            winners = tuple(pre for pre, v in values.items() if v == best)
            print(f'window={timed_window} P(RED)={p} options={values} best={winners}')
            if timed_window and p == .5:
                assert winners == ('EARN',) and best == 4
            if timed_window and p == .8:
                assert winners == ('PREP_RED',) and best == 4.4
            if not timed_window:
                assert winners == ('EARN',) and best == 6
    assert pre_outcome('PREP_RED', 'RED', True)[0] == 5
    assert pre_outcome('PREP_RED', 'BLUE', True)[0] == 2
    assert pre_outcome('EARN', 'BLUE', False)[0] == 6
    print('OK: complete field-action enumeration; preparation ex ante, field actions after reveal')


if __name__ == '__main__':
    main()
