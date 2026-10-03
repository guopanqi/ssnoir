"""前向真实公开策略，找首次未定位后的重复探查/直接搜索；不把许可状态当频率。"""
from pathlib import Path
from collections import defaultdict
import json,runpy
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'信息投资.py'))
def main():
 m=K['Model']();summary=m.summary();layers=[defaultdict(float) for _ in range(5)]
 for h,w in K['HANDS']:layers[4][(0,0,None,5,h,0,False)]+=w
 terminal=defaultdict(float);first_response=defaultdict(float);witnesses=defaultdict(list);dice_use=defaultdict(float);probes=0.
 for n in range(4,-1,-1):
  for s,weight in layers[n].items():
   a,b,target,c,h,count,pending=s;st=(a,b,target,c,h);m.value(*st);ch=m.decisions[st];route,act,d=ch
   if pending and count==1:first_response['继续探查' if route=='探查' else '直接搜索' if route in ('甲','乙') else '结束']+=weight
   if target is None and h:
    cs=m.candidates(*st);ps=[x for x in cs if x[1][0]=='探查'];ds=[x for x in cs if x[1][0] in ('甲','乙')]
    if ps and ds:
     pv,pc=m.choose(ps);dv,dc=m.choose(ds);gap=pv[0]-dv[0]
     if min(pv[0],dv[0])>0 and abs(gap)>.01:
      label='探查更好' if gap>0 else '搜索更好'
      witnesses[label].append(dict(state=st,after_first_unlocated_probe=pending and count==1,probe_choice=pc,search_choice=dc,probe_value=pv,search_value=dv,visit_mass=weight))
   if route=='结束回合':terminal['timeout']+=weight;terminal['ever_probe']+=weight*bool(count);continue
   if route=='探查':
    probes+=weight;dice_use['最高骰' if d==max(h) else '最低骰' if d==min(h) else '中间骰']+=weight
   rest=list(h);rest.remove(d);rest=tuple(rest)
   for grade,(p,(gain,delta)) in enumerate(zip(K['ODDS'](d,1),K['PAY'][act])):
    if not p:continue
    aa=min(3,a+gain) if route=='甲' else a;bb=min(3,b+gain) if route=='乙' else b;cc=c+delta
    revealed=route=='探查' and grade==2 or aa==3 or bb==3
    branches=[(1.,target)] if target else [(.5,'甲'),(.5,'乙')] if revealed else [(1.,None)]
    for w,tr in branches:
     mass=weight*p*w;cnt=count+int(route=='探查');pending2=route=='探查' and grade!=2
     if tr=='甲' and aa==3 or tr=='乙' and bb==3:terminal['success']+=mass;terminal['ever_probe']+=mass*bool(cnt)
     else:layers[n-1][(aa,bb,tr,cc,rest,cnt,pending2)]+=mass
 assert abs(terminal['success']-summary['completion'])<1e-10
 assert abs(terminal['success']+terminal['timeout']-1)<1e-10
 for label,ws in witnesses.items():ws.sort(key=lambda x:(x['after_first_unlocated_probe'],x['visit_mass']),reverse=True)
 initial_probe=initial_direct_strict=0.
 for h,w in K['HANDS']:
  cs=m.candidates(0,0,None,5,h);pv=m.choose([x for x in cs if x[1][0]=='探查'])[0][0];dv=m.choose([x for x in cs if x[1][0] in ('甲','乙')])[0][0]
  initial_probe+=w*(m.decisions[(0,0,None,5,h)][0]=='探查');initial_direct_strict+=w*(dv-pv>.01)
 out=dict(initial_probe_probability=initial_probe,initial_direct_strict_probability=initial_direct_strict,summary=summary,terminal=dict(terminal),expected_probe_actions=probes,first_unlocated_probe_response_mass=dict(first_response),expected_probe_dice_use=dict(dice_use),witnesses={label:ws[:12] for label,ws in witnesses.items()},scope='自由策略前向访问；首个未定位探查紧接下一步的事件质量两类不重复；探查次数/骰使用是期望次数；未知甲乙名称不制造猜名策略；全部模型数值非真人频率')
 (HERE/'可达选择.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps({k:v for k,v in out.items() if k!='witnesses'},ensure_ascii=False))
 for label,ws in witnesses.items():
  if ws:print(label,json.dumps(ws[0],ensure_ascii=False))
if __name__=='__main__':main()
