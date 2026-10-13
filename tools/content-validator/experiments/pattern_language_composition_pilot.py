#!/usr/bin/env python3
"""Minimal, scenario-free action-topology composition counterexample.

All results are exact, deterministic and enumerated; this is *not* a human test.
"""
from dataclasses import dataclass
from functools import lru_cache


@dataclass(frozen=True)
class State:
    turns: int
    a: int
    b: int
    blocked: bool


def actions(s: State):
    if s.turns == 0:
        return ()
    legal = ['B']
    if s.blocked:
        legal.append('FIX')
    else:
        legal.append('A')
    return tuple(legal)


def step(s: State, action: str) -> State:
    assert action in actions(s), (s, action)
    return State(
        s.turns - 1,
        min(3, s.a + (action == 'A')),
        min(3, s.b + (action == 'B')),
        s.blocked and action != 'FIX',
    )


def result(s: State, b_reward: int = 4):
    # A completion reward 3; B completion reward 4. Partial progress is valueless.
    return (3 if s.a == 3 else 0) + (b_reward if s.b == 3 else 0) - (2 - s.turns) / 4


@lru_cache(None)
def solve(s: State, b_reward: int = 4):
    # Return (best total completed-value, tied optimal full-action sequences)
    if not actions(s):
        return result(s, b_reward), ((),)
    by_action = [("STOP", (result(s, b_reward), ((),)))] + [(action, solve(step(s, action), b_reward)) for action in actions(s)]
    top = max(v[0] for _, v in by_action)
    plans = tuple(((action,) if action != "STOP" else ()) + tail for action, (v, tails) in by_action
                  if v == top for tail in tails)
    return top, plans


def forced_fix(s: State, b_reward: int = 4):
    """A naive 'repair crisis first' policy; otherwise finish A before B."""
    while actions(s):
        if s.a == 3:
            break
        a = 'FIX' if s.blocked else ('A' if s.a < 3 else 'B')
        s = step(s, a)
    return result(s, b_reward)


def main():
    observed = {}
    for blocked in (False, True):
        for b in (0, 2):
            state = State(2, 2, b, blocked)
            value, routes = solve(state)
            observed[(blocked, b)] = value
            print(f'blocked={blocked!s:5} B={b} optimal={value} '
                  f'first_moves={sorted(set(path[0] for path in routes))} '
                  f'paths={routes} repair_first={forced_fix(state)}')
    assert observed == {(False, 0): 2.75, (False, 2): 6.5,
                        (True, 0): 2.5, (True, 2): 3.75}
    assert all(path[0] == 'FIX' for path in solve(State(2, 2, 0, True))[1])
    assert all(path[0] == 'B' for path in solve(State(2, 2, 2, True))[1])
    assert forced_fix(State(2, 2, 2, True)) == 2.5
    # Removing B as a live option (all B progress=0) turns crisis into fixed tax.
    assert solve(State(2, 2, 0, True))[0] == forced_fix(State(2, 2, 0, True))
    # Ablation: if B is not worth protecting, the same crisis is only a repair tax.
    low, paths = solve(State(2, 2, 2, True), b_reward=2)
    assert low == 2.5 and all(path[0] == 'FIX' for path in paths)
    print(f'lower B reward=2: blocked=True B=2 optimal={low}, paths={paths}')
    print('OK: exact action enumeration; no game-engine, blind-agent, or human claims')


if __name__ == '__main__':
    main()
