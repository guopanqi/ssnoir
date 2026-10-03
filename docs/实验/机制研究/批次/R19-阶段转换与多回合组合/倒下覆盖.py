from pathlib import Path
import runpy,json
b=Path(__file__).resolve().parent;root=next(p for p in b.parents if (p/'AGENTS.md').exists());K=runpy.run_path(str(b/'正式对照.py'));H=K['H'];attempts=0;outcomes={}
saved=json.loads((b/'倒下覆盖.json').read_text()) if (b/'倒下覆盖.json').exists() else None
for seed in ([] if saved else range(6301,6351)):
 s=H['Session']('研究·整段留力',seed);attempts+=1
 try:
  for step in range(18):
   o=s.observation
   if o['EncounterResult'] is not None:break
   ops=[x for x in o['Operations'] if x['Kind']=='action' and x['Card'].endswith('快做')]
   if ops:
    a,bb,c,t,h=K['state'](o)
    op=min(ops,key=lambda x:(x['DieValue'],-(a if x['Card'][0]=='甲' else bb)))
   else:op=K['operation'](o,('结束回合',None,None))
   s.act(op,'终止边界覆盖搜索：刻意不用整顿、优先低骰快做；不代表优化策略或玩家体验。')
  result=K['result'](s.observation);outcomes[result['status']]=outcomes.get(result['status'],0)+1
  if result['status']=='collapse':
   path=b/'正式对照'/f'边界-倒下-{seed}.json.gz';s.save(path)
   (b/'倒下覆盖.json').write_text(json.dumps(dict(attempts=attempts,seed=seed,search_outcomes=outcomes,record=path.name,scope='搜索只用于终止覆盖；所有尝试终止计入搜索统计，只保留首个倒下轨迹，不估计概率'),ensure_ascii=False,indent=2)+'\n')
   print(json.dumps(dict(path=path.name,result=result,after_cold=K['player'](s.observation)['Composure'],after_injury=s.observation['Injury'],attempts=attempts),ensure_ascii=False));break
 finally:s.close()
else:
 if not saved:raise AssertionError(('未覆盖倒下',attempts,outcomes))
metadata=json.loads((b/'倒下覆盖.json').read_text());path=b/'正式对照'/metadata['record']
row=K['check'](path,True,1);assert row['result']['status']=='collapse';row['policy']='边界-倒下覆盖';row['seed']=metadata['seed']
recorded=json.loads((b/'正式对照/结果.json').read_text());recorded['results']=[r for r in recorded['results'] if r['path']!=path.name]+[row]
validation=json.loads((b/'正式对照/验证.json').read_text());assert row['revision']==validation['ContentRevision']
validation['strict_replays']=[r for r in validation['strict_replays'] if r['path']!=path.name]+[K['replay'](path)]
validation.update(native_games=len(recorded['results']),checked_steps=sum(r['steps'] for r in recorded['results']),collapse_search_completed_games=metadata['attempts'],scope='native_games为保存并核验的完整轨迹数；覆盖搜索额外起局单列，不估计胜率')
(b/'正式对照/结果.json').write_text(json.dumps(recorded,ensure_ascii=False,indent=2)+'\n');(b/'正式对照/验证.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(games=validation['native_games'],steps=validation['checked_steps'],strict_replays=len(validation['strict_replays']),collapse_result=row['result']),ensure_ascii=False))
