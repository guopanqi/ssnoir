"""完整伤势模型下继续找简单套路；不给自由策略的完成率当复杂性证明。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'伤势模型.py'))
class Control(K['Model']):
 def __init__(self,kind,penalty):super().__init__(True,penalty);self.kind=kind
 def candidates(self,a,b,c,k,q,t,h):
  end=[(self.end(a,b,c,k,q,t),('结束回合',None,None))];acts=self.actions(a,b,c)
  if self.kind=='不整顿':acts=[x for x in acts if x[0]!='整顿']
  if self.kind=='甲先完成' and a:acts=[x for x in acts if x[0]!='乙']
  if self.kind=='乙先完成' and b:acts=[x for x in acts if x[0]!='甲']
  dice=[max(h)] if h and self.kind=='最高骰优先' else sorted(set(h))
  return end+[(self.action_value(a,b,c,k,q,t,h,r,act,d),(r,act,d)) for r,act in acts for d in dice]
def main():
 full=json.loads((HERE/'伤势结果.json').read_text())['results'];rows=[]
 for cost in (0.,1.):
  benchmark=next(x for x in full if x['reset'] and x['injury_event_cost']==cost)
  for kind in ('不整顿','甲先完成','乙先完成','最高骰优先'):
   m=Control(kind,cost);row=m.summary();row['control']=kind;row['utility_gap']=benchmark['research_utility']-row['research_utility']
   assert row['utility_gap']>=-1e-10
   rows.append(row);print(row,flush=True)
 (HERE/'简单参照.json').write_text(json.dumps(dict(results=rows,scope='同一完整伤势模型、仅清零规则，限制目标次序/恢复/用骰；允许控制族内最优稳险与换回合。目标先完成仍可整顿；最高骰优先仍可提前换回合。共享转移，非第二份独立最优核对'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
