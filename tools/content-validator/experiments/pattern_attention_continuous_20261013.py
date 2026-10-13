#!/usr/bin/env python3
"""Three-day recurring attention: exact finite probability model, not a playtest."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

VALUES = (5, 5, 6)
CHANCE = (F(1, 2),) * 3

def study(required=4):
    @lru_cache(None)
    def optimal(day, b):
        if day == 3:
            return (F(9) if b >= required else F(0), "DONE", "DONE")
        bnext = min(required, b + 2)
        urgent = F(VALUES[day]) + optimal(day + 1, b)[0]
        long_term = optimal(day + 1, bnext)[0]
        yes = "U" if urgent > long_term else "B"
        return (CHANCE[day] * max(urgent, long_term) + (1-CHANCE[day])*long_term, yes, "B")

    def evaluate(choose):
        @lru_cache(None)
        def walk(day, b):
            if day == 3:
                return F(9) if b >= required else F(0)
            bnext = min(required, b + 2)
            with_urgent = (
                F(VALUES[day]) + walk(day + 1, b)
                if choose(day, b) == "U" else walk(day + 1, bnext)
            )
            return CHANCE[day] * with_urgent + (1-CHANCE[day])*walk(day + 1, bnext)
        return walk(0, 0)

    fixed = [
        (evaluate(lambda day, b, plan=plan: plan[day]), plan)
        for plan in product("UB", repeat=3)
    ]
    def protect_long_term(day, b):
        future_capacity = 2 * (2 - day)
        return "U" if b + future_capacity >= required else "B"

    return optimal, max(fixed), evaluate(protect_long_term), evaluate(lambda d,b:"U"), evaluate(lambda d,b:"B")

def main():
    for target in (3, 4, 5):
        solve, fixed, capacity, always_urgent, always_long = study(target)
        print("B",target,"dynamic",solve(0,0),"fixed",fixed,
              "capacity",capacity,"all urgent",always_urgent,"all B",always_long)
    solve, fixed, capacity, _, _ = study(4)
    assert solve(0,0)[1] == "U"
    assert solve(1,0)[1] == "B" and solve(1,2)[1] == "U"
    assert solve(0,0)[0] == F(27,2)
    assert fixed[0] == F(25,2)
    assert capacity == F(27,2)
    solve, fixed, capacity, _, _ = study(5)
    assert solve(0,0)[0] == F(39,4)
    assert fixed[0] == F(73,8)
    assert capacity == 9
    print("PASS: exhaustive exact-model assertions")

if __name__ == "__main__":
    main()
