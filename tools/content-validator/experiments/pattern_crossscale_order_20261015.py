"""CROSS-SCALE01: order constraints can change reachable histories without changing optimum."""
from itertools import product
def run(path,requires_a):
 a=b=0; unlocked=False
 for action in path:
  if action=="A":
   a+=1
   if requires_a and a==2:unlocked=True
  elif action=="B":
   if not requires_a or unlocked:b+=1
 return (a>=2,b>=2)
def enumerate_policies(requires_a):
 paths=["".join(v) for v in product("AB",repeat=4)]
 res={s:run(s,requires_a) for s in paths}
 both=[s for s,v in res.items() if v==(True,True)]
 maximum=max(8*x+8*y for x,y in res.values())
 return maximum,both,res
if __name__=="__main__":
 separate=enumerate_policies(False)
 coupled=enumerate_policies(True)
 assert separate[0]==coupled[0]==16
 assert len(separate[1])==6
 assert coupled[1]==["AABB"]
 assert separate[2]["BBAA"]==(True,True)
 assert coupled[2]["BBAA"]==(True,False)
 assert coupled[2]["AABB"]==(True,True)
 print("independent",separate[0],separate[1])
 print("coupled",coupled[0],coupled[1])
 print("PASS 16 schedules, 2 outcomes, legal-order counterexample")
