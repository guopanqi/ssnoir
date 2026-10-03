"""R33最后一回合最多两骰的独立Fraction小树；不导入R32/R21。"""
from fractions import Fraction as F
from functools import lru_cache
from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
STRIPS={1:(3,3,0),2:(2,3,1),3:(1,3,2),4:(1,2,3),5:(0,2,4),6:(0,1,5),7:(0,0,6)}
def skill(k,head):return 0 if 1<=k<=3 and head else 1
def odds(d,k,head):return STRIPS[min(7,d+skill(k,head))]
def spend(c,k,head,cost):
 if cost<=c:return [(F(1),c-cost,k,head)]
 nk=k+cost-c
 if nk>=7:return [(F(1),0,7,False)]
 if k==0:return [(F(1,4),0,nk,nk<4),(F(3,4),0,nk,False)]
 return [(F(1),0,nk,head and nk<4)]
def failure(k):return F(0),F(0),F(0),F(int(k>0)),F(0),F(0)
def end(p,a,b,c,k,head,h):
 total=[F(0)]*6
 for prob,nc,nk,nh in spend(c,k,head,1+int(a>0)+int(b>0)):
  v=failure(nk)
  for i in range(6):total[i]+=prob*v[i]
 return tuple(total)
def rank(v):return v[2],-v[3],-v[4],v[5]
def action(p,a,b,c,k,head,h,route,risky,d):
 tail=list(h);tail.remove(d);tail=tuple(tail);total=[F(0)]*6
 for grade,n in enumerate(odds(d,k,head)):
  if not n:continue
  gain=grade if risky else int(grade==2);cost=int(grade==0 or risky and grade==1)
  for z,nc,nk,nh in spend(c,k,head,cost):
   if nk>=7:v=failure(nk)
   else:
    np,na,nb=p,a,b
    if route==0:
     np=min(8,p+gain)
     if p<3<=np:na=2
     if p<6<=np:nb=2
    elif route==1:na=max(0,a-gain)
    else:nb=max(0,b-gain)
    v=solve(np,na,nb,nc,nk,nh,tail)
   for i in range(6):total[i]+=F(n,6)*z*v[i]
 return tuple(total)
def candidates(p,a,b,c,k,head,h):
 cs=[(end(p,a,b,c,k,head,h),('结束',None,None))]
 for route in [0]+([1] if a else [])+([2] if b else []):
  for risky in (False,True):
   for d in set(h):cs.append((action(p,a,b,c,k,head,h,route,risky,d),(route,risky,d)))
 return cs
@lru_cache(None)
def solve(p,a,b,c,k,head,h):
 if k>=7:return failure(k)
 if p==8:return F(0),F(0),F(1),F(int(k>0)),F(k),F(c)
 assert (p>=3 or a==0) and (p>=6 or b==0)
 return max(candidates(p,a,b,c,k,head,h),key=lambda x:rank(x[0]))[0]
@lru_cache(None)
def supported_capacity(c,k,head,h):
 """只求当前手牌无倒下的正概率推进支持上限；不查胜率、未来手牌或负担。"""
 best=0
 for d in set(h):
  tail=list(h);tail.remove(d);tail=tuple(tail)
  for risky in (False,True):
   for grade,n in enumerate(odds(d,k,head)):
    if not n:continue
    gain=grade if risky else int(grade==2);cost=int(grade==0 or risky and grade==1)
    for prob,nc,nk,nh in spend(c,k,head,cost):
     if nk<7:best=max(best,gain+supported_capacity(nc,nk,nh,tail))
 return best
def current_capacity(k,head,h):return sum(max(i for i,n in enumerate(odds(d,k,head)) if n) for d in h)
def encode(v):return dict(exact=[str(x) for x in v],floating=[float(x) for x in v])
CASES=(
 ('失败路径修复救曾伤',(3,2,0,1,0,False,(6,))),
 ('跨重伤恢复技能提高支持容量',(5,2,0,0,3,True,(1,1))),
 ('冷静充足无法跨档',(5,2,0,5,3,True,(1,1))),
 ('7伤须先终止',(7,2,2,0,6,False,(1,))),
 ('即时完成不收压力',(7,2,2,1,0,False,(6,))),
)
def main():
 rows=[]
 for name,st in CASES:
  p,a,b,c,k,q,h=st;cs=candidates(*st);best=max(cs,key=lambda x:rank(x[0]));first={}
  for route in (0,1,2):
   selected=[x for x in cs if x[1][0]==route]
   if selected:
    v,ch=max(selected,key=lambda x:rank(x[0]));first[('推进','修甲','修乙')[route]]=dict(choice=ch,value=encode(v))
  rows.append(dict(name=name,state=st,best=encode(solve(*st)),choice=best[1],first_route=first,capacity_2n=2*len(h),capacity_current_skill=current_capacity(k,q,h),capacity_positive_support=supported_capacity(c,k,q,h)))
 assert rows[0]['first_route']['推进']['value']['exact'][3]=='1'
 assert rows[0]['first_route']['修甲']['value']['exact'][3]=='0'
 assert rows[1]['best']['exact'][2]=='1/12'
 assert rows[1]['capacity_current_skill']==2 and rows[1]['capacity_positive_support']==3
 assert rows[2]['capacity_current_skill']==2 and rows[2]['capacity_positive_support']==2
 assert rows[3]['best']['exact'][2]=='1/6'
 assert rows[4]['best']['exact']==['0','0','1','0','0','1']
 trigger_rows=[]
 for label,st in (
  ('首次跨3产生甲',(2,0,0,1,0,False,(6,))),
  ('甲已修后继续不重生',(3,0,0,1,0,False,(6,))),
  ('首次跨6产生乙',(5,0,0,1,0,False,(6,))),
  ('乙已修后继续不重生',(6,0,0,1,0,False,(6,))),
 ):
  v=action(*st,0,False,6);trigger_rows.append(dict(name=label,state=st,forced_choice=[0,False,6],value=encode(v)))
 assert [r['value']['exact'][3] for r in trigger_rows]==['1','0','1','0']
 out=dict(date='2026-10-02',results=rows,trigger_checks=trigger_rows,scope='5个末回合最多两骰小状态＋4个强制一步触发检查；独立Fraction效果/伤势/时点；两研究主目标末回合相同；非正常访问统计、非完整三回合独立求解')
 (HERE/'末回合独立核查.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
