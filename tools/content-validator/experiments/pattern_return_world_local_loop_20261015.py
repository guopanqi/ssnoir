"""RETURN01-12: small L->W->L->W research model; exact Fractions.
Never equate a staged interface with an irreducible new architecture.
"""
from fractions import Fraction as F
from itertools import product
from collections import defaultdict

Q=F(3,4)
STARTS=('OPEN','GUARD')
WORLDS=(0,1)


def support_p(start,k):
    return ((F(3,4) if k else F(1,4)) if start=='OPEN'
            else (F(1,2) if k else F(1,8)))


def ready_after(start,h,choice,action,k):
    """P(true result, A ready, B ready, irreversible breach, local outcome)."""
    if choice=='DECLINE':
        if action=='MEND':return ((F(1),True,False,F(),'A_MENDED'),)
        if action=='KEEP':return ((F(1),bool(not h or start=='GUARD'),False,F(),'UNCHANGED'),)
    if choice=='ACCEPT':
        if action=='LINK':
            p=support_p(start,k)
            return ((p,True,True,F(),'LINK_SUCCESS'),
                    (1-p,bool(not h or start=='GUARD'),False,F(2),'LINK_FAIL'))
        if action=='CUT':return ((F(1),False,True,F(),'A_SACRIFICED'),)
        if action=='ABANDON':return ((F(1),True,False,F(2),'B_ABANDONED'),)
    raise ValueError((start,h,choice,action,k))


def world_final(a,b,w,belief=None,alt=5):
    """A/B distinct result identities; w=0 favors A and w=1 favors B.
    If belief provided, stage3 has NOT learned the true demand w.
    """
    p=F(belief) if belief is not None else F(w)
    options={'ALT':F(alt),'SIDE':F(1)}
    if a:options['A']=(1-p)*14+p*3
    if b:options['B']=(1-p)*3+p*14
    best=max(options.values())
    return best,tuple(k for k,v in options.items() if v==best)


def local_action_value(start,h,choice,action,k,pw,see_world=True,alt=5):
    total=F()
    for chance,a,b,breach,_ in ready_after(start,h,choice,action,k):
        if see_world:
            gain=sum(((pw if w else 1-pw)*world_final(a,b,w,alt=alt)[0]
                      for w in WORLDS),F())
        else:
            gain=world_final(a,b,0,belief=pw,alt=alt)[0]
        total+=chance*(gain-breach)
    return total


def local_policy(start,h,choice,k,pw,see_world=True,alt=5):
    actions=('MEND','KEEP') if choice=='DECLINE' else ('LINK','CUT','ABANDON')
    scores={action:local_action_value(start,h,choice,action,k,pw,see_world,alt)
            for action in actions}
    best=max(scores.values())
    return best,tuple(a for a in actions if scores[a]==best),scores


def world_policy(start,h,pw,see_support=True,see_world=True,alt=5,
                 force=None,link_only=False):
    """W choice -> K revealed -> local action; ablate K visibility."""
    options={}
    for choice in ('DECLINE','ACCEPT'):
        if force is not None and choice!=force:continue
        ks=(0,1)
        if see_support:
            gain=sum((F(1,2)*local_policy(start,h,choice,k,pw,see_world,alt)[0]
                      for k in ks),F())
        else:
            actions=('MEND','KEEP') if choice=='DECLINE' else ('LINK','CUT','ABANDON')
            if link_only and choice=='ACCEPT':actions=('LINK',)
            gain=max(sum((F(1,2)*local_action_value(start,h,choice,a,k,pw,see_world,alt)
                          for k in ks),F()) for a in actions)
        if link_only and choice=='ACCEPT':
            gain=sum(F(1,2)*local_action_value(start,h,choice,'LINK',k,pw,see_world,alt)
                     for k in ks)
        options[choice]=gain
    best=max(options.values())
    return best,tuple(a for a in ('DECLINE','ACCEPT') if a in options and options[a]==best),options


def root(see_forecast=True,see_support=True,see_world=True,alt=5,
         force=None,link_only=False,hazard=F(1,2)):
    roots={}
    for start in STARTS:
        result=F()
        for h,y in product((0,1),repeat=2):
            ph=(F(hazard) if h else 1-F(hazard)) if start=='OPEN' else (F() if h else F(1))
            if not ph:continue
            posterior=(Q if y else 1-Q) if see_forecast else F(1,2)
            result+=ph*F(1,2)*world_policy(start,h,posterior,see_support,see_world,alt,force,link_only)[0]
        roots[start]=result
    best=max(roots.values())
    return best,tuple(a for a,v in roots.items() if v==best),roots


def follow_true_world(start='OPEN',alt=5):
    """Independently replay real H,Y,W and supporter K + LINK success."""
    sums=defaultdict(F)
    history=[]
    for h,y,w,k in product((0,1),repeat=4):
        ph=F(1,2) if start=='OPEN' else (F() if h else F(1))
        mass=ph*F(1,2)*(Q if y==w else 1-Q)*F(1,2)
        if not mass:continue
        pw=Q if y else 1-Q
        wc=world_policy(start,h,pw)[1][0]
        la=local_policy(start,h,wc,k,pw)[1][0]
        for chance,a,b,pen,fb in ready_after(start,h,wc,la,k):
            if not chance:continue
            score,final=world_final(a,b,w,alt=alt)
            selected=final[0]
            p=mass*chance
            sums['mass']+=p
            sums['score']+=p*(score-pen)
            sums['breach']+=p*pen
            sums['world_accept']+=p*(wc=='ACCEPT')
            sums['local_link']+=p*(la=='LINK')
            sums['local_cut']+=p*(la=='CUT')
            sums['world_A']+=p*(selected=='A')
            sums['world_B']+=p*(selected=='B')
            sums['world_ALT']+=p*(selected=='ALT')
            sums['Aready']+=p*a
            sums['Bready']+=p*b
            if la=='LINK' and fb=='LINK_FAIL' and selected=='ALT':
                sums['failure_to_alt']+=p
            if h==0 and y==1 and wc=='ACCEPT':
                sums['K_changes_action_case']+=p
            history.append((h,y,w,k,wc,la,fb,selected,p,score-pen))
    assert sums['mass']==1
    assert sums['score']==root()[2][start]
    return dict(sums),history


def regress():
    baseline=root()
    assert baseline==(F(1509,128),('OPEN',),{'OPEN':F(1509,128),'GUARD':F(47,4)})
    assert world_policy('OPEN',1,F(1,4))[1]==('DECLINE',)
    assert world_policy('OPEN',1,F(3,4))[1]==('ACCEPT',)
    assert local_policy('OPEN',0,'ACCEPT',0,F(3,4))[1]==('CUT',)
    assert local_policy('OPEN',0,'ACCEPT',1,F(3,4))[1]==('LINK',)
    assert local_policy('OPEN',0,'ACCEPT',0,F(1,4))[1]==('LINK',)
    data,hist=follow_true_world()
    print('full root',baseline,'real world',data)
    for kwargs in ({'see_forecast':False},{'see_support':False},{'see_world':False},
                   {'see_forecast':False,'see_support':False},
                   {'see_support':False,'see_world':False},
                   {'force':'ACCEPT'},{'force':'DECLINE'},
                   {'link_only':True},{'alt':8},{'hazard':F(3,4)}):
        score,pol,values=root(**kwargs)
        assert score<=baseline[0] or kwargs in ({'alt':8},)
        print('ABLATE',kwargs,score,pol,values)
    pw=F(3,4)
    visible=world_policy('OPEN',0,pw,True)[2]['ACCEPT']
    hidden=world_policy('OPEN',0,pw,False)[2]['ACCEPT']
    assert visible>hidden
    assert local_policy('OPEN',0,'ACCEPT',0,pw)[1]!=local_policy('OPEN',0,'ACCEPT',1,pw)[1]
    print('RETURN01-12 PASS')

if __name__=='__main__':regress()
