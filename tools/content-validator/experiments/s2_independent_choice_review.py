#!/usr/bin/env python3
"""S2-D independent exact audit: first opportunity versus repeated visits.

Does not import either existing paper solver. Reconstructs its seven-action
model from the publicly documented rules and independently checks dynamic
Q-values, direct ablations, and non-duplicated FIRST opportunity probability.
This is paper mathematics, not native C# / Scheme or a human playtest.
"""
from collections import defaultdict
from functools import lru_cache
from fractions import Fraction as F
import json

ROOT = (0, 0, 0, False, 7)
TIE_ORDER = ("help", "lead", "submit", "investigate")

def allowed(s, disable_lead=False, no_close=False, no_deadline=False):
    a, b, c, submitted, n = s
    if n == 0:
        return ()
    choices = []
    if a < 2 and not submitted and (no_deadline or n > 3):
        choices.append("investigate")
    if a == 2 and not submitted and (no_deadline or n > 3):
        choices.append("submit")
    if b < 2 and n > 3:
        choices.append("help")
    if a == 2 and c < 2 and not disable_lead and (not submitted or no_close):
        choices.append("lead")
    return tuple(choices)

def edges(s, action):
    a, b, c, submitted, n = s
    if action == "submit":
        return [(F(1), (a, b, c, True, n - 1))]
    return [(F(1, 3), (
        min(2, a + gain) if action == "investigate" else a,
        min(2, b + gain) if action == "help" else b,
        min(2, c + gain) if action == "lead" else c,
        submitted, n - 1)) for gain in range(3)]

def payoff(s):
    a, b, c, submitted, _ = s
    return 2 * F(submitted) + 2 * F(b == 2) + 2 * F(c == 2)

@lru_cache(None)
def exact(s, disable_lead=False, no_close=False, no_deadline=False):
    acts = allowed(s, disable_lead, no_close, no_deadline)
    if not acts:
        return payoff(s)
    return max(sum(p * exact(t, disable_lead, no_close, no_deadline)
                   for p, t in edges(s, act)) for act in acts)

def qvalues(s):
    return {act: sum(p * exact(t) for p, t in edges(s, act))
            for act in allowed(s)}

def audit():
    seen = defaultdict(F)
    first = defaultdict(F)
    first_strict = {"lead": F(0), "submit": F(0), "help": F(0)}
    all_strict = {"lead": F(0), "submit": F(0), "help": F(0)}
    witnesses = {}
    def follow(s, mass, already_revealed=False):
        if not allowed(s):
            return
        seen[s] += mass
        values = qvalues(s)
        best = max(values.values())
        choice = next(x for x in TIE_ORDER if x in values and values[x] == best)
        opportunity = "lead" in values and "submit" in values
        if opportunity:
            is_first = not already_revealed
            if is_first:
                first[s] += mass
            strict = best > max(v for key, v in values.items() if key != choice)
            if strict and choice in all_strict:
                all_strict[choice] += mass
                if is_first:
                    first_strict[choice] += mass
                witnesses.setdefault(choice, {
                    "state": list(s), "one_history_mass": str(mass),
                    "action_values": {k: str(v) for k, v in values.items()}})
            already_revealed = True
        for p, nxt in edges(s, choice):
            follow(nxt, mass * p, already_revealed)
    follow(ROOT, F(1))
    out = {
        "expected_reward": str(exact(ROOT)),
        "disable_lead": str(exact(ROOT, disable_lead=True)),
        "allow_lead_after_submission": str(exact(ROOT, no_close=True)),
        "remove_letter_deadline": str(exact(ROOT, no_deadline=True)),
        "first_opportunity_probability": str(sum(first.values())),
        "first_strict_choice_probabilities": {k: str(v) for k, v in first_strict.items()},
        "expected_strict_repeated_visits": {k: str(v) for k, v in all_strict.items()},
        "witnesses": witnesses
    }
    assert out["expected_reward"] == "6598/2187"
    assert out["first_opportunity_probability"] == "1/3"
    assert out["first_strict_choice_probabilities"] == {"lead": "1/9", "submit": "2/9", "help": "0"}
    assert out["expected_strict_repeated_visits"]["submit"] == "8/27"
    assert exact(ROOT, disable_lead=True) < exact(ROOT)
    assert exact(ROOT, no_close=True) > exact(ROOT)
    assert exact(ROOT, no_deadline=True) > exact(ROOT)
    assert "lead" in witnesses and "submit" in witnesses
    return out

if __name__ == "__main__":
    print(json.dumps({
        "scope": "Independent Fraction reviewer; distinct from existing R122 solvers. No native or human evidence.",
        "result": audit()
    }, ensure_ascii=False, indent=2))
