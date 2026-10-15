"""ORDER01-05: zero-sum single-use ESCAPE, action order and credibility.
Player rows PRESS/VERIFY. Defender columns HIDE/ESCAPE. Exact Fraction.
All actual state transitions are identical; only order/incentives change.
"""
from fractions import Fraction as F
from functools import lru_cache
PAY=((F(3),F(-3)),(F(-2),F(2)))

def matrix_value(m):
 a,b=m[0];c,d=m[1];den=a-b-c+d
 ps={F(0),F(1)};qs={F(0),F(1)}
 if den:
  p=(d-c)/den;q=(d-b)/den
  if 0<=p<=1:ps.add(p)
  if 0<=q<=1:qs.add(q)
 lo=max(min(p*a+(1-p)*c,p*b+(1-p)*d) for p in ps)
 hi=min(max(q*a+(1-q)*b,q*c+(1-q)*d) for q in qs)
 assert lo==hi
 return lo

@lru_cache(None)
def solve(n,esc=True,regime='simultaneous'):
 if n==0:return F(0)
 if not esc:return F(3*n)
 h=solve(n-1,True,regime);e=solve(n-1,False,regime)
 m=((F(3)+h,F(-3)+e),(F(-2)+h,F(2)+e))
 if regime=='simultaneous':return matrix_value(m)
 if regime=='player_public_first':return max(min(row) for row in m)
 if regime=='defender_binding_first':
  return min(max(m[i][j] for i in (0,1)) for j in (0,1))
 raise ValueError(regime)

@lru_cache(None)
def announced(n,fee,esc=True):
 # Opponent promises a column; player reacts; opponent may THEN change
 # actual move, paying a transferred fee. This is not simultaneous talk.
 if n==0:return F(0)
 if not esc:return F(3*n)
 h=announced(n-1,fee,True);e=announced(n-1,fee,False)
 m=((F(3)+h,F(-3)+e),(F(-2)+h,F(2)+e))
 def message(j):
  return max(min(m[i][actual]+(fee if actual!=j else 0)
                 for actual in (0,1)) for i in (0,1))
 return min(message(0),message(1))

@lru_cache(None)
def scan_available(n,esc=True):
 if n==0:return F(0)
 if not esc:return F(3*n)
 h=scan_available(n-1,True);e=scan_available(n-1,False)
 base=matrix_value(((F(3)+h,F(-3)+e),(F(-2)+h,F(2)+e)))
 if n<2:return base
 scan=min(F(3)+scan_available(n-2,True),
          F(2)+scan_available(n-2,False))
 return max(base,scan)

def regression():
 cases={1:(-2,0,2),2:(0,F(3,2),5),3:(3,F(15,4),8),
        4:(6,F(51,8),11),5:(9,F(147,16),14)}
 for n in range(1,9):
  player=solve(n,True,'player_public_first')
  sim=solve(n,True,'simultaneous')
  defender=solve(n,True,'defender_binding_first')
  assert player<=sim<=defender
  if n in cases:assert (player,sim,defender)==tuple(map(F,cases[n]))
  assert sim==F(3*n-6)+F(3,2**(n-1))
  assert player==(F(-2) if n==1 else F(3*n-6))
  assert defender==F(3*n-1)
  assert announced(n,F(0))==player
  assert announced(n,F(5))==defender
  assert scan_available(n)==(
   F(0) if n==1 else F(3*n-6)+F(2,2**(n-2)))
  print(n,player,sim,defender,scan_available(n))
 assert [announced(1,F(c)) for c in range(6)]==list(map(F,[-2,-2,-1,0,1,2]))
 assert [announced(4,F(c)) for c in range(6)]==list(map(F,[6,6,6,6,7,11]))
 assert scan_available(4)-solve(4,True,'simultaneous')==F(1,8)
 print('ORDER01-05 PASS')

if __name__=='__main__':regression()
