"""CASCADE16: first-test reversal sensitivity to wrong-claim penalty.
Reuse exact Bellman solver; change ONLY WRONG and clear cached states.
"""
from fractions import Fraction as F
import pattern_belief_revision_cascade_20261015 as m

def regression():
    original=m.WRONG
    results=[]
    try:
        for loss in (2,3,6,9,12,15,18,24):
            m.WRONG=-F(loss)
            m.dp.cache_clear()
            base=m.dp(m.PREMISES,4)
            repair=m.dp(m.PREMISES,4,allow_repair=True)
            assert base[1]==('TEST0','TEST3')
            assert repair[1]==('TEST1','TEST2')
            assert repair[0]>=base[0]
            if loss>=9:
                assert m.fixed_open_loop(4)[0]==4
            results.append((loss,base[0],repair[0]))
        print('CASCADE16 payoff-ablation:',results)
    finally:
        m.WRONG=original
        m.dp.cache_clear()
    assert m.dp(m.PREMISES,4)[0]==F(55,4)
    print('CASCADE16 PASS')

if __name__=='__main__':regression()
