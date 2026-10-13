#!/usr/bin/env python3
"""REL-03: A preserved partial result becomes a method in the NEXT encounter.

Frozen two-stage abstract decision game; exact Fraction and exhaustive DP.
This is not a player evaluation or proof of novel gameplay. A previous USE is
a disclosed entry condition from REL-01's high-outside-offer branch.
"""
from fractions import Fraction as Q
from functools import lru_cache

def study(*,first_slot_required=False,matching_probability=Q(3,4)):
    p=matching_probability
    assert 0<=p<=1
    @lru_cache(None)
    def future(kind,slot,credential,enabled,done):
        if slot==2:return Q(0),()
        v,sequence=future(kind,slot+1,credential,enabled,done)
        options=[(Q(2)+v,("SIDE",)+sequence)]
        can_work=not done and (enabled or (kind=="M" and credential))
        if first_slot_required and kind=="M" and slot>0:
            can_work=False
        if can_work:
            v,seq=future(kind,slot+1,credential,enabled,True)
            options.append((Q(6)+v,("WORK",)+seq))
        # Same-cost on-site authorization; does not work retroactively.
        if not enabled and not done and not (kind=="M" and credential):
            v,seq=future(kind,slot+1,credential,True,done)
            options.append((v,("ENABLE",)+seq))
        return max(options,key=lambda pair:pair[0])

    M_early=future("M",0,True,False,False)
    N_early=future("N",0,True,False,False)
    M_later=future("M",0,False,False,False)
    N_later=future("N",0,False,False,False)
    # At the end of the first encounter there is one remaining action slot.
    # SECURE creates one credential, forfeiting a SIDE action worth 2.
    # WAIT takes the SIDE=2 and keeps no credential.
    prepare=p*M_early[0]+(1-p)*N_early[0]
    wait=Q(2)+p*M_later[0]+(1-p)*N_later[0]
    return {"p":p,"deadline":first_slot_required,"prepare":prepare,"wait":wait,
            "routes":(M_early,N_early,M_later,N_later)}

def main():
    for deadline in (False,True):
        for p in (Q(1,4),Q(1,2),Q(3,4),Q(1)):
            result=study(first_slot_required=deadline,matching_probability=p)
            prepare,wait=result["prepare"],result["wait"]
            assert prepare==Q(6)+2*p
            if deadline:
                assert wait==Q(8)-2*p
            else:
                assert wait==Q(8)
            m_pre,n_pre,m_late,n_late=result["routes"]
            assert (m_pre[0],n_pre[0],n_late[0])==(Q(8),Q(6),Q(6))
            assert m_late[0]==(Q(4) if deadline else Q(6))
            assert m_pre[1]==("WORK","SIDE")
            assert n_pre[1]==("ENABLE","WORK")
            if not deadline:
                assert wait>=prepare
            if deadline and p>Q(1,2):
                assert prepare>wait
            print("PORTABLE",{"deadline":deadline,"p":str(p),
                              "SECURE":str(prepare),"LATE_ENABLE":str(wait),
                              "best":"SECURE" if prepare>wait else "LATE_ENABLE" if wait>prepare else "TIE",
                              "routes":result["routes"]})
    print("PASS portable partial evidence = reusable-method option with ordinary ENABLE/timing boundary")

if __name__=="__main__":
    main()
