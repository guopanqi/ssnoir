#!/usr/bin/env python3
"""R47独立明确策略执行与局部树，不导入父模型。"""
import hashlib
import itertools
import json
import math
import time
from collections import Counter
from fractions import Fraction as F
from functools import cache
from pathlib import Path

HERE = Path(__file__).resolve().parent
FACES = {
    1: {1:(0,0,1,1,1,2),2:(0,1,1,1,2,2),3:(0,1,1,2,2,2),
        4:(1,1,2,2,2,2),5:(1,2,2,2,2,2),6:(2,2,2,2,2,2)},
    2: {1:(0,1,1,1,2,2),2:(0,1,1,2,2,2),3:(1,1,2,2,2,2),
        4:(1,2,2,2,2,2),5:(2,2,2,2,2,2),6:(2,2,2,2,2,2)},
}


def terminal(needs):
    if needs[0] == 0:
        return (F(0),F(1),F(0))
    if needs[1] == 0:
        return (F(0),F(0),F(1))
    return None


@cache
def tail(need,hand,skill):
    coeff = [1]
    for die in hand:
        new = [0]*(len(coeff)+2)
        for power,count in enumerate(coeff):
            for grade in FACES[skill][die]:
                new[power+grade] += count
        coeff = new
    return F(sum(coeff[max(0,need):]),6**len(hand))


def direct_tail(need,hand,skill):
    return F(sum(sum(x)>=need for x in itertools.product(*(FACES[skill][d] for d in hand))),
             6**len(hand))


def lock_values(needs,hand):
    return tuple((t+1)*tail(needs[t],hand,2) for t in range(2))


def locks(needs,hand):
    vals = lock_values(needs,hand)
    return (0 if vals[0]>=vals[1] else 1),max(vals)


def minsum(hand):
    return sum(min(FACES[2][d]) for d in hand)


def subtract(hand,reserve):
    rest = list(hand)
    for d in reserve:
        rest.remove(d)
    return tuple(rest)


@cache
def reserves(hand,policy):
    if policy == "top2":
        return (hand[-2:],) if len(hand)>=2 else ()
    counts = Counter(hand)
    faces = sorted(counts)
    out=[]
    for nums in itertools.product(*(range(counts[d]+1) for d in faces)):
        out.append(tuple(d for d,n in zip(faces,nums) for _ in range(n)))
    return tuple(sorted(out,key=lambda r:(len(r),r)))


def plans(needs,hand,policy):
    out=[]
    for r in reserves(hand,policy):
        support=minsum(r)
        if support>=needs[0]:
            s=subtract(hand,r)
            g=1+tail(needs[1]-support,s,1)
            out.append((r,s,g))
    return out


def choose(needs,hand,policy):
    target,l=locks(needs,hand)
    ps=plans(needs,hand,policy)
    if ps:
        r,s,g=max(ps,key=lambda row:row[2])
        if g>l:
            assert s, (needs,hand,r,l,g)
            return "advance",s[0],r,g
    return "lock",target,(),l


def move(needs,target,grade,mode="risk"):
    out=list(needs)
    gain=grade if mode=="risk" else int(grade==2)
    out[target]=max(0,out[target]-gain)
    return tuple(out)


def mix(rows):
    return tuple(sum((r[i] for r in rows),F(0))/len(rows) for i in range(3))


def score(distribution):
    return distribution[1]+2*distribution[2]


@cache
def execute(needs,hand,policy):
    done=terminal(needs)
    if done is not None:
        return done
    if not hand:
        return F(1),F(0),F(0)
    kind,pick,_,_=choose(needs,hand,policy)
    if kind=="lock":
        p=tail(needs[pick],hand,2)
        return (1-p,p,F(0)) if pick==0 else (1-p,F(0),p)
    rest=list(hand)
    rest.remove(pick)
    return mix([execute(move(needs,1,g),tuple(rest),policy) for g in FACES[1][pick]])


@cache
def free_value(needs,hand):
    done=terminal(needs)
    if done is not None:
        return score(done)
    if not hand:
        return F(0)
    vals=list(lock_values(needs,hand))
    for die,t,mode in itertools.product(sorted(set(hand)),range(2),("risk","steady")):
        rest=list(hand)
        rest.remove(die)
        vals.append(sum((free_value(move(needs,t,g,mode),tuple(rest))
                         for g in FACES[1][die]),F(0))/6)
    return max(vals)


def sf(v):
    return f"{v.numerator}/{v.denominator}"


def encoded_choice(needs,hand,policy):
    kind,pick,r,estimate=choose(needs,hand,policy)
    return {"kind":kind,"pick":pick,"reserve":r,"estimate":sf(estimate)}


def main():
    started=time.perf_counter()
    localhands=list(itertools.combinations_with_replacement(range(1,7),2))
    localhands+=list(itertools.combinations_with_replacement(range(1,7),3))
    polychecks=gchecks=0
    cases=[]
    local_gaps=[]
    for hand in localhands:
        for need,skill in itertools.product(range(7),(1,2)):
            assert tail(need,hand,skill)==direct_tail(need,hand,skill)
            polychecks+=1
        for a,b in itertools.product(range(1,4),range(1,7)):
            needs=(a,b)
            l=max(lock_values(needs,hand))
            fv=free_value(needs,hand)
            row={"needs":needs,"dice":hand,"L":sf(l),"free":sf(fv)}
            for policy in ("top2","all_reserves"):
                for r,s,g in plans(needs,hand,policy):
                    assert minsum(r)>=a
                    direct=F(sum(2 if sum(grades)+minsum(r)>=b else 1 for grades in
                                  itertools.product(*(FACES[1][d] for d in s))),6**len(s))
                    assert direct==g
                    gchecks+=1
                dist=execute(needs,hand,policy)
                val=score(dist)
                assert sum(dist)==1 and l<=val<=fv
                row[policy]=sf(val)
                row[policy+"_choice"]=encoded_choice(needs,hand,policy)
            cases.append(row)
            if F(row["all_reserves"])<fv:
                local_gaps.append(row)
    initial=[]
    totals={p:[F(0)]*3 for p in ("top2","all_reserves")}
    for hand in itertools.combinations_with_replacement(range(1,7),4):
        mult=math.factorial(4)
        for n in Counter(hand).values():
            mult//=math.factorial(n)
        weight=F(mult,1296)
        row={"dice":hand,"weight":sf(weight)}
        for p in totals:
            dist=execute((3,6),hand,p)
            row[p]={"value":sf(score(dist)),"distribution":list(map(sf,dist)),
                    "choice":encoded_choice((3,6),hand,p)}
            for i in range(3):
                totals[p][i]+=weight*dist[i]
        initial.append(row)
    assert sum(F(row["weight"]) for row in initial)==1
    for d in totals.values():
        assert sum(d)==1
    # 只追加一个正常根1145，查储备只接受保证乙的遗漏；不做126根自由优化。
    hand=(1,1,4,5)
    needs=(3,6)
    fv=free_value(needs,hand)
    root={"dice":hand,"free":sf(fv),"L":sf(max(lock_values(needs,hand)))}
    for p in totals:
        root[p]={"value":sf(score(execute(needs,hand,p))),"choice":encoded_choice(needs,hand,p)}
    root["first_1_risk_B_branches"]=[]
    for grade in range(3):
        nxt=move(needs,1,grade)
        root["first_1_risk_B_branches"].append({"grade":grade,"needs":nxt,
            "free":sf(free_value(nxt,(1,4,5))),"L":sf(max(lock_values(nxt,(1,4,5)))),
            "top2":sf(score(execute(nxt,(1,4,5),"top2"))),
            "all_reserves":sf(score(execute(nxt,(1,4,5),"all_reserves")))})
    # 协调者明确授权追加单个正常根2245，定位保证阈值的遗漏。
    needs=(3,6)
    hand=(2,2,4,5)
    reserve=(4,5)
    trial=(2,2)
    root2={"dice":hand,"free":sf(free_value(needs,hand)),
           "L":sf(max(lock_values(needs,hand))),
           "guaranteed_G":sf(1+tail(6-minsum(reserve),trial,1))}
    assert F(root2["free"])==F(91,54)
    assert F(root2["L"])==F(44,27) and F(root2["guaranteed_G"])==F(13,9)
    for p in totals:
        root2[p]={"value":sf(score(execute(needs,hand,p))),"choice":encoded_choice(needs,hand,p)}
    sums=Counter(sum(gs) for gs in itertools.product(*(FACES[1][d] for d in trial)))
    reserve_sums=Counter(sum(gs) for gs in itertools.product(*(FACES[2][d] for d in reserve)))
    plan_by_sum={total:max(lock_values((3,max(0,6-total)),reserve)) for total in sums}
    plan=sum((F(count,36)*plan_by_sum[total] for total,count in sums.items()),F(0))
    assert plan==F(91,54)
    root2["trial_contribution_distribution"]={str(s):sf(F(n,36)) for s,n in sorted(sums.items())}
    root2["reserve_contribution_distribution"]={str(s):sf(F(n,36)) for s,n in sorted(reserve_sums.items())}
    root2["plan_terminal_value_by_trial_sum"]={str(s):sf(v) for s,v in sorted(plan_by_sum.items())}
    root2["probabilistic_fixed_plan_value"]=sf(plan)
    root2["first_2_risk_B_branches"]=[]
    for grade in range(3):
        nxt=move(needs,1,grade)
        root2["first_2_risk_B_branches"].append({"grade":grade,"needs":nxt,
            "free":sf(free_value(nxt,(2,4,5))),"L":sf(max(lock_values(nxt,(2,4,5))))})
    out={"status":"passed","scope":"独立两/三骰局部与126初始明确策略执行；仅追加正常根1145/2245自由，不是全初始自由独立优化",
         "local_conditions":len(cases),"poly_direct_checks":polychecks,"G_direct_checks":gchecks,
         "cases":cases,"local_all_reserve_gaps":local_gaps,"initial_strategy_execution":initial,
         "initial_strategy_totals":{p:{"value":sf(score(tuple(d))),"distribution":list(map(sf,d))}
                                    for p,d in totals.items()},
         "normal_root_1145":root,"normal_root_2245":root2,"literal_fate":FACES,
         "source_sha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
         "elapsed_seconds":time.perf_counter()-started}
    (HERE/"审查储备结果.json").write_text(json.dumps(out,ensure_ascii=False,indent=2)+"\n")
    print(json.dumps({k:out[k] for k in ("status","local_conditions","poly_direct_checks","G_direct_checks",
        "initial_strategy_totals","normal_root_1145","normal_root_2245","elapsed_seconds")},ensure_ascii=False))


if __name__=="__main__":
    main()
