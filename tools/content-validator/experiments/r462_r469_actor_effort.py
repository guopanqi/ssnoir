#!/usr/bin/env python3
"""R462-469: scarce effort of a self-interested counterpart; no scripted policy."""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product
import json
T=3
BASE=((0,0),(0,1),(0,1))
def solve(effort=2,coop=2,bonus=2,tie='hostile',actor='long'):
    @lru_cache(None)
    def state(t,e):
        if t==T:return (F(0),F(0))
        return tuple(sum(move(t,e,shock)[3][j] for shock in (0,1))/2 for j in (0,1))
    def move(t,e,shock):
        q={}
        for a in 'AB':
            legal='RAB' if e else 'R'
            options=[]
            for b in legal:
                gain=F(0 if b=='R' else BASE[t]['AB'.index(b)]+bonus*int(b=='AB'[shock])+coop*int(b==a))
                future=state(t+1,e-int(b!='R'))
                pay=(F(1+int(b!='R' and b!=a))+future[0],gain+future[1])
                own_objective=gain if actor=='myopic' else pay[1]
                options.append((b,pay,own_objective,gain))
            top=max(x[2] for x in options)
            ties=[x for x in options if x[2]==top]
            chosen=(min if tie=='hostile' else max)(ties,key=lambda x:(x[1][0],x[0]))
            q[a]=chosen
        a=max('AB',key=lambda a:(q[a][1][0],-ord(a)))
        return a,q[a][0],q[a],q[a][1],q
    root=state(0,effort)
    def replay(tape):
        e=effort;total=[F(0),F(0)];trace=[]
        for t,shock in enumerate(tape):
            a,b,chosen,_,q=move(t,e,shock)
            gain=F(0 if b=='R' else BASE[t]['AB'.index(b)]+bonus*int(b=='AB'[shock])+coop*int(b==a))
            local=(F(1+int(b!='R' and b!=a)),gain)
            total=[total[i]+local[i] for i in range(2)]
            trace.append({'t':t,'shock':shock,'effort_before':e,'player':a,'other':b,'player_gain':local[0],'other_gain':local[1]})
            e-=int(b!='R')
        return tuple(total),trace
    tape_vals=[replay(tape)[0] for tape in product((0,1),repeat=T)]
    independent=tuple(sum(p[k] for p in tape_vals)/len(tape_vals) for k in (0,1))
    assert root==independent,(root,independent)
    return root,move,replay

def audit():
    (p,o),move,replay=solve()
    assert (p,o)==(F(4),F(7))
    r0=[move(0,2,x) for x in (0,1)]
    assert [(z[0],z[1]) for z in r0]==[('B','R'),('A','R')]
    assert (r0[0][4]['A'][0],r0[1][4]['B'][0])==('A','B')
    r1=[move(1,2,x) for x in (0,1)]
    assert [(z[0],z[1]) for z in r1]==[('A','A'),('A','B')]
    myopic=solve(actor='myopic')[0]
    assert myopic==(F(7,2),F(13,2)),myopic
    no_coop=solve(coop=0)[0]
    assert no_coop==(F(5),F(5)),no_coop
    friendly=solve(tie='friendly')[0]
    assert friendly==(F(4),F(7)),friendly
    blind=[sum(move(0,2,s)[4][a][1][0] for s in (0,1))/2 for a in 'AB']
    assert blind==[F(15,4),F(15,4)],blind
    e3=solve(effort=3)[0]
    assert e3==(F(4),F(10)),e3
    all_paths={''.join(map(str,t)):replay(t)[1] for t in product((0,1),repeat=T)}
    assert all(x[0]['player']==('B' if x[0]['shock']==0 else 'A')
        and all(y['player']=='A' for y in x[1:]) for x in all_paths.values())
    return {'schema':'ssnoir.r462-r469.actor-effort-v1',
        'protocol':'3 turns, player observes independent uniform public shock A/B then chooses A/B. Another autonomous self-interested actor sees this move and chooses A, B or REST with OWN effort budget 2 (work costs 1), independently maximizing its three-turn payoff with future opportunities. Own payoff for job b is public BASE[t,b]+2 if b matches shock+2 if b matches player; rest0; BASE is 0/0,0/1,0/1 over t. Player gets one for working plus one if counterpart performs DIFFERENT work, duplicate zero extra. Other anticipates future player optimal responses and future public shocks, ties resolve hostile/friendly to player. Public complete-information sequential game, not a hidden social NPC.',
        'R462_long_horizon':(p,o),
        'R463_first_shock_conditional':[{'shock':i,'player':r0[i][0],'other':r0[i][1]} for i in (0,1)],
        'R464_second_turn_changed_response':[{'shock':i,'player':r1[i][0],'other':r1[i][1]} for i in (0,1)],
        'R465_myopic_actor':myopic,'R466_no_cooperation_bonus':no_coop,
        'R467_friendly_tie':friendly,'R468_no_first_shock_access':blind,
        'R469_extra_counterpart_effort':e3,'independent_replay_all_8_shock_tapes':all_paths,
        'verdict':'The second actor really chooses REST/work to maximize scarce own effort across future public opportunities and responds to the player; it is no longer an exogenous scripted action. Yet all 8 reachable shock tapes share the tiny player policy: first choose task opposite to public shock, thereafter choose A. More effort helps the other but not player. Game-theoretic self-interest and changed expected payoff are NOT by themselves a novel repeatable human gameplay loop.'}

def enc(x):
    if isinstance(x,F):return str(x)
    if isinstance(x,dict):return {str(k):enc(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)):return [enc(z) for z in x]
    return x
if __name__=='__main__':print(json.dumps(enc(audit()),ensure_ascii=False,indent=2))
