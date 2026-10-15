"""CROSS-SCALE05. Complete exact DP and all fixed scripts, standard library only."""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product

ACTIONS = ("TEST", "A", "B", "GO_A", "GO_B", "SIDE")

def solve(appointment=True, travel=True, ordinary_b=False):
    def transition(s, act):
        t, loc, a, b, typ, seen = s
        if act == "TEST":
            if t != 0 or (travel and loc != "A"):
                return None
            a += 1
            seen = typ
        elif act == "A":
            if travel and loc != "A" or appointment and t >= 2:
                return None
            a += 1
        elif act == "B":
            if travel and loc != "B":
                return None
            if appointment and (t != 2 if not ordinary_b else t < 2):
                return None
            b += 1
        elif act.startswith("GO_"):
            dest = act[-1]
            if not travel or loc == dest:
                return None
            loc = dest
        elif act != "SIDE":
            return None
        return (t + 1, loc, a, b, typ, seen)

    def result(s):
        _, _, a, b, typ, _ = s
        return (10 if typ == "H" else 4) * (a >= 2) + 8 * (b >= (2 if ordinary_b else 1))

    @lru_cache(None)
    def dp(t, loc, a, b, seen):
        if t == 4:
            return F(0), ()
        candidates = []
        for act in ACTIONS:
            outcomes = []
            for typ in ("H", "L") if seen == "?" else (seen,):
                n = transition((t, loc, a, b, typ, seen), act)
                if n is None:
                    break
                _, ll, aa, bb, _, ss = n
                value = F(result(n)) if t == 3 else dp(t + 1, ll, aa, bb, ss)[0]
                outcomes.append(value + (act == "SIDE"))
            if outcomes:
                candidates.append((sum(outcomes, F(0)) / len(outcomes), act))
        optimum = max(v for v, _ in candidates)
        return optimum, tuple(act for v, act in candidates if v == optimum)

    fixed = []
    for actions in product(ACTIONS, repeat=4):
        values = []
        for typ in ("H", "L"):
            s = (0, "A", 0, 0, typ, "?")
            sides = 0
            for act in actions:
                next_s = transition(s, act)
                if next_s is None:
                    break
                s = next_s
                sides += act == "SIDE"
            else:
                values.append(result(s) + sides)
        if len(values) == 2:
            fixed.append((F(sum(values), 2), actions))
    maximum = max(v for v, _ in fixed)
    return dp(0, "A", 0, 0, "?"), maximum, len(fixed), [seq for v, seq in fixed if v == maximum], dp(1, "A", 1, 0, "H"), dp(1, "A", 1, 0, "L")

def regression():
    checks = {
        (True, True, False): (F(21, 2), F(10), 52),
        (False, True, False): (F(15), F(15), 108),
        (True, False, False): (F(16), F(16), 12),
        (False, False, False): (F(16), F(16), 108),
        (True, True, True): (F(10), F(9), 67),
        (False, True, True): (F(10), F(9), 108),
    }
    for scenario, expected in checks.items():
        dynamic, fixed, n, best, h, l = solve(*scenario)
        assert (dynamic[0], fixed, n) == expected, (scenario, dynamic, fixed, n)
        print(scenario, "dynamic", dynamic, "fixed", fixed, "legal", n, "fixed_plan", best[0], "H/L", h, l)
    print("CROSS-SCALE05 regression PASS")

if __name__ == "__main__":
    regression()
