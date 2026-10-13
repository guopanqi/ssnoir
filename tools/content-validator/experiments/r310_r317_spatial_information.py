#!/usr/bin/env python3
"""R310–R317: active observation and spatial belief.

4 moving cells, three 2-cell cameras; player moves at most one site
before current event. COVER pays +1 when target in its footprint but only
discloses the yes/no result; SCAN pays 0, reports exact current target.
Movement/observations occur before target Markov update. Public posterior
is the *only* controller state; private target is never inspected by DP.
"""
from fractions import Fraction as F
from functools import lru_cache
from collections import defaultdict
import json
SITES=((0,1),(1,2),(2,3)); PRIOR=(F(1,4),)*4; T=5
def trans(e,stay,jump):
    assert 0<=stay+jump<=1
    neighbors=(1,) if e==0 else (2,) if e==3 else (e-1,e+1)
    out=defaultdict(F)
    out[e]+=stay
    out[3-e]+=jump
    for n in neighbors:out[n]+=(1-stay-jump)/len(neighbors)
    return {n:v for n,v in out.items() if v}
def legal(site):return tuple(n for n in range(3) if abs(n-site)<=1)

def observations(prior,new,mode,exact_on_hit=False):
    groups=[]
    if mode=="S":
        groups=[(e,) for e in range(4)]
    elif exact_on_hit:
        groups=[(e,) for e in SITES[new]]
        groups.append(tuple(e for e in range(4) if e not in SITES[new]))
    else:
        groups=[tuple(e for e in range(4) if e in SITES[new]),
                tuple(e for e in range(4) if e not in SITES[new])]
    for obs in groups:
        prob=sum(prior[e] for e in obs)
        if prob:yield obs,prob

def model(stay,jump,allow_scan=True,exact_on_hit=False):
    @lru_cache(None)
    def V(t,site,prior):
        if t==T:return F(0)
        return max(Q(t,site,prior,n,mode) for n in legal(site)
                   for mode in (("C","S") if allow_scan else ("C",)))
    def Q(t,site,prior,n,mode):
        total=F(0)
        for obs,prob in observations(prior,n,mode,exact_on_hit):
            nxt=[F(0)]*4
            for e in obs:
                for k,p in trans(e,stay,jump).items():nxt[k]+=prior[e]*p/prob
            immediate=sum(prior[e] for e in obs if mode=="C" and e in SITES[n])
            total+=immediate+prob*V(t+1,n,tuple(nxt))
        return total
    root=V(0,1,PRIOR)
    qs={f"{n}/{mode}":Q(0,1,PRIOR,n,mode) for n in legal(1)
        for mode in (("C","S") if allow_scan else ("C",))}
    strict_scan=[]
    reach={(1,PRIOR):F(1)}
    for t in range(T):
        nd=defaultdict(F)
        for (site,prior),weight in reach.items():
            q={f"{n}/{mode}":Q(t,site,prior,n,mode) for n in legal(site)
               for mode in (("C","S") if allow_scan else ("C",))}
            winners=[key for key,val in q.items() if val==max(q.values())]
            if len(winners)==1 and winners[0].endswith("/S"):
                strict_scan.append((t,site,prior,weight,q))
            chosen=sorted(winners)[0]
            n=int(chosen[0]);mode=chosen[-1]
            for obs,prob in observations(prior,n,mode,exact_on_hit):
                nxt=[F(0)]*4
                for e in obs:
                    for k,p in trans(e,stay,jump).items():nxt[k]+=prior[e]*p/prob
                nd[(n,tuple(nxt))]+=weight*prob
        assert sum(nd.values())==1
        reach=nd
    return root,qs,strict_scan,V.cache_info().currsize

def independent_policy(stay,jump,rule):
    # Separate forward distribution of public beliefs, no DP or Q invoked.
    reach={(1,PRIOR):F(1)}
    total=F(0)
    for turn in range(T):
        nd=defaultdict(F)
        for (site,prior),mass in reach.items():
            if rule=="center":new=1
            elif rule=="greedy":new=max(legal(site),key=lambda s:(
                sum(prior[e] for e in SITES[s]),-abs(s-1)))
            else:raise ValueError(rule)
            for obs,prob in observations(prior,new,"C"):
                total+=mass*sum(prior[e] for e in obs if e in SITES[new])
                nxt=[F(0)]*4
                for e in obs:
                    for nxtcell,p in trans(e,stay,jump).items():nxt[nxtcell]+=prior[e]*p/prob
                nd[(new,tuple(nxt))]+=mass*prob
        assert sum(nd.values())==1
        reach=nd
    return total
def jsonize(v):
    if isinstance(v,F):return str(v)
    if isinstance(v,dict):return {str(k):jsonize(z) for k,z in v.items()}
    if isinstance(v,(tuple,list)):return [jsonize(z) for z in v]
    return v
def audit():
    data={}
    for name,stay,jump in (("persistent",F(3,4),F(0)),
                            ("nonlocal",F(1,2),F(1,4)),
                            ("static",F(1),F(0))):
        active,qs,scan_strict,states=model(stay,jump)
        no_scan,_,_,_=model(stay,jump,allow_scan=False)
        exact,_,_,_=model(stay,jump,allow_scan=False,exact_on_hit=True)
        greedy=independent_policy(stay,jump,"greedy")
        center=independent_policy(stay,jump,"center")
        assert active==no_scan>=greedy>=center
        assert exact>=no_scan
        assert not scan_strict
        data[name]={"stay":stay,"jump":jump,"full_with_scan":active,
                    "cover_only":no_scan,"scan_option_gap":active-no_scan,
                    "exact_location_if_covered":exact,"loss_from_only_binary_feedback":exact-active,
                    "greedy_observation":greedy,"center_only":center,
                    "scan_strict_witnesses":scan_strict,"reachable_DP_states":states,
                    "root_Q":qs}
    assert data["persistent"]["cover_only"]==F(32873,8192)
    assert data["persistent"]["greedy_observation"]==F(30365,8192)
    assert data["nonlocal"]["cover_only"]==data["nonlocal"]["greedy_observation"]==F(27389,8192)
    assert data["nonlocal"]["exact_location_if_covered"]==F(28545,8192)
    assert data["static"]["cover_only"]==F(17,4)
    assert data["static"]["center_only"]==F(5,2)
    return jsonize({
      "schema":"ssnoir.r310-r317.partial-spatial-observation.v1",
      "scope":"exact paper POMDP; no real environment / human observation",
      "rules":"5 turns, 4 target cells, 3 sites covering 2 neighboring cells; public prior and last binary event only. Move within one site before target realized; COVER rewards 1 on membership and reveals yes/no, SCAN rewards 0 and reveals exact cell; update Markov after feedback.",
      "R310_partial_feedback_persistent":data["persistent"],
      "R311_partial_feedback_nonlocal":data["nonlocal"],
      "R312_static_target_control":data["static"],
      "R313_remove_paid_SCAN":{k:{"with_scan":v["full_with_scan"],"without":v["cover_only"]} for k,v in data.items()},
      "R314_better_passive_exact_feedback":{k:v["exact_location_if_covered"] for k,v in data.items()},
      "R315_strong_simple_policies":{k:{"greedy":v["greedy_observation"],"center":v["center_only"]} for k,v in data.items()},
      "R316_public_Q_and_no_strict_scan":{k:{"root_Q":v["root_Q"],"strict_scan":len(v["scan_strict_witnesses"])} for k,v in data.items()},
      "R317_novelty":"Partial observation has a measurable value compared with exact position reporting, but adding a paid full-info SCAN is never optimal in three frozen environments. Spatial/information composition, NOT an independent investigation mode."
    })
if __name__=="__main__":print(json.dumps(audit(),ensure_ascii=False,indent=2))
