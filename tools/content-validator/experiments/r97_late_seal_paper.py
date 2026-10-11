#!/usr/bin/env python3
"""R97 independent PAPER foil to P20: optional *late* sealing.
Not an edited native scene, not human behavior. Two rounds open;
three if sealed. 4 actions/round. Seal costs one die, repair costs two,
talk gains 0/1/2 with 1/6,2/6,3/6 probability. A failed talk or
round transition costs one composure. 10 progress plus an exit wins.
"""
import json,sys
from functools import lru_cache
from fractions import Fraction as F
ODDS=((0,F(1,6)),(1,F(2,6)),(2,F(3,6)))

def solve(goal,health):
    def maker(allow):
        @lru_cache(None)
        def dp(p,h,turn,slots,sealed,ladder):
            if h<=0:return F(0)
            if p>=goal and (not sealed or ladder==2):return F(1)
            if turn >=2+sealed:return F(0)
            if slots==0:
                if turn+1>=2+sealed:return F(0)
                return dp(p,h-1,turn+1,4,sealed,ladder)
            vals=[]
            if p<goal:vals.append(sum(pr*dp(min(goal,p+g),h-int(g==0),
                    turn,slots-1,sealed,ladder) for g,pr in ODDS))
            if sealed and ladder<2:vals.append(dp(p,h,turn,slots-1,sealed,ladder+1))
            if allow and not sealed:vals.append(dp(p,h,turn,slots-1,1,ladder))
            return max(vals,default=F(0))
        return dp
    full=maker(True);disabled=maker(False)
    @lru_cache(None)
    def trace(p,h,turn,slots,sealed,ladder):
        if h<=0 or (p>=goal and (not sealed or ladder==2)) or turn>=2+sealed:return (F(0),)*3
        if slots==0:
            if turn+1>=2+sealed:return (F(0),)*3
            return trace(p,h-1,turn+1,4,sealed,ladder)
        choices=[]
        if p<goal:choices.append(('talk',sum(pr*full(min(goal,p+g),h-int(g==0),
                      turn,slots-1,sealed,ladder) for g,pr in ODDS)))
        if sealed and ladder<2:choices.append(('repair',full(p,h,turn,slots-1,sealed,ladder+1)))
        if not sealed:choices.append(('seal',full(p,h,turn,slots-1,1,ladder)))
        v=max(val for _,val in choices)
        action=next(a for a,x in choices if x==v) # talk before seal on tie
        if action=='seal':
            strict=v>max(val for a,val in choices if a!='seal')
            child=trace(p,h,turn,slots-1,1,ladder)
            return (child[0]+1,child[1]+int(strict),child[2]+int(strict and turn==1))
        if action=='repair':return trace(p,h,turn,slots-1,sealed,ladder+1)
        acc=[F(0)]*3
        for g,pr in ODDS:
            child=trace(min(goal,p+g),h-int(g==0),turn,slots-1,sealed,ladder)
            for k in range(3):acc[k]+=pr*child[k]
        return tuple(acc)
    s=(0,health,0,4,0,0)
    optimum=full(*s);without=disabled(*s)
    forced=full(0,health,0,3,1,0)
    first_talk=sum(pr*full(g,health-int(g==0),0,3,0,0) for g,pr in ODDS)
    visits=trace(*s)
    assert optimum>=without and optimum>=forced and optimum==first_talk
    return dict(goal=goal,initial_composure=health,optimal=float(optimum),
       never_seal=float(without),seal_immediately=float(forced),
       independent_seal_gain=float(optimum-without),
       optimal_path_strict_seal_visits=float(visits[1]),
       optimal_path_strict_seal_in_second_round=float(visits[2]),
       optimal_path_total_seal_visits=float(visits[0]))
def main():
    rows=[solve(g,h) for g,h in [(8,4),(10,3),(10,4),(10,5),(12,4)]]
    key=next(x for x in rows if x['goal']==10 and x['initial_composure']==4)
    assert 0.028<key['independent_seal_gain']<0.030
    assert 0.096<key['optimal_path_strict_seal_visits']<0.098
    assert key['optimal_path_strict_seal_visits']==key['optimal_path_strict_seal_in_second_round']
    out=dict(id='R97',evidence='Exact Fraction bounded paper model; not native P20 or human play',
             key='Optional *late* route commitment, not forced opening seal',cases=rows)
    s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
    if len(sys.argv)>1:
        from pathlib import Path
        Path(sys.argv[1]).write_text(s,encoding='utf8')
    else:print(s)
if __name__=='__main__':main()
