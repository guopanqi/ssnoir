"""检查当前回合完整规划、未来先假设只推进的滚动策略；不窥视未来手牌。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent
K=runpy.run_path(str(HERE/'余力模型.py'));Base=K['Model']
class Oracle(Base):
 def __init__(self,**kwargs):
  super().__init__(**kwargs)
  self.future=Base(**(kwargs|{'policy':'只推进'}))
 def _end(self,r,c,t):
  if c<1 or t<=1:return 0.,0.
  p=keep=0.
  for h,w in K['HANDS']:
   v=self.future.value(r,c-1,t-1,h);p+=w*v[0];keep+=w*v[1]
  return p,keep
class Rolling(Base):
 def __init__(self,**kwargs):
  super().__init__(**kwargs);self.oracle=Oracle(**kwargs)
 def _value(self,r,c,t,h):
  if r==0:return 1.,float(c)
  self.oracle.value(r,c,t,h);act,d=self.oracle.decisions[(r,c,t,h)]
  self.decisions[(r,c,t,h)]=(act,d)
  return self.end(r,c,t) if act=='结束回合' else self.action_value(r,c,t,h,act,d)
class IgnoreCarry(Base):
 def __init__(self,**kwargs):
  super().__init__(**kwargs);self.oracle=Base(**(kwargs|{'reset':True}))
 def _value(self,r,c,t,h):
  if r==0:return 1.,float(c)
  self.oracle.value(r,c,t,h);act,d=self.oracle.decisions[(r,c,t,h)]
  self.decisions[(r,c,t,h)]=(act,d)
  return self.end(r,c,t) if act=='结束回合' else self.action_value(r,c,t,h,act,d)

class FixedOpening(Base):
 def __init__(self,start_threshold):
  self.start_threshold=start_threshold;super().__init__(cold=3,recovery=2)
 def candidates(self,r,c,t,h):
  out=[(self.end(r,c,t),('结束回合',None))]
  finisher=any(all(not w or (c+dc>=0 and gain>=r) for w,(gain,dc) in zip(K['ODDS'](d,self.g),self.pay[a])) for a in ('稳做','快做') for d in set(h))
  if not finisher and len(h)==4 and c<=self.start_threshold:
   return out+[(self.action_value(r,c,t,h,'整顿',max(h)),('整顿',max(h)))]
  return out+[(self.action_value(r,c,t,h,a,d),(a,d)) for a in ('稳做','快做') for d in sorted(set(h))]

class UseAll(Base):
 def candidates(self,r,c,t,h):
  return [x for x in super().candidates(r,c,t,h) if not h or x[1][0]!='结束回合']
class EndCutoff(Base):
 def __init__(self,cut):
  self.cut=cut;super().__init__(cold=3,recovery=2,policy='只推进')
 def candidates(self,r,c,t,h):
  cs=super().candidates(r,c,t,h)
  if h and t>1 and c>0 and max(h)<=self.cut:return [cs[0]]
  return [x for x in cs if not h or x[1][0]!='结束回合']

def main():
 rows=[]
 for cold in (3,5):
  for cls,policy,k in [(Base,'求解',2),(Base,'只推进',2),(Base,'冷静阈值',0),(Base,'冷静阈值',1),(Base,'冷静阈值',2),(Base,'冷静阈值',3),(Base,'冷静阈值',4),(Base,'低骰整顿',2),(Rolling,'求解',2),(IgnoreCarry,'求解',2)]:
   m=cls(cold=cold,recovery=2,policy=policy,threshold=k);r=m.summary()
   if cls is Rolling:r['policy']='当前回合全规划，未来只推进；逐回合滚动'
   if cls is IgnoreCarry:r['policy']='规划时把未来冷静当5；执行仍按真实冷静延续'
   rows.append(r);print(r,flush=True)
 opening=[]
 for threshold in (1,2,3,4):
  m=FixedOpening(threshold);r=m.summary();r['policy']=f'每轮开头冷静≤{threshold}，最高骰整顿一次，之后只推进；有保证终点可直接完成';opening.append(r)
 (HERE/'固定开局.json').write_text(json.dumps(dict(results=opening,scope='只在开头整顿一次；其余稳险与骰序仍优化；纯模型'),ensure_ascii=False,indent=2)+'\n')
 ends=[]
 for policy in ('求解','只推进'):
  m=UseAll(cold=3,recovery=2,policy=policy);r=m.summary();r['policy']='用完骰子才结束回合：'+policy;ends.append(r)
 (HERE/'结束回合约束.json').write_text(json.dumps(dict(results=ends,scope='只限制提前结束；其余行动与骰序优化'),ensure_ascii=False,indent=2)+'\n')
 cuts=[]
 for cut in (1,2,3,4):
  m=EndCutoff(cut);r=m.summary();r['policy']=f'剩骰都≤{cut}且还有下一轮、冷静够时间税就换轮；否则用完';cuts.append(r)
 (HERE/'换手阈值.json').write_text(json.dumps(dict(results=cuts,scope='禁止整顿；阈值外仍优化骰序与稳险，最后一轮不提前超时'),ensure_ascii=False,indent=2)+'\n')
 (HERE/'强参照.json').write_text(json.dumps(dict(results=rows,scope='同一规则同一无伤目标；限制策略共同优化所允许的骰子和行动；滚动策略仅读当前手牌'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
