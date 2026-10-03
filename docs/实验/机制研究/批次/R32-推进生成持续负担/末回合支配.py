"""R32末回合局部核对；数学耦合证明之外的共享转移检查，非独立求解。"""
from pathlib import Path
from itertools import product
import runpy,json
HERE=Path(__file__).resolve().parent;P=runpy.run_path(str(HERE/'负担模型.py'))
def main():
 m=P['Model']('completion');count=0;maxrepair=0.;strict=0
 from itertools import combinations_with_replacement
 hands=[h for n in (1,2) for h in combinations_with_replacement(range(1,7),n)]
 for p in range(3,8):
  for a,b in product(range(3),repeat=2):
   if p<6 and b or not a and not b:continue
   for k in range(7):
    for q in ((0,1) if 1<=k<=3 else (0,)):
     for c in range(4):
      for h in hands:
       cs=m.candidates(p,a,b,c,k,q,1,h,-1)
       advance=max(m.score(v) for v,ch in cs if ch[0]==0)
       repair=max(m.score(v) for v,ch in cs if ch[0] in (1,2))
       gap=repair-advance;maxrepair=max(maxrepair,gap);count+=1
       assert gap<=1e-10,(p,a,b,c,k,q,h,advance,repair)
       if gap< -1e-10:strict+=1
 # 头伤恢复反例：当前静态容量4，小于实际正概率可推进5。
 state=(3,2,0,0,3,1,1,(1,1,4),-1)
 skill=0;capacity=sum(max(g for w,(g,dc) in zip(P['ODDS'](d,skill),P['PAY']['快做']) if w>0) for d in state[7])
 assert capacity==4
 # 投4风险中档：+1/−1，3头伤进入4重伤；余下两颗1均好档：各+2，无冷静成本。
 first=P['ODDS'](4,0)[1];nextgood=P['ODDS'](1,1)[2];prob=first*nextgood**2
 assert prob>0 and 1+2+2>capacity
 out=dict(checked_states=count,strict_advance_primary_better=strict,max_repair_primary_advantage=maxrepair,passed=True,scope='末回合1/2骰、p3..7、所有合法负担剩余值、c0..3、完整k0..6及头伤状态；只比较成功主目标。共享R32转移不算独立求解，B末回合收益=成功率所以同样适用',static_capacity_counterexample=dict(state=state,static_capacity=capacity,positive_path_gain=5,path_probability=prob,path=['4风险中档→p4/k4，恢复技能','1风险好档→p6','1风险好档→p8成功'],note='当前有效技能正概率容量不是完整回合上界；恢复技能的非单调性须单独注明'))
 data=json.loads((HERE/'负担结果.json').read_text())
 first=max(0.,sum(P['ODDS'](d,1)[2] for d in range(1,7))/6)**4
 observed=next(r for r in data['results'] if r['objective']=='early' and r['policy']=='自由')['completion_by_round'][0]
 assert abs(first-observed)<1e-12,(first,observed)
 out['first_round_capacity_bound']=dict(bound=first,observed=observed,passed=True,reason='四行动达到8须全部推进风险好档；成功路径无伤，因此公平四骰的最大概率为平均好档概率的四次方=(7/12)^4')
 (HERE/'末回合支配.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
