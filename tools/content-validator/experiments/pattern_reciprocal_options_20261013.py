#!/usr/bin/env python3
"""GRC01-04: selfish consent, future replacement, verifiable commitment, info timing.

Backward induction with Fraction. Player: SAFE=3; if helper HELP, gets 8
now, then sees future HIGH=9 or LOW=1 each probability 1/2. Player may
REPAY 3 and obtain future grant; if it pays, helper strictly prefers GRANT
worth 4 over late EXIT worth 2. Player can alternatively BYPASS with fee k
if enabled. Helper's early EXIT earns outside w; HELP yields the expected
later gain of 4 when REPAY happens, otherwise late EXIT worth 2.
No private types, repeated-game equilibrium, humans or emotional rewards.
"""
from fractions import Fraction as F

HIGH,LOW=F(9),F(1)
VALUES=(HIGH,LOW)
REP_COST=F(3)
HELP_NOW=F(8)
PLAYER_SAFE=F(3)
HELPER_GRANT=F(4)
HELPER_LATE_EXIT=F(2)

def later(v,bypass=None,tie='repay',enforce=False,exclusive=False):
    if enforce:
        # A publicly executable contract signed BEFORE HELP: future
        # HIGH -> REPAY and GRANT, LOW -> no repayment and late EXIT.
        return ('CONTRACT' if v==HIGH else 'DEFAULT',
                v-REP_COST if v==HIGH else F(0),
                HELPER_GRANT if v==HIGH else HELPER_LATE_EXIT)
    choices={'DEFAULT':F(0),'REPAY':v-REP_COST}
    if bypass is not None and not exclusive: choices['BYPASS']=v-F(bypass)
    priority=('REPAY','BYPASS','DEFAULT') if tie=='repay' else ('BYPASS','REPAY','DEFAULT')
    act=max(choices,key=lambda a:(choices[a],-priority.index(a)))
    return act,choices[act],HELPER_GRANT if act=='REPAY' else HELPER_LATE_EXIT

def game(early_outside=F(5,2),bypass=None,tie='repay',
         early_signal=False,enforce=False,exclusive=False,forced=False):
    outcomes=tuple(later(v,bypass,tie,enforce,exclusive) for v in VALUES)
    branches=[]
    for _,gain,partner_gain in outcomes:
        allow=forced or partner_gain>early_outside if early_signal else None
        branches.append((gain,partner_gain,allow))
    if not early_signal:
        partner_expected=sum(x[1] for x in branches)/2
        consent=forced or partner_expected>early_outside
        result=[('HELP',HELP_NOW+x[0],x[1]) if consent
                else ('EXIT',PLAYER_SAFE,early_outside) for x in branches]
    else:
        result=[('HELP',HELP_NOW+gain,partner_gain) if allow
                else ('EXIT',PLAYER_SAFE,early_outside)
                for gain,partner_gain,allow in branches]
    return (sum(x[1] for x in result)/2,
            sum(x[2] for x in result)/2,
            tuple(result),outcomes)

def main():
    w=F(5,2)
    original=game(w)
    assert original[:2]==(F(11),F(3))
    assert original[2][0][0]=='HELP' and original[2][1][0]=='HELP'
    assert original[3][0][0]=='REPAY' and original[3][1][0]=='DEFAULT'
    for k in range(6):
        for tie in ('repay','bypass'):
            res=game(w,bypass=k,tie=tie)
            if k<3:assert res[:2]==(F(3),w)
            if k>3:assert res[:2]==(F(11),F(3))
            if k==3:assert (res[0]==11)==(tie=='repay')
            print('GRC01 fallback',k,tie,'player/helper',res[:2])
    assert game(w,bypass=2,forced=True)[0]==F(23,2)
    assert game(w,bypass=2,exclusive=True)[:2]==original[:2]
    assert game(w,bypass=2,enforce=True)[:2]==original[:2]
    # Holding helper policy fixed, cheaper BYPASS helps the player;
    # when helper can anticipate it, early HELP ceases altogether.
    print('GRC02 commitment/bypass',{'unbound':game(w,bypass=2)[:2],
          'contract':game(w,bypass=2,enforce=True)[:2],
          'exclusive':game(w,bypass=2,exclusive=True)[:2],
          'forced':game(w,bypass=2,forced=True)[:2]})
    expected={
        F(3,2):((F(11),F(3)),(F(11),F(3))),
        F(5,2):((F(17,2),F(13,4)),(F(11),F(3))),
        F(7,2):((F(17,2),F(15,4)),(F(3),F(7,2))),
        F(9,2):((F(3),F(9,2)),(F(3),F(9,2)))
    }
    for outside,(early,late) in expected.items():
        assert game(outside,early_signal=True)[:2]==early
        assert game(outside,early_signal=False)[:2]==late
        print('GRC03 outside',outside,'early_public',early,'late_public',late,
              'best_player_timing', 'early' if early[0]>late[0] else
              'late' if late[0]>early[0] else 'either')
    assert game(w,early_signal=True,forced=True)[:2]==(F(11),F(3))
    assert game(w,early_signal=False,forced=True)[:2]==(F(11),F(3))
    assert game(w,early_signal=True,bypass=2)[:2]==(F(3),w)
    print('PASS: full backward responses, cheap outside option, early disclosure reversal')

if __name__=='__main__':main()
