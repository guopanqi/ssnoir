"""Independent forward replay of HEDGE public policies; no optimization solver."""
from fractions import Fraction
from itertools import product
VALUE={"A":{"H":14,"L":5},"B":{"H":14,"L":0}}
def play(events,actions,early=True,overlap=True):
 live={}; progress={}; opened=set();done=set();side=0;penalty=0
 for t,act in enumerate(actions,1):
  kind=act[0]
  if kind=="S":side+=1
  elif kind=="O":
   obj=act[1]
   assert obj not in opened and (not early or obj!="A" or t==1)
   assert overlap or not live
   live[obj]=t;progress[obj]=0;opened.add(obj)
  elif kind=="W":
   obj=act[1];assert obj in live
   progress[obj]+=1
   if progress[obj]==2:done.add(obj);live.pop(obj);progress.pop(obj)
  elif kind=="D":
   obj=act[1];assert obj in live
   live.pop(obj);progress.pop(obj)
  for obj in list(live):
   if t-live[obj]>=3:
    penalty+=4;live.pop(obj);progress.pop(obj)
 penalty+=4*len(live)
 return sum(VALUE[obj][events[obj]] for obj in done)+side-penalty,done
def plan(name,event):
 a,b=event["A"],event["B"]
 if name=="overlap":
  return "OA WA WA S S" if a=="H" else ("OA OB DA WB WB" if b=="H" else "OA OB WA WA DB")
 if name=="serial":
  return "OA WA WA S S" if a=="H" else ("OA DA OB WB WB" if b=="H" else "OA DA OB DB S")
 return "OB WB WB S S" if b=="H" else "OB DB OA WA WA"
def main():
 expected={"overlap":(Fraction(51,4),Fraction(3,4),Fraction(1,4)),
           "serial":(Fraction(47,4),Fraction(1,2),Fraction(1,4)),
           "late_reopen":(Fraction(51,4),Fraction(1,2),Fraction(1,2))}
 for name in expected:
  results=[]
  for a,b in product("HL",repeat=2):
   events={"A":a,"B":b}
   actions=plan(name,events).split()
   results.append(play(events,actions,early=name!="late_reopen",overlap=name=="overlap"))
  observed=(Fraction(sum(x[0] for x in results),4),
   Fraction(sum("A" in x[1] for x in results),4),
   Fraction(sum("B" in x[1] for x in results),4))
  assert observed==expected[name],(name,observed)
  print(name,observed)
 print("PASS independent forward replay of 12 policy-event trajectories")
if __name__=="__main__":main()
