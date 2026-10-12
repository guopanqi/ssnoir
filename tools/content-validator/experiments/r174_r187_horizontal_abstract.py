#!/usr/bin/env python3
"""SSNoir abstract mechanism experiments R174-R187.

No narrative labels, no official SSNoir content changes. All stochastic
probabilities are exact fractions. Independent open-loop enumeration is
separate from recursively solved feedback policies.
"""
from collections import defaultdict
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json

def fjson(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):fjson(x) for k,x in v.items()}
    if isinstance(v,(list,tuple)):return [fjson(x) for x in v]
    return v

def reciprocal(target=(3,3),T=5,p=F(1,2),coupled=True):
    """Every A makes a token that can boost a later B, and vice versa.
    If a token exists, attempting to consume it succeeds with probability p.
    Producing the token is certain, so the current move's outcome is public.
    """
    S0=(0,0,0,0) # x,y,A-gift,B-gift
    def step(s,a,bonus):
        x,y,h,k=s
        if a=="A":
            x+=1+int(bool(k and bonus and coupled))
            k=0 if bonus else k
            h=int(coupled)
        else:
            y+=1+int(bool(h and bonus and coupled))
            h=0 if bonus else h
            k=int(coupled)
        return (min(x,target[0]),min(y,target[1]),h,k)
    def trans(s,a):
        if not coupled:return {step(s,a,0):F(1)}
        out=defaultdict(F)
        for bonus,pr in ((0,1-p),(1,p)):
            out[step(s,a,bonus)]+=pr
        return dict(out)
    def reward(s):
        x,y,*_=s
        return F(4)*int(x==target[0])+F(4)*int(y==target[1])+F(x+y,10)
    @lru_cache(None)
    def opt(t,s):
        if t==T:return reward(s)
        return max(q(t,s,a) for a in "AB")
    def q(t,s,a):
        return sum(prob*opt(t+1,ns) for ns,prob in trans(s,a).items())
    def value_sequence(seq):
        states={S0:F(1)}
        for a in seq:
            after=defaultdict(F)
            for s,m in states.items():
                for ns,prob in trans(s,a).items():after[ns]+=m*prob
            states=after
        return sum(m*reward(s) for s,m in states.items())
    allseq={''.join(seq):value_sequence(seq) for seq in product("AB",repeat=T)}
    fixed=max(allseq.values())
    examples=[k for k,v in allseq.items() if v==fixed][:5]
    mass={S0:F(1)}
    reachable=[]
    for t in range(T):
        nxt=defaultdict(F)
        for s,m in mass.items():
            options={a:q(t,s,a) for a in "AB"}
            if options["A"]!=options["B"]:
                reachable.append(dict(t=t,state=s,mass=m,options=options,
                                      strict_best=max(options,key=options.get)))
            choice=max("AB",key=lambda a:options[a])
            for ns,prob in trans(s,choice).items():nxt[ns]+=m*prob
        mass=nxt
    return dict(value=opt(0,S0),best_fixed=fixed,fixed_examples=examples,
                fixed_gap=opt(0,S0)-fixed,reachable_strict=reachable,
                max_reachable_states=len(mass),optimal_terminal_mass=sum(mass.values()))

def bank(T=5,q=F(1,4),shield=True,decay_at=None):
    """G risks all unbanked reserve; S bank converts and saves; P protects
    one next G at cost one action. Unbanked reserve is worthless at end.
    Decay happens AFTER player action at a public boundary.
    """
    acts=("G","S","P") if shield else ("G","S")
    S0=(0,0,False) # pending, secured, shield
    def trans(s,a):
        r,b,g=s
        if a=="S":return {(0,b+r,g):F(1)}
        if a=="P":return {(r,b,True):F(1)}
        out=defaultdict(F)
        for inc in (0,2):
            for spoiled in (False,True):
                prob=F(1,2)*(int(not spoiled) if g else (q if spoiled else 1-q))
                if prob:out[(0 if spoiled else r+inc,b,False)]+=prob
        return dict(out)
    def boundary(t,s):
        if decay_at is not None and t in decay_at:
            return (0,s[1],s[2])
        return s
    @lru_cache(None)
    def opt(t,s):
        if t==T:return F(s[1])
        return max(qvalue(t,s,a) for a in acts)
    def qvalue(t,s,a):
        return sum(m*opt(t+1,boundary(t+1,ns)) for ns,m in trans(s,a).items())
    def evaluate_sequence(seq):
        dist={S0:F(1)}
        for t,a in enumerate(seq):
            nd=defaultdict(F)
            for s,m in dist.items():
                for ns,prob in trans(s,a).items():
                    nd[boundary(t+1,ns)]+=m*prob
            dist=nd
        return sum(m*F(s[1]) for s,m in dist.items())
    vals={''.join(s):evaluate_sequence(s) for s in product(acts,repeat=T)}
    best_fixed=max(vals.values())
    fixed_seq=sorted(k for k,v in vals.items() if v==best_fixed)[:4]
    mass={S0:F(1)}
    strict=[]
    for t in range(T):
        nextstates=defaultdict(F)
        for s,m in mass.items():
            qvals={a:qvalue(t,s,a) for a in acts}
            best=max(qvals.values())
            winners=[a for a,v in qvals.items() if v==best]
            if len(winners)==1 and len(set(qvals.values()))>1:
                strict.append(dict(t=t,state=s,mass=m,best=winners[0],options=qvals))
            choice=winners[0]
            for ns,prob in trans(s,choice).items():
                nextstates[boundary(t+1,ns)]+=m*prob
        mass=nextstates
    return dict(value=opt(0,S0),best_fixed=best_fixed,
                best_fixed_examples=fixed_seq,adaptive_gap=opt(0,S0)-best_fixed,
                reachable_strict=strict,terminal_mass=sum(mass.values()))

def edit_grammar(T=4):
    """Simple action 'change verb from A to B' counterexample.
    Changing only the button name/target is ordinary setup + progress.
    """
    options=("work","edit","wait")
    def advance(s,a):
        x,y,mode=s
        if a=="work":
            return (min(x+(mode==0),2),min(y+(mode==1),1),mode)
        if a=="edit":
            return (x,y,1)
        return s
    def score(s):
        x,y,_=s
        return 5*int(x==2 and y==1)+2*int(x==2)+int(y==1)
    rows=[]
    for plan in product(options,repeat=T):
        s=(0,0,0)
        for act in plan:s=advance(s,act)
        rows.append((score(s),''.join("W" if a=="work" else "E" if a=="edit" else "-" for a in plan),s))
    best=max(v for v,*_ in rows)
    winners=sorted((seq for v,seq,_ in rows if v==best))[:6]
    assert best==8 and winners==["WWEW"]
    return dict(best=best,optimal_schedules=winners,mechanics="edit changes next work output; fixed WWEW completes both goals",is_new_action_language=False)

def study():
    a=reciprocal()
    assert a["value"]==F(647,80)
    assert a["best_fixed"]==F(303,40)
    assert a["fixed_gap"]==F(41,80)
    assert a["optimal_terminal_mass"]==1
    a_off=reciprocal(coupled=False)
    a_deterministic=reciprocal(p=F(1))
    assert a_deterministic["fixed_gap"]==0
    a_harder=reciprocal(target=(4,4))
    assert a_harder["value"]==F(433,80)
    assert a_harder["best_fixed"]==F(187,40)
    a_pref={name for x in a_harder["reachable_strict"] for name in [x["strict_best"]]}
    assert a_pref=={"A","B"} # opposite strict choices on true feedback path
    b=bank()
    assert b["value"]==F(2523,1024)
    assert b["best_fixed"]==F(37,16)
    no_guard=bank(shield=False)
    assert no_guard["value"]==F(2457,1024)
    assert b["value"]>no_guard["value"]
    no_risk=bank(q=F(0))
    assert no_risk["value"]==4
    assert any(x["best"]=="P" for x in b["reachable_strict"])
    assert any(x["best"]=="S" for x in b["reachable_strict"])
    highrisk=bank(q=F(1,2))
    assert highrisk["value"]==2
    shortbank=bank(decay_at=(2,))
    assert shortbank["value"]<=b["value"]
    grammar=edit_grammar()
    return fjson({
      "schema":"ssnoir.abstract.horizontal.r174-r187.v1",
      "evidence":"exact paper simulations; 2 independent policy evaluation routes; not human or native",
      "R174_reciprocity_random":a,
      "R175_remove_reciprocity":a_off,
      "R176_perfect_feedback_degeneracy":a_deterministic,
      "R177_different_target_4_4":a_harder,
      "R178_real_path_both_preferences":sorted(a_pref),
      "R179_first_family_assessment":"P02 + recent-action echo R50; feedback usefulness does not certify new verb; next test needs novel production choice",
      "R180_stake_and_bank":b,
      "R181_remove_protect":no_guard,
      "R182_no_risk":no_risk,
      "R183_high_risk":highrisk,
      "R184_forced_between_cycle_decay":shortbank,
      "R185_normal_path_distinct_verbs":sorted(set(x["best"] for x in b["reachable_strict"])),
      "R186_simple_action_rewrite_counterexample":grammar,
      "R187_cross_family_novelty":"reciprocal boosts change sequence efficiency; bank/protect is existing P09/P10; edit-only just unlocks a new progress target. Do not promote as independent."
    })
if __name__=="__main__":
    print(json.dumps(study(),ensure_ascii=False,indent=2))
