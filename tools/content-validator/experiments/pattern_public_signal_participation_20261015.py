"""PLEDGE10-14: signal accuracy and independent actor self-interest.
Exact complete-world Fraction enumeration. Keep audience's own decision
separate from the player's objective weight on B.
"""
from fractions import Fraction as F

GOOD,BAD=F(4),F(-6)
PUBLISH,FALSE,FALLBACK=F(2),F(12),F(1)

def invest(belief):
    return belief*GOOD+(1-belief)*BAD>0

def scenario(q=F(3,4),w=F(1),can_warn=True):
    q,w=F(q),F(w)
    assert F(1,2)<=q<=1 and w>=0
    rows=[]
    for s in (0,1):
        p_good=q if s else 1-q
        plans=[]
        for first in (('HOLD','ISSUE') if s else ('HOLD',)):
            value=F()
            details=[]
            for t in (0,1):
                prob=p_good if t else 1-p_good
                ainvest=first=='ISSUE' and invest(q)
                ar=(GOOD if t else BAD) if ainvest else F()
                pb=PUBLISH if first=='ISSUE' else F()
                reput=-FALSE if first=='ISSUE' and not t else F()
                choices=[]
                for last in (('SIDE','SEND') if can_warn else ('SIDE',)):
                    binvest=(bool(t) if last=='SEND' else
                             first=='ISSUE' and invest(q))
                    br=(GOOD if t else BAD) if binvest else F()
                    side=FALLBACK if last=='SIDE' else F()
                    util=ar+w*br+pb+reput+side
                    choices.append((util,last,ar,br,pb,reput,side))
                best=max(v[0] for v in choices)
                chosen=next(v for v in choices if v[0]==best)
                value+=prob*best
                details.append((t,prob,chosen))
            plans.append((value,first,details))
        peak=max(v for v,_,_ in plans)
        rows.append((s,peak,tuple(first for v,first,_ in plans if v==peak),
                     plans))
    root=sum((r[1] for r in rows),F())/2
    return root,tuple(rows)

def regression():
    assert not invest(F(3,5)) and invest(F(13,20))
    assert invest(F(3,4))
    for q in (F(1,2),F(3,5),F(13,20),F(7,10),F(3,4),F(9,10)):
        value,rows=scenario(q)
        publish=('ISSUE' in rows[1][2])
        assert publish==(q>F(17,24))
        expected=(F(5,2) if q<=F(17,24) else
                  F(1,2)*(F(27)*q-F(16))+
                  F(1,2)*(F(4)-F(3)*q))
        assert value==expected,(q,value,expected)
        print('accuracy',q,'audience_invests',invest(q),
              'publicize_positive',publish,'value',value)
    for w in (F(0),F(1,6),F(1,4),F(1,2),F(1)):
        value,rows=scenario(w=w)
        alternative,_=scenario(w=w,can_warn=False)
        if w>F(1,6):
            assert rows[1][3][1][2][0][2][1]=='SEND'
        if w==0:
            assert rows[1][3][1][2][0][2][1]=='SIDE'
        assert value>=alternative
        print('B_weight',w,'with_verified_messages',value,
              'without_messages',alternative)
    assert scenario()[0]==F(3)
    assert scenario(can_warn=False)[0]==F(2)
    print('PLEDGE10-14 PASS')

if __name__=='__main__':regression()
