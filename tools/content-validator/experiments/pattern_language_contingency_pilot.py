#!/usr/bin/env python3
"""A small exact model of advance preparation as a contingent alternative plan.

The two preparation actions occur before the shock is known; two field actions
occur after observation. This checks structural choices, not enjoyment.
"""
from fractions import Fraction as F
from functools import lru_cache


@lru_cache(None)
def field(a: int, b: int, shock: bool, turns: int = 2):
    # Public progress. A requires 3 and awards 6, B requires 2 and awards 4.
    # A field action is illegal in a shock; pre-shock A progress was lost.
    stop = F(6 if a >= 3 else 0) + F(4 if b >= 2 else 0)
    best = (stop, ())
    if not turns:
        return best
    for action in ("B",) if shock else ("A", "B"):
        next_a = min(3, a + (action == "A"))
        next_b = min(2, b + (action == "B"))
        future, route = field(next_a, next_b, shock, turns - 1)
        value = future - F(1, 2)  # Each spent field action has opportunity cost.
        if value > best[0]:
            best = (value, (action,) + route)
    return best


def preparation_table(shock_probability: F):
    choices = {}
    for label, a, b in (("AA", 2, 0), ("AB", 1, 1), ("BB", 0, 2)):
        safe = field(a, b, False)
        shocked = field(0, b, True)
        expected = (1 - shock_probability) * safe[0] + shock_probability * shocked[0]
        choices[label] = {
            "expected": expected,
            "safe": safe,
            "shock": shocked,
        }
    return choices



@lru_cache(None)
def repairable_field(a: int, b: int, blocked: bool, turns: int = 2):
    """A shock now merely locks A until spending one field action on FIX.

    No A progress is destroyed; the external event still occurs at the same
    time and with the same probability.
    """
    best = (F(6 if a >= 3 else 0) + F(4 if b >= 2 else 0), ())
    if not turns:
        return best
    for action in (("FIX", "B") if blocked else ("A", "B")):
        next_a = min(3, a + (action == "A"))
        next_b = min(2, b + (action == "B"))
        next_blocked = blocked and action != "FIX"
        reward, route = repairable_field(next_a, next_b, next_blocked, turns - 1)
        candidate = reward - F(1, 2)
        if candidate > best[0]:
            best = (candidate, (action,) + route)
    return best


def repairable_preparation_table(shock_probability: F):
    values = {}
    for label, a, b in (("AA", 2, 0), ("AB", 1, 1), ("BB", 0, 2)):
        safe, _ = repairable_field(a, b, False)
        shocked, _ = repairable_field(a, b, True)
        values[label] = (1 - shock_probability) * safe + shock_probability * shocked
    return values

def main():
    expected_best = {
        F(1, 5): ("AA", F(5)),
        F(3, 5): ("AB", F(41, 10)),
        F(4, 5): ("BB", F(4)),
    }
    for p in (F(1, 5), F(1, 2), F(3, 5), F(4, 5)):
        values = preparation_table(p)
        winners = tuple(k for k, v in values.items() if v["expected"] == max(
            item["expected"] for item in values.values()
        ))
        print(f"shock={p} " + " ".join(
            f"{k}:{v['expected']}" for k, v in values.items()
        ) + f" winner(s)={','.join(winners)}")
        if p in expected_best:
            label, score = expected_best[p]
            assert winners == (label,), (p, winners)
            assert values[label]["expected"] == score
    assert field(1, 1, False) == (F(5), ("A", "A"))
    assert field(0, 1, True) == (F(7, 2), ("B",))
    for p, expected in ((F(1, 5), F(27, 5)), (F(3, 5), F(26, 5)),
                        (F(4, 5), F(51, 10)), (F(1), F(5))):
        options = repairable_preparation_table(p)
        assert max(options, key=options.get) == "AA"
        assert options["AA"] == expected
        print(f"repairable shock={p} " +
              " ".join(f"{k}:{v}" for k, v in options.items()) +
              " winner=AA")
    print("PASS: original hard-closure and new one-step repair ablation")


if __name__ == "__main__":
    main()
