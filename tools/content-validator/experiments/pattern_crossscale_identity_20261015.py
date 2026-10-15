"""CROSS-SCALE06: exhaustive identity-aware policies atop frozen CROSS-SCALE05 rules.

No unverified human preference: Pareto counts are mathematical reachability only.
"""
from itertools import product
from fractions import Fraction as F

ACTIONS = ("TEST", "A", "B", "GO_A", "GO_B", "SIDE")


def outcome(seq, typ):
    loc, a, b, side = "A", 0, 0, 0
    for t, action in enumerate(seq):
        if action == "TEST":
            if t != 0 or loc != "A":
                return None
            a += 1
        elif action == "A":
            if loc != "A" or t >= 2:
                return None
            a += 1
        elif action == "B":
            if loc != "B" or t != 2:
                return None
            b += 1
        elif action in ("GO_A", "GO_B"):
            destination = action[-1]
            if loc == destination:
                return None
            loc = destination
        elif action == "SIDE":
            side += 1
        else:
            raise ValueError(action)
    done_a, done_b = int(a >= 2), int(b >= 1)
    return (done_a, done_b, side,
            (10 if typ == "H" else 4) * done_a + 8 * done_b + side)


def enumerated():
    valid = []
    for actions in product(ACTIONS, repeat=4):
        high, low = outcome(actions, "H"), outcome(actions, "L")
        if high is not None and low is not None:
            valid.append((actions, high, low))
    # No foreknowledge: only TEST on first action reveals the type.
    conditional = [(actions, actions, high, low)
                   for actions, high, low in valid]
    after_test = [p for p in valid if p[0][0] == "TEST"]
    conditional.extend((high_actions, low_actions, high_result, low_result)
                       for high_actions, high_result, _ in after_test
                       for low_actions, _, low_result in after_test)
    return valid, conditional


def vector(policy):
    _, _, high, low = policy
    return (F(high[0] + low[0], 2),
            F(high[1] + low[1], 2),
            F(high[2] + low[2], 2))


def frontiers():
    fixed, policies = enumerated()
    vectors = {vector(p) for p in policies}
    front = {v for v in vectors if not any(
        v != w and all(a <= b for a, b in zip(v, w)) for w in vectors)}
    return fixed, policies, vectors, front


def regression():
    fixed, policies, vectors, front = frontiers()
    assert len(fixed) == 52
    assert len(policies) == 248
    assert len(vectors) == 32
    assert len(front) == 5
    assert max(F(h[3] + l[3], 2) for _, h, l in fixed) == 10
    assert max(F(h[3] + l[3], 2) for _, _, h, l in policies) == F(21, 2)
    assert front == {
        (F(0), F(0), F(4)), (F(0), F(1), F(2)),
        (F(1, 2), F(0), F(5, 2)),
        (F(1, 2), F(1, 2), F(3, 2)),
        (F(1), F(0), F(2)),
    }
    for label, predicate in (
        ("guarantee_B", lambda v: v[1] == 1),
        ("guarantee_A", lambda v: v[0] == 1),
        ("preserve_both_possible", lambda v: v[0] > 0 and v[1] > 0),
    ):
        matches = [p for p in policies if predicate(vector(p))]
        assert matches, label
        best = max(F(p[2][3] + p[3][3], 2) for p in matches)
        print(label, "best_score", best)
    print("fixed", len(fixed), "contingent", len(policies))
    print("vectors", len(vectors), "Pareto", len(front))
    for v in sorted(front):
        print("identity_frontier", *(str(x) for x in v))
    print("PASS CROSS-SCALE06")


if __name__ == "__main__":
    regression()
