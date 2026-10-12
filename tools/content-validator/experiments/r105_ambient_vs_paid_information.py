#!/usr/bin/env python3
"""R105: Information from ordinary work competes with dedicated probing.
Four actions. Primary goal>=3 earns 1; optional correct site earns 2.
K possible sites (uniform prior); exploring one uses an action, finds
the optional result with chance 1/K, otherwise eliminates that site.
Paid probe costs one action, reveals correct site; ordinary work gains
0/1/2 equiprobably and independently reveals it with chance q.
Once both outcomes complete there is no further utility. Compare full
DP to direct removal of paid probe and to 'probe first'. Track strictly
better probes on the tie-broken optimal path. PAPER proxy only.
"""
from functools import lru_cache
from fractions import Fraction as F
import json,sys

def audit(q,K):
 @lru_cache(None)
 def val(a,k,got,n,allow):
  if n==0 or (a>=3 and got):return F(a>=3)+2*F(got)
  opts=[]
  if a<3:
   work=F(0)
   for g in (0,1,2):
    aa=min(3,a+g)
    work+=F(1,3)*(q*val(aa,1,got,n-1,allow)+(1-q)*val(aa,k,got,n-1,allow)) if k>1 else F(1,3)*val(aa,1,got,n-1,allow)
   opts.append(work)
  if not got:
   opts.append(F(1,k)*val(a,k,True,n-1,allow)+F(k-1,k)*val(a,k-1,False,n-1,allow) if k>1 else val(a,1,True,n-1,allow))
  if allow and not got and k>1:opts.append(val(a,1,got,n-1,allow))
  return max(opts)
 @lru_cache(None)
 def forward(a,k,got,n):
  if n==0 or (a>=3 and got):return F(0),F(0)
  choices={}
  if a<3:
   choices['work']=sum(
    (F(1,3)*(q*val(min(3,a+g),1,got,n-1,True)+(1-q)*val(min(3,a+g),k,got,n-1,True))
      if k>1 else F(1,3)*val(min(3,a+g),1,got,n-1,True)) for g in (0,1,2))
  if not got:
   choices['explore']=(F(1,k)*val(a,k,True,n-1,True)+F(k-1,k)*val(a,k-1,False,n-1,True)
                       if k>1 else val(a,1,True,n-1,True))
  if k>1 and not got:choices['probe']=val(a,1,got,n-1,True)
  best=max(choices.values())
  action=next(x for x in ('work','explore','probe') if x in choices and choices[x]==best)
  if action=='probe':
   child=forward(a,1,got,n-1)
   return F(1)+child[0],F(best>max(v for x,v in choices.items() if x!='probe'))+child[1]
  if action=='explore':
   yes=forward(a,k,True,n-1)
   if k==1:return yes
   no=forward(a,k-1,False,n-1)
   return ((yes[0]+(k-1)*no[0])/k,(yes[1]+(k-1)*no[1])/k)
  acc=[F(0),F(0)]
  for g in (0,1,2):
   aa=min(3,a+g)
   transitions=((q,1),(1-q,k)) if k>1 else ((F(1),1),)
   for chance,kk in transitions:
    child=forward(aa,kk,got,n-1)
    for i in range(2):acc[i]+=F(1,3)*chance*child[i]
  return tuple(acc)
 full=val(0,K,False,4,True);disabled=val(0,K,False,4,False)
 forced=val(0,1,False,3,True)
 paths=forward(0,K,False,4)
 assert full>=disabled and full>=forced
 return {'possible_sites':K,'natural_clue_chance':float(q),
         'optimal':float(full),'without_paid_probe':float(disabled),
         'probe_first':float(forced),'marginal_probe_value':float(full-disabled),
         'expected_probe_visits':float(paths[0]),'strict_probe_visits':float(paths[1])}
def main():
 rows=[audit(p,k) for k in (2,3,4,5) for p in (F(0),F(1,4),F(1,2),F(1))]
 assert all(r['marginal_probe_value']==0 for r in rows if r['possible_sites']==2)
 assert all(r['marginal_probe_value']==0 for r in rows if r['natural_clue_chance']==1)
 ex=next(r for r in rows if r['possible_sites']==4 and r['natural_clue_chance']==0.25)
 assert ex['marginal_probe_value']>0.2 and ex['strict_probe_visits']>0.5
 out={'study':'R105','scope':'Exact Fraction four-action paper model, not C#/Scheme or human play','results':rows}
 s=json.dumps(out,ensure_ascii=False,indent=2)+'\n'
 if len(sys.argv)>1:
  from pathlib import Path
  Path(sys.argv[1]).write_text(s,encoding='utf8')
 else:print(s)
if __name__=='__main__':main()
