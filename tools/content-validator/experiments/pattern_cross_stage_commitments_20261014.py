"""OBLIGATION05: complete conditional strategy enumeration for required outcome sets."""
from itertools import product
from fractions import Fraction

ACTIONS = "ABCS"
PATHS = ["".join(s) for s in product(ACTIONS, repeat=5)]
INDEX = {"A": 0, "B": 1, "C": 2, "P": 4, "R": 5}

def first(s, convenient):
    return (
        int(s[:2].count("A") >= 2),
        int(s[:4].count("B") >= 2),
        int(s.count("C") >= (1 if convenient else 2)),
        s.count("S"),
    )

def follow(a, b, always=False):
    menu = "XS" + ("Q" if a or always else "") + ("R" if b or always else "")
    for acts in product(menu, repeat=2):
        if acts.count("Q") > 1 or acts.count("R") > 1:
            continue
        yield "".join(acts), (
            int("Q" in acts), int("R" in acts),
            acts.count("X"), acts.count("S"),
        )

def score(v):
    a, b, c, s, p, r, x, t = v
    return 8*a + 9*b + 10*c + s + 9*p + 5*r + 3*x + t

def outcome_choices(always):
    grouped = {(first, shock): [] for first in ACTIONS for shock in (0, 1)}
    for shock in (0, 1):
        for prefix in PATHS:
            a, b, c, side = first(prefix, shock)
            for continuation, (p, r, x, t) in follow(a, b, always):
                v = (a, b, c, side, p, r, x, t)
                grouped[prefix[0], shock].append((score(v), prefix, continuation, v))
    return grouped

def optimum(required="", always=False):
    grouped = outcome_choices(always)
    root = None
    for starting in ACTIONS:
        branches = []
        for shock in (0, 1):
            feasible = [z for z in grouped[starting, shock]
                        if all(z[3][INDEX[k]] for k in required)]
            if not feasible:
                break
            branches.append(max(feasible))
        if len(branches) == 2:
            expected = Fraction(branches[0][0] + branches[1][0], 2)
            if root is None or expected > root[0]:
                root = (expected, starting, branches)
    return root

if __name__ == "__main__":
    expected = {
        "": (Fraction(73, 2), Fraction(37)),
        "PR": (Fraction(73, 2), Fraction(37)),
        "PC": (Fraction(36), Fraction(37)),
        "RC": (Fraction(34), Fraction(37)),
        "PRC": (None, Fraction(37)),
        "ABC": (None, None),
        "ABPR": (Fraction(73, 2), Fraction(73, 2)),
    }
    for required, (gated, always) in expected.items():
        a, b = optimum(required, False), optimum(required, True)
        assert (a[0] if a else None) == gated, (required, a)
        assert (b[0] if b else None) == always, (required, b)
        print(required or "none", "gated", a[0] if a else None,
              "always", b[0] if b else None)
    print("PASS: seven exact commitment comparisons, common-first-action condition")
