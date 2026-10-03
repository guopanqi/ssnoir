"""前向可达与独立自底向上递推；不调用主模型的候选/转移来生成复核值。"""
from pathlib import Path
from itertools import combinations_with_replacement
from math import factorial
import json,runpy,re,time
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'组合模型.py'));ROOT=next(p for p in HERE.parents if (p/'AGENTS.md').exists())
def reach(m):
 m.summary();mass={(5,3,m.cold,3,h):w for h,w in K['HANDS']};success=fail=keep=0.;heals=[];early=[]
 for t in (3,2,1):
  for n in range(4,-1,-1):
   for st,w in list(mass.items()):
    a,b,c,tt,h=st
    if tt!=t or len(h)!=n or not w:continue
    if a==b==0:success+=w;keep+=w*c;continue
    m.value(*st);route,act,d=m.decisions[st]
    if route=='结束回合':
     if h:early.append(dict(state=st,mass=w))
     if c<1 or t==1:fail+=w;continue
     aa=5 if a and m.reset else a;bb=3 if b and m.reset else b
     for hh,ww in K['HANDS']:
      nxt=(aa,bb,c-1,t-1,hh);mass[nxt]=mass.get(nxt,0.)+w*ww
     continue
    if route=='整顿':heals.append(dict(state=st,mass=w,die=d))
    rest=list(h);rest.remove(d);rest=tuple(rest)
    for prob,(gain,dc) in zip(K['ODDS'](d,m.g),K['PAY'][act]):
     if not prob:continue
     if c+dc<0:fail+=w*prob;continue
     aa,bb=(max(0,a-gain),b) if route=='甲' else (a,max(0,b-gain)) if route=='乙' else (a,b)
     nxt=(aa,bb,min(5,c+dc),t,rest);mass[nxt]=mass.get(nxt,0.)+w*prob
 assert abs(success+fail-1)<1e-9
 expect=sum(w*m.value(5,3,m.cold,3,h)[0] for h,w in K['HANDS'])
 assert abs(expect-success)<1e-9
 return mass,dict(success=success,failed_no_injury_event=fail,success_weighted_cold=keep,healing_action_expected_count=sum(x['mass'] for x in heals),early_end_expected_count=sum(x['mass'] for x in early))
def interaction():
 primary=K['Model']();control=K['Model'](False);pm,ps=reach(primary);cm,cs=reach(control);rows=[]
 for st,w in pm.items():
  a,b,c,t,h=st
  if not h or not a+b or w<1e-6:continue
  primary.value(*st);control.value(*st);p=primary.decisions[st];q=control.decisions[st]
  if p==q:continue
  vp=primary.value(*st)[0];vq=control.value(*st)[0]
  p_use_q=next(v[0] for v,ch in primary.candidates(*st) if ch==q)
  q_use_p=next(v[0] for v,ch in control.candidates(*st) if ch==p)
  if vp-p_use_q>.01 and vq-q_use_p>.01:
   rows.append(dict(state=st,primary_choice=p,control_choice=q,primary_mass=w,control_mass=cm.get(st,0.),primary_loss_using_control_choice=vp-p_use_q,control_loss_using_primary_choice=vq-q_use_p))
 rows.sort(key=lambda r:(r['control_mass']>0,r['primary_mass']+r['control_mass']),reverse=True)
 assert rows,'组合裁决要求存在可达严格反转'
 (HERE/'交互结果.json').write_text(json.dumps(dict(primary=ps,control=cs,witnesses=rows,scope='同状态只改变回合末清零；严格主目标损失>1个百分点，分别报告两条件访问质量；期望动作数不是每局至少一次概率'),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(interaction_witnesses=len(rows),first=rows[0]),ensure_ascii=False),flush=True)
def independent(reset,g):
 text=(ROOT/'Engine/Runtime/Core/FateStrip.cs').read_text();table={k:(int(b),int(m),6-int(b)-int(m)) for k,b,m in re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',text)}
 probabilities={d:tuple(x/6 for x in table['_' if d+g>=7 else str(d+g)]) for d in range(1,7)}
 hands={n:list(combinations_with_replacement(range(1,7),n)) for n in range(5)}
 weights=[]
 for h in hands[4]:
  count=factorial(4)
  for d in set(h):count//=factorial(h.count(d))
  weights.append((h,count/1296))
 transitions={h:[(d,tuple(list(h[:i])+list(h[i+1:]))) for i,d in enumerate(h) if i==0 or h[i-1]!=d] for hs in hands.values() for h in hs}
 values={};states=0
 for t in range(1,4):
  ends={}
  for a in range(6):
   for b in range(4):
    for c in range(6):
     if t==1 or c==0:ends[(a,b,c)]=(0.,0.);continue
     aa=5 if reset and a else a;bb=3 if reset and b else b
     ends[(a,b,c)]=tuple(sum(w*values[(aa,bb,c-1,t-1,h)][j] for h,w in weights) for j in (0,1))
  for n in range(5):
   for h in hands[n]:
    for a in range(6):
     for b in range(4):
      for c in range(6):
       st=(a,b,c,t,h);states+=1
       if a==b==0:values[st]=(1.,float(c));continue
       candidates=[ends[(a,b,c)]]
       if (a+1)//2+(b+1)//2<=n+4*(t-1):
        routes=([0] if a else [])+([1] if b else [])+([2] if c<5 else [])
        for route in routes:
         for fast in ((False,True) if route<2 else (False,)):
          for d,rest in transitions[h]:
           p=keep=0.
           for grade,w in enumerate(probabilities[d]):
            if not w:continue
            dc=2 if route==2 and grade==2 else -1 if grade==0 or (route<2 and fast and grade==1) else 0
            if c+dc<0:continue
            gain=(2 if fast else 1) if grade==2 and route<2 else 1 if fast and grade==1 and route<2 else 0
            aa=max(0,a-gain) if route==0 else a;bb=max(0,b-gain) if route==1 else b
            v=values[(aa,bb,min(5,c+dc),t,rest)];p+=w*v[0];keep+=w*v[1]
           v=(p,keep)
           candidates.append(v)
       top=max(v[0] for v in candidates)
       values[st]=max((v for v in candidates if top-v[0]<=1e-12),key=lambda v:v[1])
 return values,states
def main():
 interaction();checks=[];state_count=0;start=time.perf_counter()
 for reset,g,colds in [(True,1,(3,5)),(False,1,(3,)),(True,2,(3,))]:
  reference,count=independent(reset,g);state_count+=count
  for cold in colds:
   m=K['Model'](reset,g=g,cold=cold)
   for h,w in K['HANDS']:
    st=(5,3,cold,3,h);actual=m.value(*st);want=reference[st]
    assert max(abs(x-y) for x,y in zip(actual,want))<1e-10,(st,reset,g,actual,want)
    checks.append(dict(reset=reset,skill=g,cold=cold,hand=h,passed=True))
 (HERE/'独立核对.json').write_text(json.dumps(dict(initial_cases=len(checks),iterative_states=state_count,tolerance=1e-10,results=checks,scope='独立自底向上全三轮递推，共用正式概率输入；不是Fraction精确证明、正式会话或真人测试'),ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(dict(initial_cases=len(checks),iterative_states=state_count,seconds=time.perf_counter()-start),ensure_ascii=False))
if __name__=='__main__':main()
