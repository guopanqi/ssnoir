"""不改规则；技能0只用于覆盖首次模型外的住院结束路径。"""
from pathlib import Path
import runpy,json
HERE=Path(__file__).resolve().parent;K=runpy.run_path(str(HERE/'正式对照.py'));OUT=HERE/'正式对照'
def main():
 path=OUT/'边界-倒下.json.gz';assert not path.exists()
 for seed in range(2501,2551):
  s=K['H']['Session']('研究·留力推进',seed,0)
  try:
   for _ in range(20):
    o=s.observation
    if o['EncounterResult'] is not None:break
    choices=[x for x in o['Operations'] if x['Card']=='快做']
    op=min(choices,key=lambda x:x['DieValue']) if choices else K['operation'](o,('结束回合',None))
    s.act(op,'边界：低技能持续推进，覆盖伤势达到7后的真实倒下。')
   if K['result'](s.observation)['status']!='collapse':continue
   s.save(path);row=K['check'](path,0);row['growth']=0;row['seed']=seed;replay=K['replay'](path)
   v=json.loads((OUT/'验证.json').read_text());assert row['revision']==v['ContentRevision']
   v['native_games']+=1;v['checked_steps']+=row['steps'];v['strict_replays'].append(replay)
   (OUT/'验证.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n')
   r=json.loads((OUT/'结果.json').read_text());r['results'].append(row);(OUT/'结果.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n')
   print(json.dumps(row,ensure_ascii=False,indent=2));break
  finally:s.close()
 else:raise AssertionError('没有覆盖倒下边界')
if __name__=='__main__':main()
