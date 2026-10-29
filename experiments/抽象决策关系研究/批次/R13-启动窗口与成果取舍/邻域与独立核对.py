"""少量明确邻域与独立Fraction递推；不调正式参数。"""
from pathlib import Path
from functools import lru_cache
from fractions import Fraction as F
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'数学筛选.py'));A=runpy.run_path(str(HERE/'开窗强参照.py'))
# 不调用主模型的转移函数；独立写出的进度/冷静递推，概率计数来自共同正式表。
TABLE=K['K']['T']
def odds(d,g):
 b=d+g;return tuple(F(x,6) for x in TABLE['<= 1' if b<=1 else '_' if b>=7 else str(b)])
OUTCOMES=(((0,1),(0,0),(1,0)),((0,1),(1,1),(2,0)))
@lru_cache(None)
def harvest_exact(a,b,c,h,g):
 earned=(1 if a==0 else 0)+(2 if b==0 else 0)
 values=[(F(earned),F(earned>0),F(c-1) if earned else F(0))]
 for route,remain in enumerate((a,b)):
  if remain==0:continue
  for d in set(h):
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for outcome in OUTCOMES:
    v=[F(0),F(0),F(0)]
    for w,(advance,harm) in zip(odds(d,g),outcome):
     if not w:continue
     assert c>=harm
     aa=max(0,a-advance) if route==0 else a;bb=max(0,b-advance) if route==1 else b
     tail=harvest_exact(aa,bb,c-harm,rest,g)
     for i in range(3):v[i]+=w*tail[i]
    values.append(tuple(v))
 return max(values)
@lru_cache(None)
def window_exact(p,q,c,h,g):
 if q==0:return F(1),F(c)
 values=[(F(0),F(0))]
 for d in set(h):
  rest=list(h);rest.remove(d);rest=tuple(rest)
  for outcome in OUTCOMES:
   v=[F(0),F(0)]
   for w,(advance,harm) in zip(odds(d,g),outcome):
    if not w or harm>c:continue
    pp=max(0,p-advance) if p else 0;qq=q if p else max(0,q-advance)
    tail=window_exact(pp,qq,c-harm,rest,g)
    for i in range(2):v[i]+=w*tail[i]
   values.append(tuple(v))
 return max(values)
def main():
 checked=[]
 for g in (1,2):
  m=K['Harvest'](growth=g)
  for h,w in K['HANDS']:
   exact=harvest_exact(3,6,5,h,g);actual=m.value(3,6,5,h,-1)
   assert all(abs(float(x)-y)<1e-12 for x,y in zip(exact,actual))
   checked.append(dict(kind='harvest',growth=g,hand=h,expected_score=str(exact[0]),passed=True))
  m=K['Window'](growth=g)
  for p in (0,1,2,3):
   for q in (2,4):
    for c in (0,1,3):
     for h in ((1,2,5),(2,4,6)):
      exact=window_exact(p,q,c,h,g);actual=m.value(p,q,c,1,h)
      assert all(abs(float(x)-y)<1e-12 for x,y in zip(exact,actual)),(p,q,c,h,g,exact,actual)
      checked.append(dict(kind='window_single_round',growth=g,prep=p,payload=q,cold=c,hand=h,completion=str(exact[0]),passed=True))
 (HERE/'独立核对.json').write_text(json.dumps(dict(cases=len(checked),checks=checked,scope='成果全126初始无序手牌×技能1/2；窗口最后一轮96项状态；独立转移与Fraction，概率输入共用正式表；未独立求解窗口全部多回合'),ensure_ascii=False,indent=2)+'\n')
 rows=[]
 for rewards in ((1,1),(1,2),(1,3)):
  for policy in ('求解','固定小','固定大','开局选定'):
   rows.append(dict(kind='harvest',**K['Harvest'](rewards=rewards,policy=policy).summary()))
 for targets in ((2,7),(4,5)):
  for policy in ('求解','开局选定'):rows.append(dict(kind='harvest',**K['Harvest'](targets=targets,policy=policy).summary()))
 for payload in (3,4,5):
  for cls in (K['Window'],A['AtStart']):
   row=cls(payload=payload).summary()
   if cls is A['AtStart']:row['policy']='只在新回合第一手开启，其他仍求解'
   rows.append(dict(kind='window',**row))
 (HERE/'小邻域.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
 print('独立核对',len(checked),'全部通过')
 for row in rows:print({k:v for k,v in row.items() if k in ('kind','targets','rewards','payload','policy','expected_score','injury_free_completion')})
if __name__=='__main__':main()
