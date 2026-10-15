"""STRATEGIC01/02: a genuinely choosing counterpart in finite repeated interaction.
For each public history, use backward induction with exact Fraction utilities.
This is a complete-information pure-strategy finite game, not an empirical test.
"""
from fractions import Fraction as F
from functools import lru_cache
from itertools import product

PLAYER_SUCCESS, PLAYER_FAIL, PLAYER_SAFE = F(6), F(-4), F(1)
FEE, EFFORT_COST = F(1), F(2)

def game(periods,bonus=F(0)):
    bonus=F(bonus)
    @lru_cache(None)
    def subgame(remaining,history):
        if remaining==0:return F(0),F(0),None
        after_wait=subgame(remaining-1,history+'W')
        safe=(PLAYER_SAFE+after_wait[0],after_wait[1],'SAFE')
        after_work=subgame(remaining-1,history+'E')
        after_shirk=subgame(remaining-1,history+'S')
        work=(PLAYER_SUCCESS-bonus+after_work[0],
              FEE+bonus-EFFORT_COST+after_work[1], 'OFFER->EFFORT')
        shirk=(PLAYER_FAIL+after_shirk[0],
               FEE+after_shirk[1], 'OFFER->SHIRK')
        # Other agent chooses its best response, shirks when tied;
        # player chooses SAFE if offer and SAFE are exactly equal.
        reply=work if work[1]>shirk[1] else shirk
        return reply if reply[0]>safe[0] else safe
    all_subgames={}
    for length in range(periods):
        for prefix in product('WES',repeat=length):
            h=''.join(prefix)
            all_subgames[h]=subgame(periods-length,h)
    return subgame(periods,''),all_subgames

def regression():
    for n in (1,2,3,4,5):
        for b in range(7):
            value,histories=game(n,b)
            if b<=2 or b>=5:
                assert value==(F(n),F(0),'SAFE')
                assert all(x[2]=='SAFE' for x in histories.values())
            else:
                assert b in (3,4)
                assert value==(F(n*(6-b)),F(n*(b-1)),'OFFER->EFFORT')
                assert all(x[2]=='OFFER->EFFORT' for x in histories.values())
            if n==3:print('rounds',n,'verified_bonus',b,'player_other_action',value)
    assert game(3,0)[0][0]==3
    assert game(3,3)[0][0]==9
    assert FEE-EFFORT_COST<FEE
    print('STRATEGIC01/02 PASS')

if __name__=='__main__':regression()
