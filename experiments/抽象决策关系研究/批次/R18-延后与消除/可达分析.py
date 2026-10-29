from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'期限模型.py'))
m=K['Model']();summary=m.summary();mass={(*m.targets,5,2,0,h):w for h,w in K['HANDS']};finished=failed=success_keep=0.;suppression=[]
for t in (2,1):
 for n in range(4,-1,-1):
  for st,w in list(mass.items()):
   a,b,c,tt,q,h=st
   if tt!=t or len(h)!=n or not w:continue
   if a==b==0:finished+=w;success_keep+=w*c;continue
   m.value(*st);route,act,d=m.decisions[st]
   if route=='结束回合':
    if c<1 or t==1 or (t==2 and a>0 and not q):failed+=w;continue
    for hh,hw in K['HANDS']:
     nxt=(a,b,c-1,t-1,q,hh);mass[nxt]=mass.get(nxt,0.)+w*hw
    continue
   rest=list(h);rest.remove(d);rest=tuple(rest)
   if route=='压制':suppression.append(dict(state=st,mass=w,die=d,direct_possible_by_capacity=a<=2*len(h)))
   for prob,(gain,cost) in zip(K['ODDS'](d,m.g),K['PAY'][act]):
    if not prob:continue
    if c<cost:failed+=w*prob;continue
    aa,bb,qq=(max(0,a-gain),b,q) if route=='甲' else (a,max(0,b-gain),q) if route=='乙' else (a,b,int(q or gain>0))
    nxt=(aa,bb,c-cost,t,qq,rest);mass[nxt]=mass.get(nxt,0.)+w*prob
assert abs(finished+failed-1)<1e-10
assert abs(finished-summary['no_injury_completion'])<1e-10
assert abs(success_keep-finished*summary['cold_on_success'])<1e-10
out=dict(summary=summary,terminal_mass=finished+failed,suppression_action_expected_count=sum(r['mass'] for r in suppression),suppression_with_more_than_one_die_expected_count=sum(r['mass'] for r in suppression if len(r['state'][-1])>1),states=sorted(suppression,key=lambda r:-r['mass']),scope='正常起局最优模型路径；期望动作数不是至少一次概率，首次伤后质量归无伤目标失败')
(HERE/'可达结果.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k not in ('states','summary')},ensure_ascii=False))
