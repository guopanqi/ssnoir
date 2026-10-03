"""R31两个剩骰的小决策树，独立写出冻结概率/伤势/终结。不加载R30/R21模型。"""
from fractions import Fraction as F
from functools import lru_cache
from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
STRIPS={1:(3,3,0),2:(2,3,1),3:(1,3,2),4:(1,2,3),5:(0,2,4),6:(0,1,5),7:(0,0,6)}
# 当前第3回合，甲1乙2丙0作为主要检验；末回合无后续手牌。
def spend(c,k,head,n):
 if n<=c:return [(F(1),c-n,k,head)]
 severity=k+n-c
 if severity>=7:return [(F(1),0,7,False)]
 if k==0:return [(F(1,4),0,severity,severity<4),(F(3,4),0,severity,False)]
 return [(F(1),0,severity,head and severity<4)]
def terminal_failure(c,k,head,hp):
 damage=sum((1,2,0)[i] for i,x in enumerate(hp) if x)+1
 return (F(0),sum(p*int(nk>0) for p,nc,nk,nh in spend(c,k,head,damage)),F(0),F(0))
def rank(v):return v[0],-v[1],-v[2],v[3]
def action(hp,c,k,head,h,route,risky,d):
 remaining=list(h);remaining.remove(d);remaining=tuple(remaining);total=[F(0)]*4
 effective=0 if 1<=k<=3 and head else 1
 for grade,n in enumerate(STRIPS[min(7,d+effective)]):
  if not n:continue
  gain=grade if risky else int(grade==2);cost=int(grade==0 or risky and grade==1)
  for p,nc,nk,nh in spend(c,k,head,cost):
   if nk>=7:v=(F(0),F(1),F(0),F(0))
   else:
    newhp=list(hp);newhp[route]=max(0,newhp[route]-gain)
    v=solve(tuple(newhp),nc,nk,nh,remaining)
   for j in range(4):total[j]+=F(n,6)*p*v[j]
 return tuple(total)
def candidates(hp,c,k,head,h):
 out=[(terminal_failure(c,k,head,hp),('结束',None,None))]
 for i,left in enumerate(hp):
  if left:
   for risk in (False,True):
    for d in set(h):out.append((action(hp,c,k,head,h,i,risk,d),(i,risk,d)))
 return out
@lru_cache(None)
def solve(hp,c,k,head,h):
 if k>=7:return F(0),F(1),F(0),F(0)
 if not any(hp):return F(1),F(int(k>0)),F(k),F(c)
 return max(candidates(hp,c,k,head,h),key=lambda x:rank(x[0]))[0]
def encode(v):return dict(exact=[str(x) for x in v],floating=[float(x) for x in v])
def main():
 rows=[]
 for name,k,head in (('无伤冷静已空',0,False),('头部轻伤3',3,True),('重伤4技能恢复',4,False)):
  st=((1,2,0),0,k,head,(1,6));cs=candidates(*st);best=max(cs,key=lambda x:rank(x[0]));first={}
  for r in (0,1):
   v,ch=max([x for x in cs if x[1][0]==r],key=lambda x:rank(x[0]));first['甲乙'[r]]=dict(value=encode(v),choice=ch)
  rows.append(dict(name=name,state=st,best=encode(best[0]),choice=best[1],first_target=first))
 assert rows[0]['best']['exact'][0]=='2/3'
 assert rows[0]['first_target']['甲']['value']['exact'][0]=='31/48'
 assert rows[1]['best']['exact'][0]=='1/2'
 assert rows[1]['first_target']['乙']['value']['exact'][0]=='5/12'
 assert rows[2]['best']['exact'][0]=='2/3'
 collapse=((1,0,0),0,6,False,(1,));cs=candidates(*collapse)
 assert solve(*collapse)[0]==F(1,6)
 # 本轮即时压力/最少风险好档行动数 = 甲1/1、乙2/1，故两个公式此时都选乙。
 out=dict(date='2026-10-02',results=rows,collapse_case=dict(state=collapse,best=encode(solve(*collapse)),candidates=[dict(choice=ch,value=encode(v)) for v,ch in cs]),scope='末回合最多两骰、独立Fraction小树；无R30/R21导入；许可状态未称完整正常策略访问；冻结概率/首次部位均匀沿用模型假设')
 (HERE/'末回合独立核查.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
