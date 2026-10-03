"""仅补R19缺少的轻伤头部后续行动。条件先存，完成轨迹与搜索数分别报告。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;OLD=HERE.parent/'R19-阶段转换与多回合组合'
K=runpy.run_path(str(OLD/'正式对照.py'));H=K['H'];OUT=HERE/'正式补覆盖'
def health(o):
 k=K['injury'](o);return k,int(1<=k<=3 and o['Injury'].startswith('头 '))
def main():
 OUT.mkdir(exist_ok=True);attempts=[]
 assert not list(OUT.glob('*.json.gz')),'已存在正式覆盖，先读记录；不重复生成'
 for seed in range(6601,6651):
  s=H['Session']('研究·整段留力',seed);covered=0
  try:
   for step in range(18):
    o=s.observation
    if o['EncounterResult'] is not None:break
    ops=[x for x in o['Operations'] if x['Kind']=='action' and x['Card']!='整顿']
    if ops:
     op=min(ops,key=lambda x:(x['DieValue'],x['Card'].endswith('稳做'),x['Card']))
     covered+=health(o)[1]
    else:op=K['operation'](o,('结束回合',None,None))
    s.act(op,'R21预先冻结补覆盖：只用公开操作；低骰快做，核对轻伤头部后续判定。')
   assert s.observation['EncounterResult'] is not None
   attempts.append(dict(seed=seed,completed=True,head_penalty_actions=covered,result=K['result'](s.observation)))
   if covered:
    p=OUT/f'头伤后续-{seed}.json.gz';s.save(p);row=K['check'](p,True,1);replay=K['replay'](p)
    (OUT/'结果.json').write_text(json.dumps(dict(results=[row],search_attempts=attempts,search_starts=len(attempts),search_completed=len(attempts),strict_replays=[replay],scope='覆盖筛选，仅保存第一条头伤之后实际行动的完整轨迹，不是胜率或概率抽样'),ensure_ascii=False,indent=2)+'\n')
    print(dict(saved=p.name,attempts=len(attempts),head_actions=covered,steps=row['steps'],replayed=True),flush=True);return
  finally:s.close()
 (OUT/'搜索失败.json').write_text(json.dumps(dict(search_attempts=attempts,covered=False),ensure_ascii=False,indent=2)+'\n')
 raise AssertionError('50个预定种子未覆盖头伤后续行动')
if __name__=='__main__':main()
