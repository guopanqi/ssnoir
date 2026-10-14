# 可转移准备与后到揭晓：四行动精确枚举
# Python 3 standard library, deterministic exhaustive state search.
from functools import lru_cache
from fractions import Fraction
from itertools import product

def solve(before, mode):
    after = 4 - before
    @lru_cache(None)
    def walk(t, a, b, flex, side, converted, typ):
        if t == 0:
            return Fraction(side + ((10 if typ == "A" else 8)
                         if (a if typ == "A" else b) >= 3 else 0)), ()
        moves = [("A", (a+1,b,flex,side,converted)),
                 ("B", (a,b+1,flex,side,converted)),
                 ("S", (a,b,flex,side+1,converted))]
        if flex and not converted and mode in ("single", "bulk"):
            moved = 1 if mode == "single" else flex
            state = ((a+moved,b,flex-moved,side,True) if typ=="A"
                     else (a,b+moved,flex-moved,side,True))
            moves.append(("C", state))
        results = [(walk(t-1,*st,typ)[0],
                    (name,)+walk(t-1,*st,typ)[1]) for name,st in moves]
        return max(results, key=lambda row:row[0])

    policies = []
    for sequence in product("ABFS", repeat=before):
        a,b,f,s = [sequence.count(x) for x in "ABFS"]
        if mode == "none" and f:
            continue
        endings = []
        for typ in "AB":
            aa,bb,ff = a,b,f
            if mode == "free":
                if typ=="A": aa+=f
                else: bb+=f
                ff=0
            endings.append(walk(after,aa,bb,ff,s,mode=="free",typ))
        policies.append((sum((v[0] for v in endings),Fraction())/2,
                         "".join(sequence), endings))
    maximum = max(x[0] for x in policies)
    return maximum, [x for x in policies if x[0]==maximum]

def test():
    expected = {
        "none": ["10","10","9","13/2","6"],
        "single":["10","10","9","13/2","6"],
        "bulk": ["10","10","9","9","6"],
        "free": ["10","10","10","10","10"],
    }
    for name, values in expected.items():
        for before, target in enumerate(values):
            result, policies = solve(before,name)
            assert result == Fraction(target), (name,before,result,target)
            assert policies
    print("PASS: 20 complete-menu optimizations agree with expected rational values")
    for before in range(5):
        print("reveal_after",before,[(name,str(solve(before,name)[0]))
                                   for name in expected])
if __name__=="__main__":
    test()
