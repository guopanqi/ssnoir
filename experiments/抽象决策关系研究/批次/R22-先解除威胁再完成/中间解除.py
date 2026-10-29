"""固定小邻域，不增加收益档；阶段进度既推进任务也关闭持续威胁。"""
from pathlib import Path
import runpy,json,time
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE.parent/'R21-伤势与目标偏好/伤势模型.py'))
ORDERS=((('甲',2),('甲',0),('乙',2),('乙',0)),(('甲',2),('乙',2),('甲',0),('乙',0)),(('甲',2),('乙',2),('乙',0),('甲',0)),(('乙',2),('乙',0),('甲',2),('甲',0)),(('乙',2),('甲',2),('乙',0),('甲',0)),(('乙',2),('甲',2),('甲',0),('乙',0)))
class Model(K['Model']):
 def __init__(self,partial=True,cost=0.,order=None):super().__init__(False,cost);self.partial,self.order=partial,order
 def _end(self,a,b,c,k,q,t):
  cost=1+int(a>(2 if self.partial else 0))+int(b>(2 if self.partial else 0));out=[0.,0.,0.,0.]
  for p,cc,kk,qq in self.spend(c,k,q,cost):
   if kk>=7 or t<=1:v=self.fail(kk);out=[x+p*y for x,y in zip(out,v)];continue
   for hh,w in K['HANDS'][3 if kk>=4 else 4]:
    v=self.value(a,b,cc,kk,qq,t-1,hh)
    for j in range(4):out[j]+=p*w*v[j]
  return tuple(out)
 def actions(self,a,b,c):
  acts=super().actions(a,b,c)
  if self.order:
   route=next((r for r,left in self.order if (a if r=='甲' else b)>left),None)
   acts=[x for x in acts if x[0] in (route,'整顿')]
  return acts
 def summary(self):
  start=time.perf_counter();v=[0.,0.,0.,0.]
  for h,w in K['HANDS'][4]:
   x=self.value(4,4,3,0,0,3,h)
   for j in range(4):v[j]+=w*x[j]
  return dict(partial=self.partial,cost=self.penalty,order=self.order,value=v,utility=self.score(v),states=self.value.cache_info().currsize,seconds=time.perf_counter()-start)
def main():
 rows=[]
 for cost in (0.,1.):
  for partial in (True,False):
   m=Model(partial,cost);free=m.summary();rows.append(dict(kind='自由',**free));print(rows[-1],flush=True)
   controls=[]
   for i,order in enumerate(ORDERS):
    c=Model(partial,cost,order);r=c.summary();controls.append(c);rows.append(dict(kind=f'固定里程碑{i+1}',**r));print(rows[-1],flush=True)
   v=[0.,0.,0.,0.]
   for h,w in K['HANDS'][4]:
    winner=m.choose([(c.value(4,4,3,0,0,3,h),i) for i,c in enumerate(controls)])[0]
    for j in range(4):v[j]+=w*winner[j]
   rows.append(dict(kind='看初始手牌选里程碑序',partial=partial,cost=cost,value=v,utility=m.score(v),utility_gap=free['utility']-m.score(v)))
   assert rows[-1]['utility_gap']>=-1e-10
   print(rows[-1],flush=True)
 (HERE/'中间解除结果.json').write_text(json.dumps(dict(results=rows,scope='数学筛选；甲乙4/4、三回合、初始冷静3、持续伤害1+未解除目标数；完整伤势，独立骰/首次部位均匀假设；λ为研究偏好，尚无新Content/正式轨迹/独立复核'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
