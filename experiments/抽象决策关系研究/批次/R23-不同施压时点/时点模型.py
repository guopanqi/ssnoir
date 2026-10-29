"""只改乙第2轮施压，检验强固定序及公开到期规则。"""
from pathlib import Path
import runpy,json,time
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R22-先解除威胁再完成/中间解除.py'))
class Model(K['Model']):
 def __init__(self,cost=0.,order=None,rule=None,tie='甲'):
  super().__init__(True,cost,order);self.rule,self.tie=rule,tie
 def due(self,route,t):return route=='甲' or (4-t)%2==0
 def pressure(self,a,b,t):return 1+int(a>2)+int(b>2 and self.due('乙',t))
 def _end(self,a,b,c,k,q,t):
  out=[0.,0.,0.,0.]
  for p,cc,kk,qq in self.spend(c,k,q,self.pressure(a,b,t)):
   if kk>=7 or t<=1:v=self.fail(kk);out=[x+p*y for x,y in zip(out,v)];continue
   for hh,w in K['K']['HANDS'][3 if kk>=4 else 4]:
    v=self.value(a,b,cc,kk,qq,t-1,hh)
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def candidates(self,a,b,c,k,q,t,h):
  if self.rule is None:return super().candidates(a,b,c,k,q,t,h)
  left={'甲':a,'乙':b};due=[r for r,x in left.items() if x>2 and self.due(r,t)]
  if due:route=min(due,key=lambda r:(left[r]-2,r!=self.tie))
  elif self.rule=='先解除' and any(x>2 for x in left.values()):route=min((r for r,x in left.items() if x>2),key=lambda r:(left[r]-2,r!=self.tie))
  else:route=min((r for r,x in left.items() if x),key=lambda r:(left[r],r!=self.tie))
  return [(self.end(a,b,c,k,q,t),('结束回合',None,None))]+[(self.action_value(a,b,c,k,q,t,h,r,act,d),(r,act,d)) for r,act in self.actions(a,b,c) if r in (route,'整顿') for d in sorted(set(h))]
def main():
 rows=[]
 for cost in (0.,1.):
  m=Model(cost);free=m.summary();rows.append(dict(kind='自由',**free));print(rows[-1],flush=True)
  controls=[]
  for i,order in enumerate(K['ORDERS']):
   c=Model(cost,order);r=c.summary();controls.append(c);rows.append(dict(kind=f'固定里程碑{i+1}',**r));print(rows[-1],flush=True)
  v=[0.,0.,0.,0.]
  for h,w in K['K']['HANDS'][4]:
   win=m.choose([(c.value(4,4,3,0,0,3,h),i) for i,c in enumerate(controls)])[0]
   for j in range(4):v[j]+=w*win[j]
  rows.append(dict(kind='看初始手牌选里程碑序',cost=cost,value=v,utility=m.score(v),utility_gap=free['utility']-m.score(v)));print(rows[-1],flush=True)
  for rule in ('先完成','先解除'):
   for tie in ('甲','乙'):
    r=Model(cost,rule=rule,tie=tie).summary();r.update(kind='到期优先-'+rule+'-平手'+tie,utility_gap=free['utility']-r['utility']);rows.append(r);print(r,flush=True)
  assert all(r['utility']<=free['utility']+1e-10 for r in rows if r['cost']==cost)
 (HERE/'时点结果.json').write_text(json.dumps(dict(results=rows,scope='数学筛选，唯一新变化乙仅第2轮施压；完整伤势/fair independent假设沿用R21；尚未独立新关系复核或正式执行'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
