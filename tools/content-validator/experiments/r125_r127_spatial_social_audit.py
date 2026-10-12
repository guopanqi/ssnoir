"""R125-R127: exact small-model counterexamples. Not official SSNoir Content."""
from fractions import Fraction as Q
from functools import lru_cache
from itertools import product
import json

def spatial(steps, east_deadline, mode="free"):
    """Hub -> E/W costs one action; each site needs two successful work actions.
    Every work has P(success)=1/2; each completed result permanently pays 2.
    """
    def actions(t, where, e, w):
        a = []
        if where != "E": a.append(("moveE", (t + 1, "E", e, w)))
        if where != "W": a.append(("moveW", (t + 1, "W", e, w)))
        if where == "E" and e < 2 and t < east_deadline: a.append(("workE", None))
        if where == "W" and w < 2 and t < steps: a.append(("workW", None))
        a.append(("wait", (t + 1, where, e, w)))
        if mode.startswith("Acut") and e < 2:
            cut = int(mode[4:])
            ban = ("moveW", "workW") if t < cut else ("moveE", "workE")
            a = [(n, x) for n, x in a if n not in ban]
        if mode.startswith("Asimple") and e < 2:
            cut = tuple(map(int, mode[7:].split("_")))[e]
            ban = ("moveW", "workW") if t < cut else ("moveE", "workE")
            a = [(n, x) for n, x in a if n not in ban]
        if mode == "onlyE": a = [(n, x) for n, x in a if n not in ("moveW", "workW")]
        if mode == "onlyW": a = [(n, x) for n, x in a if n not in ("moveE", "workE")]
        return a

    @lru_cache(None)
    def value(t, where, e, w):
        if t >= steps: return Q(2 * (e == 2) + 2 * (w == 2))
        opts = []
        for n, s in actions(t, where, e, w):
            if n == "workE":
                v = (value(t + 1, where, min(2, e + 1), w) + value(t + 1, where, e, w)) / 2
            elif n == "workW":
                v = (value(t + 1, where, e, min(2, w + 1)) + value(t + 1, where, e, w)) / 2
            else: v = value(*s)
            opts.append(v)
        return max(opts)
    return value(0, "H", 0, 0)

def social(steps=3, collateral=True, orderly=False, allow_coerce=True):
    """Two independent 1/2 witnesses. Asking reveals cooperation; after refusal,
    coercion gets evidence but, if collateral, closes any unasked other witness.
    Two records pay 5, one pays 2; completed records never disappear.
    """
    @lru_cache(None)
    def V(t, asked, recorded, reluctant, closed):
        if t == steps: return Q(2 * recorded.bit_count() + (recorded == 3))
        outcomes = [V(t + 1, asked, recorded, reluctant, closed)]
        for i in (0, 1):
            bit = 1 << i
            if not (asked | closed) & bit:
                yes = V(t + 1, asked | bit, recorded | bit, reluctant, closed)
                no = V(t + 1, asked | bit, recorded, reluctant | bit, closed)
                outcomes.append((yes + no) / 2)
            if allow_coerce and reluctant & bit and not recorded & bit:
                other = 3 ^ bit
                if orderly and not (asked | closed) & other: continue
                new_closed = closed | (other if collateral and not (asked & other) else 0)
                outcomes.append(V(t + 1, asked, recorded | bit, reluctant, new_closed))
        return max(outcomes)
    return V(0, 0, 0, 0, 0)

def fmt(q): return {"fraction": str(q), "decimal": float(q)}

def audit():
    cases = {}
    for name, steps, deadline in (
        ("capacity5", 5, 4), ("capacity7", 7, 5), ("sameDeadline7", 7, 7)):
        free = spatial(steps, deadline)
        fixed = max((spatial(steps, deadline, f"Acut{k}"), k) for k in range(steps + 1))
        responsive = max(
            (spatial(steps, deadline, f"Asimple{x}_{y}"), x, y)
            for x, y in product(range(steps + 1), repeat=2))
        only = max(spatial(steps, deadline, "onlyE"), spatial(steps, deadline, "onlyW"))
        assert only <= fixed[0] <= responsive[0] <= free
        cases[name] = {
            "free": fmt(free), "singleSite": fmt(only),
            "bestTimedCut": {"value": fmt(fixed[0]), "time": fixed[1]},
            "twoPublicThresholds": {
                "value": fmt(responsive[0]), "cut0": responsive[1], "cut1": responsive[2]},
            "gainOverTimedCut": fmt(free - fixed[0]),
            "gainOverPublicThresholds": fmt(free - responsive[0])}
    assert cases["capacity5"]["gainOverTimedCut"]["fraction"] == "0"
    assert cases["capacity7"]["gainOverTimedCut"]["fraction"] == "3/32"
    assert cases["capacity7"]["gainOverPublicThresholds"]["fraction"] == "0"
    witness = {name: fmt(social(**kw)) for name, kw in {
        "full": {}, "askBothFirst": {"orderly": True},
        "noCollateral": {"collateral": False},
        "noCoercion": {"allow_coerce": False}}.items()}
    assert witness["full"] == witness["askBothFirst"] == witness["noCollateral"]
    assert social() > social(allow_coerce=False)
    return {
        "schema": "ssnoir.R125-R127.exact-paper/v1",
        "evidence": "exact Fraction paper models only",
        "spatial": cases, "social": witness,
        "verdict": {
            "R125": "spatial switching becomes meaningful only at sufficient capacity",
            "R126": "two public visible-progress thresholds reproduce exact optimum",
            "R127": "collateral witness-closure yields zero marginal value at optimum"}}

if __name__ == "__main__":
    print(json.dumps(audit(), ensure_ascii=False, indent=2))
