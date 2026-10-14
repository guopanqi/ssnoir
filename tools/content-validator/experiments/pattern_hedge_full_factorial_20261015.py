"""Independent complete finite-horizon DP for the HEDGE two-factor design."""
from functools import lru_cache
from fractions import Fraction as F
from itertools import product
@lru_cache(None)
def V(t,early,parallel,va,vb,oa,ob,la,lb,wa,wb):
 if t==6:return F(-4*(bool(la)+bool(lb))),'END'
 actions=[(F(1)+advance(t+1,early,parallel,va,vb,oa,ob,la,lb,wa,wb),'SIDE')]
 if not oa and (not early or t==1) and (parallel or not lb):
  z=sum((F(1,2)*advance(t+1,early,parallel,v,vb,1,ob,t,lb,0,wb) for v in (5,14)),F())
  actions.append((z,'OPEN_A'))
 if not ob and (parallel or not la):
  z=sum((F(1,2)*advance(t+1,early,parallel,va,v,oa,1,la,t,wa,0) for v in (0,14)),F())
  actions.append((z,'OPEN_B'))
 if la:
  if wa:actions.append((F(va)+advance(t+1,early,parallel,va,vb,oa,ob,0,lb,0,wb),'WORK_A'))
  else:actions.append((advance(t+1,early,parallel,va,vb,oa,ob,la,lb,1,wb),'WORK_A'))
  actions.append((advance(t+1,early,parallel,va,vb,oa,ob,0,lb,0,wb),'DROP_A'))
 if lb:
  if wb:actions.append((F(vb)+advance(t+1,early,parallel,va,vb,oa,ob,la,0,wa,0),'WORK_B'))
  else:actions.append((advance(t+1,early,parallel,va,vb,oa,ob,la,lb,wa,1),'WORK_B'))
  actions.append((advance(t+1,early,parallel,va,vb,oa,ob,la,0,wa,0),'DROP_B'))
 return max(actions,key=lambda z:z[0])
def advance(t,early,parallel,va,vb,oa,ob,la,lb,wa,wb):
 penalty=0
 if la and t-1-la>=3:penalty-=4;la=0;wa=0
 if lb and t-1-lb>=3:penalty-=4;lb=0;wb=0
 return penalty+V(t,early,parallel,va,vb,oa,ob,la,lb,wa,wb)[0]
def root(early,parallel):
 return V(1,early,parallel,-1,-1,0,0,0,0,0,0)
if __name__=='__main__':
 expected={(True,True):F(51,4),(True,False):F(47,4),(False,True):F(51,4),(False,False):F(51,4)}
 for e,p in product((True,False),repeat=2):
  got=root(e,p)
  assert got[0]==expected[e,p],(e,p,got)
  print('early',e,'parallel',p,'optimal',got)
 assert root(True,True)[0]-root(True,False)[0]==1
 assert root(False,True)[0]-root(False,False)[0]==0
 print('PASS four factorial cells and interaction contrast',V.cache_info())
