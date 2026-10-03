"""独立Fraction核选定无头伤许可状态的下一轮；不复算完整三回合。"""
from pathlib import Path
from fractions import Fraction as F
from functools import lru_cache
from itertools import combinations_with_replacement
from math import factorial
import runpy,json
HERE=Path(__file__).resolve().parent
STRIPS={1:(2,3,1),2:(1,3,2),3:(1,2,3),4:(0,2,4),5:(0,1,5),6:(0,0,6)}
# 上表以骰面为键，技能1；与正式 prepared=d+1 的三档一致。
@lru_cache(None)
def exact(left,k,pressure,hand):
 if k>=7 or pressure>=5:return F(0)
 if left<=0:return F(1)
 best=F(0)
 for d in set(hand):
  tail=list(hand);tail.remove(d);tail=tuple(tail)
  for risky in (False,True):
   value=F(0)
   for grade,n in enumerate(STRIPS[d]):
    if not n:continue
    gain=grade if risky else int(grade==2);cost=int(grade==0 or risky and grade==1)
    value+=F(n,6)*exact(left-gain,k+cost,pressure+int(grade==0),tail)
   best=max(best,value)
 return best

def hands():
 for h in combinations_with_replacement(range(1,7),4):
  mult=factorial(4)
  for d in set(h):mult//=factorial(h.count(d))
  yield h,F(mult,6**4)

def main():
 P=runpy.run_path(str(HERE/'压力模型.py'));rows=[]
 for left,k,s in ((2,3,4),(4,3,4),(4,3,3),(8,3,3),(6,2,4),(4,2,4),(6,2,3)):
  m=P['Model']();avg=F(0);err=0.
  for h,w in hands():
   x=exact(left,k,s,h);v=m.value(12-left,0,2 if 12-left>=6 else 0,s,0,k,0,1,h,-1)
   err=max(err,abs(float(x)-sum(v[:3])));avg+=w*x
  assert err<1e-12,(left,k,s,err)
  rows.append(dict(remaining=left,injury=k,pressure=s,exact_probability=str(avg),probability=float(avg),initial_hands=126,max_error=err))
 by={(r['remaining'],r['injury'],r['pressure']):r['probability'] for r in rows}
 # 已伤非头部，技能恒为1；末回合修复可省略，其骰可保留，压力仅结束的减量无成功价值。
 # 即时推进坏档的压力仍被逐步检查；没有套用R33“修复替推进”的错误耦合。
 m=P['Model']();state=(8,2,2,3,0,2,0,2,(6,),-1)
 adv=m.action_value(*state,0,'快做',6);rep=m.action_value(*state,1,'快做',6)
 counter=list(state);counter[3]=2;counter=tuple(counter)
 ca=m.action_value(*counter,0,'快做',6);cr=m.action_value(*counter,1,'快做',6)
 assert sum(adv[:3])==0
 assert abs(sum(rep[:3])-by[4,3,4])<1e-12
 assert abs(sum(ca[:3])-by[2,3,4])<1e-12
 assert abs(sum(cr[:3])-by[4,3,3])<1e-12
 ordinary=[]
 for s in (3,4):
  st=(6,1,0,s,0,1,0,2,(6,),-1);v=m.value(*st);choice=m.decisions[st]
  if s==3:assert choice[0]==0 and abs(sum(v[:3])-by[4,2,4])<1e-12
  else:assert choice[0] in (1,2) and abs(sum(v[:3])-by[6,2,4])<1e-12
  ordinary.append(dict(pressure=s,state=st,choice=choice,completion=sum(v[:3])))
 assert abs(by[8,3,3]-(7/12)**4)<1e-12
 out=dict(date='2026-10-03',results=rows,checked_future_initial_hands=7*126,max_error=max(r['max_error'] for r in rows),selected_actual_flip=dict(state=state,advance=adv,repair=rep,counter_advance=ca,counter_repair=cr),ordinary_pressure_pair=ordinary,scope='7个已伤非头部许可后续状态×126新四骰，独立Fraction主收益；不是全部三回合初始独立优化，也不是正常访问质量或正式校准')
 (HERE/'未来一轮独立核对.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps({k:v for k,v in out.items() if k not in ('results','selected_actual_flip','ordinary_pressure_pair')},ensure_ascii=False))
if __name__=='__main__':main()
