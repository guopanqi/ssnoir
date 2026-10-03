"""恢复本批冻结正式构建/Content，隔离核对全部轨迹并回放代表分支。"""
from pathlib import Path
import json,hashlib,tarfile,tempfile,shutil,runpy
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 v=json.loads((HERE/'版本.json').read_text());archive=HERE/'运行快照.tar.gz';assert digest(archive)==v['snapshot']['sha256']
 isolation=Path(tempfile.mkdtemp(prefix='_隔离正式-',dir=HERE))
 try:
  with tarfile.open(archive) as tar:
   for member in tar.getmembers():assert (isolation/member.name).resolve().is_relative_to(isolation.resolve())
   tar.extractall(isolation,filter='data')
  for rel,sha in v['source_sha256'].items():assert digest(isolation/rel)==sha,rel
  task=isolation/HERE.relative_to(ROOT);h=runpy.run_path(str(task/'正式核对.py'))
  rows=json.loads((task/'正式对照/结果.json').read_text())['results'];checks=[h['check'](task/'正式对照'/row['path'],row['instant']) for row in rows]
  for expected,actual in zip(rows,checks):
   for key in ('path','revision','steps','result','grades','counts'):assert expected[key]==actual[key],(key,expected['path'])
  names=('early-1-8108.json.gz','early-0-8111.json.gz','覆盖-8131.json.gz','覆盖-8136.json.gz')
  replays=[h['replay'](task/'正式对照'/name) for name in names]
  out=dict(source_hashes_checked=len(v['source_sha256']),isolated_calibration_records=len(checks),isolated_calibration_steps=sum(row['steps'] for row in checks),isolated_replays=replays,passed=True,scope='冻结原生构建和全Content隔离复核；四代表包括对照成功、压力失败、倒下、超时；非新会话或真人证据')
  (HERE/'隔离复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
 finally:shutil.rmtree(isolation)
if __name__=='__main__':main()
