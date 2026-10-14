from functools import lru_cache
from fractions import Fraction

# Four actions; after the SECOND action, an unredeemed partner-held X can
# lose permission with probability 1/2. The owner may arrange action order.
def solve(revoke=True, recover=True, independent_b=True):
    @lru_cache(None)
    def v(t, owner, permit, redeemed, b, side, transferred):
        if not t:
            return Fraction(side + (7 if redeemed == 1 else 12 if redeemed == 2 else 0)
                            + (10 if b == 2 else 0)), ()
        candidates = []
        def try_action(label, s):
            if t == 3 and revoke and s[0] == 1 and s[1] == 1 and s[2] == 0:
                # The public revocation lottery occurs after action 2.
                x = v(t-1, s[0], 0, s[2], s[3], s[4], s[5])[0]
                y = v(t-1, *s)[0]
                score = (x+y)/2
            else:
                score = v(t-1, *s)[0]
            candidates.append((score,label))
        try_action('SIDE',(owner,permit,redeemed,b,side+1,transferred))
        if independent_b and b<2:
            try_action('B',(owner,permit,redeemed,b+1,side,transferred))
        if owner == 0 and not transferred and not redeemed:
            try_action('TRANSFER',(1,1,redeemed,b,side,1))
        if owner == 0 and not redeemed:
            try_action('REDEEM',(0,permit,1,b,side,transferred))
        if owner == 1 and permit and not redeemed:
            try_action('ASK',(1,permit,2,b,side,transferred))
        if owner == 1 and not redeemed and recover:
            try_action('RECOVER',(0,permit,redeemed,b,side,transferred))
        best=max(x[0] for x in candidates)
        return best,tuple(x[1] for x in candidates if x[0]==best)
    return v(4,0,0,0,0,0,0),v

if __name__=='__main__':
    expected={
        (True,True,True):22,
        (True,False,True):22,
        (False,True,True):22,
        (True,True,False):14,
        (False,False,True):22,
    }
    for args,ans in expected.items():
        (value,actions),v=solve(*args)
        assert value==ans,(args,value,ans)
        print(args, value, actions)
    print('5 exact Fraction regression cases PASS')
