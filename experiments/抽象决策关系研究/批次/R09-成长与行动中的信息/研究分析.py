"""R09信息邻域、可达判断、成长和独立固定真值枚举。不会运行正式会话。"""
from pathlib import Path
from fractions import Fraction
import json,runpy
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'行动信息.py'));Model,Rolling=K['Model'],K['Rolling']
def disclosure_stats(m):
 frontier={(s,False):w for w,s in m.starts()};totals={'strict_stay_events':0.,'strict_switch_events':0.,'long_strict_stay_events':0.,'long_strict_switch_events':0.,'disclosure_events':0.,'at_least_one_strict_choice':0.}
 for step in range(5):
  new={}
  for (s,flag),w in frontier.items():
   if m.terminal(s) or not s[-1]:
    if flag:totals['at_least_one_strict_choice']+=w
    continue
   ch=m.decisions[s];i=ch[0]
   for q,ns in m.transitions(s,*ch):
    f=flag
    if (s[1],s[3])[i]<0 and (ns[1],ns[3])[i]>0:
     totals['disclosure_events']+=w*q
     if not m.terminal(ns) and ns[-1]:
      vs=[max(v[0] for v,k in m.candidates(ns) if k[0]==j) for j in (0,1)]
      gap=vs[i]-vs[1-i]
      if abs(gap)>.01:
       totals['strict_stay_events' if gap>0 else 'strict_switch_events']+=w*q;f=True
       if (ns[1],ns[3])[i]==m.long:totals['long_strict_stay_events' if gap>0 else 'long_strict_switch_events']+=w*q
    key=(ns,f);new[key]=new.get(key,0.)+w*q
  frontier=new
 assert not frontier
 totals['scope']='事件指标是每局期望次数；at_least_one是实际可达至少一次概率；严格差>1个百分点'
 return totals

def fixed_truth_enumeration(m):
 """不用模型的transitions/value计算收益；真值开局固定，决策只读公开状态。"""
 p=Fraction(0);weighted_c=Fraction(0);nodes=0
 def walk(a,x,b,y,c,h,la,lb):
  nonlocal nodes
  nodes+=1
  if a>=la or b>=lb:return Fraction(1),Fraction(c)
  if not h:return Fraction(0),Fraction(0)
  s=(a,x,b,y,c,h);i,act,d=m.decisions[s]
  rest=list(h);rest.remove(d);rest=tuple(rest)
  # 直接按官方准备值六面计数，和真实隐藏路长计算，而非在揭示时再抛硬币。
  probs=K['K']['probabilities'](d,m.g)
  ret=[Fraction(0),Fraction(0)]
  gains=(0,0,1) if act=='稳健' else (0,1,2)
  costs=(1,0,0) if act=='稳健' else (1,1,0)
  for j,w in enumerate(probs):
   count=round(w*6)
   if not count:continue
   aa=min(la,a+gains[j]) if i==0 else a
   bb=min(lb,b+gains[j]) if i==1 else b
   xx=la if aa>=m.reveal else x;yy=lb if bb>=m.reveal else y
   v=walk(aa,xx,bb,yy,c-costs[j],rest,la,lb)
   for k in (0,1):ret[k]+=Fraction(count,6)*v[k]
  return tuple(ret)
 for h,w in K['HANDS']:
  for la in (m.short,m.long):
   for lb in (m.short,m.long):
    v=walk(0,la if m.known else -1,0,lb if m.known else -1,5,h,la,lb)
    weight=Fraction(round(w*1296),1296)*Fraction(1,4)
    p+=weight*v[0];weighted_c+=weight*v[1]
 return dict(completion=float(p),exact_completion=str(p),weighted_composure=float(weighted_c),nodes=nodes)

class FirstDie(Model):
 def __init__(self,low):self.low=low;super().__init__(reveal=1)
 def candidates(self,s):
  cs=super().candidates(s)
  if s[:4]==(0,-1,0,-1) and len(s[-1])==4:
   face=(min if self.low else max)(s[-1]);cs=[x for x in cs if x[1][2]==face]
  return cs

def main():
 neighbors=[]
 for short,long,reveal in [(3,6,1),(3,5,1),(4,6,2)]:
  for cls,policy in [(Model,'求解'),(Model,'固定A'),(Model,'遇长必换'),(Model,'期望剩余最短'),(Rolling,'求解')]:
   m=cls(short=short,long=long,reveal=reveal,policy=policy);r=m.summary()
   if cls is Rolling:r['policy']='两条固定路线滚动比较'
   elif policy=='求解':r['reachable']=disclosure_stats(m)
   neighbors.append(r)
 (HERE/'信息邻域.json').write_text(json.dumps(dict(reason='检查降低信息成本与相邻容量条件；不改变收益、不追加状态',results=neighbors),ensure_ascii=False,indent=2)+'\n')
 rows=[];checks=[]
 for g in range(1,5):
  for cls,policy,known in [(Model,'求解',False),(Model,'固定A',False),(Model,'遇长必换',False),(Model,'期望剩余最短',False),(Rolling,'求解',False),(Model,'求解',True)]:
   m=cls(reveal=1,growth=g,policy=policy,known=known);r=m.summary()
   if cls is Rolling:r['policy']='两条固定路线滚动比较'
   if cls is Model and policy=='求解' and not known:r['reachable']=disclosure_stats(m)
   rows.append(r)
   if g==1:
    independent=fixed_truth_enumeration(m)
    assert abs(independent['completion']-r['completion'])<1e-12,(r,independent)
    assert abs(independent['weighted_composure']-r['completion']*r['composure_on_success'])<1e-12
    checks.append(dict(policy=r['policy'],known=known,independent=independent,passed=True))
    print(r,flush=True)
 opening=[]
 for low in (True,False):
  m=FirstDie(low);r=m.summary();r['policy']='首手只用最低骰；后续完全求解' if low else '首手只用最高骰；后续完全求解';opening.append(r)
 (HERE/'开手约束.json').write_text(json.dumps(dict(results=opening,scope='只限制首颗，后续最优；检验骰序套路'),ensure_ascii=False,indent=2)+'\n')
 m=Model(reveal=1);m.summary();examples=[]
 for hand in [(1,1,2,4),(1,4,5,6),(1,1,1,6),(4,4,6,6)]:
  st=(0,-1,0,-1,5,hand);ch=m.decisions[st]
  for q,ns in m.transitions(st,*ch):
   if (ns[1],ns[3])[ch[0]]!=6 or len(ns[-1])!=3:continue
   vs=[max(v[0] for v,a in m.candidates(ns) if a[0]==i) for i in (0,1)]
   examples.append(dict(hand=hand,initial_action=ch,after=ns,branch_probability=q,first_route_values=vs,next_choice=m.decisions[ns]))
 (HERE/'判断例子.json').write_text(json.dumps(examples,ensure_ascii=False,indent=2)+'\n')
 (HERE/'定稿分析.json').write_text(json.dumps(dict(results=rows,independent_checks=checks,scope='四骰一回合，冷静5，正常未受伤局；隐藏长度独立等概率；完成任一路；平手保留冷静'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
