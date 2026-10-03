from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'数学筛选.py'))
class AtStart(K['Window']):
 def candidates(self,a,b,c,t,h):
  out=[]
  for value,choice in super().candidates(a,b,c,t,h):
   if choice[0]=='结束回合':out.append((value,choice));continue
   act,d=choice
   opens=a>0 and any(w and gain>=a for w,(gain,cost) in zip(K['ODDS'](d,self.g),K['PAY'][act]))
   if not opens or len(h)==4:out.append((value,choice))
  return out
if __name__=='__main__':
 rows=[]
 for g in (1,2,3,4):
  m=AtStart(growth=g);row=m.summary();row['policy']='只在新回合第一手开启，其他选择仍求解';rows.append(row)
 (HERE/'开窗强参照.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
