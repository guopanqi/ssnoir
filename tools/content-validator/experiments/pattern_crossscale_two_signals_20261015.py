"""CROSS-SCALE07: two sequential world-target signals; exact Fraction policy and conditional traces."""
from itertools import product
from functools import lru_cache
from fractions import Fraction as F

ACTIONS = ("WORK", "GO_A", "GO_B", "SIDE")
HIDDEN = tuple(product("AB", repeat=2))

def solve(observe_first=True, observe_second=True, closed_t3=False):
    @lru_cache(None)
    def policy(t, location, seen_first, seen_second):
        if t == 6:
            return F(0), ()
        worlds = [(a,b) for a,b in HIDDEN
                  if (seen_first == "?" or seen_first == a)
                  and (seen_second == "?" or seen_second == b)]
        candidates = []
        for action in ACTIONS:
            if action.startswith("GO_"):
                if action[-1] == location or (closed_t3 and t == 3):
                    continue
                destination = action[-1]
            else:
                destination = location
            expected = F(0)
            for a,b in worlds:
                immediate = 1 + 4 * (location == (a if t < 3 else b)) if action == "WORK" else (1 if action == "SIDE" else 0)
                next_first = a if observe_first and t + 1 >= 1 else "?"
                next_second = b if observe_second and t + 1 >= 3 else "?"
                expected += immediate + policy(t+1, destination, next_first, next_second)[0]
            candidates.append((expected / len(worlds), action))
        best = max(v for v, _ in candidates)
        return best, tuple(a for v,a in candidates if v == best)

    def trace(first, second):
        location = "A"
        by_stage = [0,0]
        steps = []
        for t in range(6):
            known_first = first if observe_first and t >= 1 else "?"
            known_second = second if observe_second and t >= 3 else "?"
            action = policy(t, location, known_first, known_second)[1][0]
            gain = 1 + 4*(location == (first if t<3 else second)) if action == "WORK" else (1 if action == "SIDE" else 0)
            by_stage[t>=3] += gain
            steps.append(action)
            if action.startswith("GO_"):
                location = action[-1]
        return tuple(steps), tuple(by_stage)
    return policy(0,"A","?","?")[0], {a+b:trace(a,b) for a,b in HIDDEN}

def regression():
    for blocked, expected in [
        (False,{(False,False):F(18),(True,False):F(39,2),
                (False,True):F(43,2),(True,True):F(23)}),
        (True,{(False,False):F(18),(True,False):F(39,2),
               (False,True):F(39,2),(True,True):F(21)})]:
        outputs={flags:solve(*flags,closed_t3=blocked) for flags in expected}
        assert {k:v[0] for k,v in outputs.items()} == expected
        interaction=expected[True,True]-expected[True,False]-expected[False,True]+expected[False,False]
        assert interaction == 0
        for flags,(value,traces) in outputs.items():
            assert sum((F(sum(stages),4) for _,stages in traces.values()),F()) == value
        print("closed_t3",blocked, "roots",expected, "information_interaction",interaction)
        if not blocked:
            assert outputs[True,True][1]["BA"][0] == ("WORK","GO_B","WORK","GO_A","WORK","WORK")
            print("two_revision_witness_BA",outputs[True,True][1]["BA"])
    print("CROSS-SCALE07 PASS")

if __name__ == "__main__":
    regression()
