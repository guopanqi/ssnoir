#!/usr/bin/env python3
"""SSNoir S2-A: R110 revelation timing, R111 generated opportunity,
R112 expiring offer. Exact Fraction independent paper models; no native or human evidence."""
from fractions import Fraction as F
from functools import lru_cache
import json
G=((0,F(1,3)),(1,F(1,3)),(2,F(1,3)))
def r110():
 K=4;q=F(1,4);prob=F(1,2)
 def terminal(p,found,n,close):return n==0 or (p>=3 and (found or close))
 def reward(p,found):return F(p>=3)+2*F(found)
 def acts(p,k,found):return (['work'] if p<3 else [])+(['explore'] if not found else [])+(['probe'] if k>1 and not found else [])
 def trans(p,k,found,n,a):
  if a=='work':
   return [(v*F(1,3),min(3,p+g),kk,found,n-1) for g in range(3) for v,kk in (((q,1),(1-q,k)) if k>1 else ((F(1),1),)) if v]
  if a=='explore':return [(F(1,k),p,k,True,n-1),(F(k-1,k),p,k-1,False,n-1)] if k>1 else [(F(1),p,1,True,n-1)]
  return [(F(1),p,1,found,n-1)]
 @lru_cache(None)
 def known(p,k,found,n,close):
  if terminal(p,found,n,close):return reward(p,found)
  return max(sum(ch*known(pp,kk,ff,nn,close) for ch,pp,kk,ff,nn in trans(p,k,found,n,a)) for a in acts(p,k,found))
 def blind(reveal_at):
  @lru_cache(None)
  def val(p,k,found,n):
   if n==0 or (p>=3 and found):return reward(p,found)
   if p>=3:return prob*reward(p,found)+(1-prob)*known(p,k,found,n,False)
   if n==reveal_at:return prob*known(p,k,found,n,True)+(1-prob)*known(p,k,found,n,False)
   return max(sum(ch*val(pp,kk,ff,nn) for ch,pp,kk,ff,nn in trans(p,k,found,n,a)) for a in acts(p,k,found))
  return val(0,K,False,4)
 outset=prob*known(0,4,False,4,True)+(1-prob)*known(0,4,False,4,False)
 first=blind(3);second=blind(2);never=blind(-1)
 assert outset>=first>=second>=never
 state=(1,4,False,3)
 qvals={str(cl):{a:str(sum(ch*known(pp,kk,ff,nn,cl) for ch,pp,kk,ff,nn in trans(*state,a))) for a in acts(*state[:3])} for cl in (False,True)}
 assert outset==first and first>second and second==never
 return {'r':'R110','prior_closed':'1/2','outset_revealed':str(outset),'after_first':str(first),'after_second':str(second),'never':str(never),'revelation_gain':str(first-never),'delay_loss':str(first-second),'decision_example':qvals}
def r111():
 def study(cause,value):
  @lru_cache(None)
  def V(p,offered,done,n,mode):
   if n==0 or p>=4:return F(p>=4)+value*F(done)
   work=sum(ch*V(min(4,p+g),offered or (((g==2) if cause=='good' else (g==0)) if cause!='disabled' else False),done,n-1,mode) for g,ch in G)
   if mode=='never' or not offered:return work
   help_now=V(p,False,True,n-1,mode)
   if mode=='always':return help_now
   return max(work,help_now)
  free=V(0,False,False,4,'free');always=V(0,False,False,4,'always');never=V(0,False,False,4,'never')
  assert free>=always and free>=never
  return {'trigger':cause,'optional_value':str(value),'optimal':str(free),'always_help':str(always),'never_help':str(never),'adaptive_above_simple':str(free-max(always,never))}
 rows=[study(c,v) for v in (F(1,2),F(1),F(3,2),F(2)) for c in ('good','bad','disabled')]
 return {'r':'R111','results':rows}
def r112():
 def study(value):
  def solve(expiring,mode):
   @lru_cache(None)
   def V(p,offered,done,n):
    if p>=4 or n==0:return F(p>=4)+value*F(done)
    work=sum(ch*V(min(4,p+g),offered and not expiring,done,n-1) for g,ch in G)
    if not offered or done:return work
    help_now=V(p,False,True,n-1)
    if mode=='never':return work
    if mode=='always':return help_now
    return max(work,help_now)
   return sum(ch*(F(1,2)*V(g,True,False,4)+F(1,2)*V(g,False,False,4)) for g,ch in G)
  persistent=solve(False,'free');expires=solve(True,'free');none=solve(True,'never');always=solve(True,'always')
  assert persistent>=expires>=none and expires>=always
  return {'optional_value':str(value),'persistent':str(persistent),'expires_next_turn':str(expires),'expire_loss':str(persistent-expires),'no_help':str(none),'always_help':str(always),'adaptive_above_simple':str(expires-max(none,always))}
 rows=[study(v) for v in (F(1,3),F(1,2),F(3,5),F(1),F(3,2))]
 assert all(x['adaptive_above_simple']=='0' for x in rows)
 return {'r':'R112','results':rows}
def main():
 print(json.dumps({'stage':'S2-A','scope':'Exact Fraction synthetic paper models; not C#/Scheme, not human play','studies':[r110(),r111(),r112()]},indent=2,ensure_ascii=False))
if __name__=='__main__':main()
