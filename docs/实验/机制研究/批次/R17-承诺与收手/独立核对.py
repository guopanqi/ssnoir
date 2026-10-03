"""不带缓存的完整选择树，与独立显式简单策略路径比较。共用官方概率源。"""
from pathlib import Path
from fractions import Fraction as F
from itertools import combinations_with_replacement
import runpy,json,re,hashlib
HERE=Path(__file__).resolve().parent;ROOT=next(p for p in HERE.parents if (p/'AGENTS.md').exists())
source=ROOT/'Engine/Runtime/Core/FateStrip.cs'
counts={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',source.read_text())}
assert len(counts)==7
main=runpy.run_path(str(HERE/'收手模型.py'));Model=main['Model']
def weights(d,g):
 b=d+g;return tuple(F(n,6) for n in counts['<= 1' if b<=1 else '_' if b>=7 else str(b)])
def tree(s,h,g):
 if not h:return F(s)
 best=F(s)
 for d in set(h):
  rest=list(h);rest.remove(d);rest=tuple(rest);w=weights(d,g)
  v=sum(p*tree(s+gain,rest,g) for p,gain in ((w[1],1),(w[2],2)) if p)
  best=max(best,v)
 return best
def simple(s,h,g):
 if not h or (g==1 and max(h)==1 and s>=3):return F(s)
 d=max(h);rest=list(h);rest.remove(d);rest=tuple(rest);w=weights(d,g)
 return sum(p*simple(s+gain,rest,g) for p,gain in ((w[1],1),(w[2],2)) if p)
rows=[];state_rows=[]
for g in range(1,5):
 m=Model(g)
 for h in combinations_with_replacement(range(1,7),4):
  v=tree(0,h,g);u=simple(0,h,g);assert v==u==m.value(0,h),(g,h,v,u,m.value(0,h))
  rows.append(dict(skill=g,hand=h,exact=str(v),passed=True))
 # 0..4颗剩骰，进度范围由已经用掉的骰数及中+1/好+2决定。
 for n in range(5):
  spent=4-n;progress=[0] if spent==0 else range(spent,2*spent+1)
  for h in combinations_with_replacement(range(1,7),n):
   for s in progress:
    v=m.value(s,h);u=simple(s,h,g)
    assert v==u,(g,s,h,v,u)
    state_rows.append(dict(skill=g,progress=s,hand=h,exact=str(v),passed=True))
(HERE/'独立核对.json').write_text(json.dumps(dict(initial_tree_cases=len(rows),capacity_state_cases=len(state_rows),probability_source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),scope='504项独立无缓存完整决策树，另核对1320项容量许可状态的显式最高骰/收手规则；后者不是另一份独立最优求解；均非原生会话',initial_results=rows,state_results=state_rows),ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(initial_tree_cases=len(rows),capacity_state_cases=len(state_rows),passed=True),ensure_ascii=False))
