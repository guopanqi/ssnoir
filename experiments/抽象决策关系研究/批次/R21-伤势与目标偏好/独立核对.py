"""逐初始手牌核对独立C++迭代与Python递归；不从均值吻合推断每手正确。"""
from pathlib import Path
import runpy,json,time
HERE=Path(__file__).resolve().parent
M=runpy.run_path(str(HERE/'伤势模型.py'))
def main():
 data=json.loads((HERE/'独立迭代结果.json').read_text());rows=[];summaries=[]
 old=json.loads((HERE/'伤势结果.json').read_text())['results']
 for row in data['profiles']:
  start=time.perf_counter();m=M['Model'](row['reset'],row['cost']);error=0.
  for item in row['hands']:
   primary=m.value(5,3,3,0,0,3,tuple(item['hand']))
   error=max(error,max(abs(a-b) for a,b in zip(primary,item['value'])))
  assert error<1e-10,(row['reset'],row['cost'],error)
  summary=m.summary();summaries.append(summary)
  before=next(x for x in old if x['reset']==row['reset'] and x['injury_event_cost']==row['cost'])
  assert abs(before['actual_completion']-summary['actual_completion'])<1e-10 and abs(before['ever_injured']-summary['ever_injured'])<1e-10,'平手修正影响了主指标，需重新裁决'
  expected=[summary['actual_completion'],summary['ever_injured'],summary['injury_on_success']*summary['actual_completion'],summary['cold_on_success']*summary['actual_completion']]
  assert max(abs(a-b) for a,b in zip(expected,row['value']))<1e-10
  result=dict(reset=row['reset'],cost=row['cost'],initial_hands=len(row['hands']),maximum_error=error,passed=True,seconds=time.perf_counter()-start)
  rows.append(result);print(result,flush=True)
 (HERE/'伤势结果.json').write_text(json.dumps(dict(results=summaries,scope='完整有限伤势模型；公平独立骰与首次伤部位均匀是假设；六组756手已独立迭代核对四维值，效用系数不是游戏奖励或玩家估值'),ensure_ascii=False,indent=2)+'\n')
 (HERE/'独立核对.json').write_text(json.dumps(dict(profiles=rows,initial_states=sum(x['initial_hands'] for x in rows),iterative_states_per_profile=907200,scope='六个目标/规则条件的756手初始状态，每手核对完成、曾伤、成功加权伤势与冷静四维值；共用概率校准输入，独立语言/健康索引/迭代转移/状态表；数值有限模型，不是随机分布或体验验证'),ensure_ascii=False,indent=2)+'\n')
if __name__=='__main__':main()
