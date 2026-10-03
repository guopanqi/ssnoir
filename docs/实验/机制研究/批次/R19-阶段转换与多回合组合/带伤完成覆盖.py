"""覆盖实际带伤成功，区别模型事件失败和游戏失败。只用公开操作，不改状态。"""
from pathlib import Path
import runpy,json
b=Path(__file__).resolve().parent;K=runpy.run_path(str(b/'正式对照.py'));H=K['H'];model=K['K']['Model'](policy='不用整顿');model.summary();attempts=0;outcomes={}
metadata=json.loads((b/'带伤完成覆盖.json').read_text()) if (b/'带伤完成覆盖.json').exists() else None
if not metadata:
 for seed in range(6341,6401):
  s=H['Session']('研究·整段留力',seed);attempts+=1
  try:
   for _ in range(18):
    o=s.observation
    if o['EncounterResult'] is not None:break
    if K['injury'](o)==0:
     st=K['state'](o);model.value(*st);op=K['operation'](o,model.decisions[st])
    else:
     a,bb,c,t,h=K['state'](o);ops=[x for x in o['Operations'] if x['Kind']=='action' and x['Card']!='整顿']
     if not ops:op=K['operation'](o,('结束回合',None,None))
     else:
      def expected(op):
       r=a if op['Card'][0]=='甲' else bb
       return sum(p*min(r,gain) for p,(gain,dc) in zip(H['odds'](op['Odds']).values(),K['K']['PAY'][op['Card'][1:]]))
      op=max(ops,key=expected)
    s.act(op,'模型边界覆盖：不使用整顿，首伤后按公开概率争取实际完成；不代表玩家或最优策略。')
   status=K['result'](s.observation)['status'];outcomes[status]=outcomes.get(status,0)+1
   if status=='success' and K['injury'](s.observation)>0:
    path=b/'正式对照'/f'边界-带伤成功-{seed}.json.gz';s.save(path)
    metadata=dict(attempts=attempts,seed=seed,search_outcomes=outcomes,record=path.name,scope='完整覆盖搜索，只保留首个带伤成功，用于证明模型事件与游戏结果不同，不估计概率')
    (b/'带伤完成覆盖.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n');break
  finally:s.close()
 else:raise AssertionError(('未覆盖带伤成功',attempts,outcomes))
path=b/'正式对照'/metadata['record'];row=K['check'](path,True,1);assert row['left_injury_free_model'] and row['result']['status']=='success';row.update(policy='边界-带伤成功覆盖',seed=metadata['seed'])
data=json.loads((b/'正式对照/结果.json').read_text());data['results']=[r for r in data['results'] if r['path']!=path.name]+[row]
v=json.loads((b/'正式对照/验证.json').read_text());assert row['revision']==v['ContentRevision'];v['strict_replays']=[r for r in v['strict_replays'] if r['path']!=path.name]+[K['replay'](path)]
v.update(native_games=len(data['results']),checked_steps=sum(r['steps'] for r in data['results']),injured_success_search_completed_games=metadata['attempts'],interaction_visits=sum(len(r['interaction_visits']) for r in data['results']))
(b/'正式对照/结果.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n');(b/'正式对照/验证.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(games=v['native_games'],steps=v['checked_steps'],strict_replays=len(v['strict_replays']),attempts=metadata['attempts'],injured_success=row['result']),ensure_ascii=False))
