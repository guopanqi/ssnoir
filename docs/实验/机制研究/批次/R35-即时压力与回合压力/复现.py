"""从本批冻结快照隔离重算；只写本任务目录，不运行正式会话。"""
from pathlib import Path
import hashlib,json,sys,tarfile,subprocess,shutil,tempfile
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def clean(x):
 if isinstance(x,dict):return {k:clean(v) for k,v in x.items() if k not in ('seconds','dependencies')}
 if isinstance(x,list):return [clean(v) for v in x]
 return x
def main():
 version=json.loads((HERE/'版本.json').read_text());snapshot=HERE/'运行快照.tar.gz'
 assert digest(snapshot)==version['snapshot']['sha256']
 isolation=Path(tempfile.mkdtemp(prefix='_隔离复算-',dir=HERE))
 try:
  with tarfile.open(snapshot) as tar:
   for member in tar.getmembers():
    target=(isolation/member.name).resolve();assert target.is_relative_to(isolation.resolve())
   tar.extractall(isolation,filter='data')
  for rel,sha in version['source_sha256'].items():assert digest(isolation/rel)==sha,rel
  task=isolation/HERE.relative_to(ROOT);results=[]
  for script,result in [('压力模型.py', '压力结果.json'), ('边界核对.py', '边界核对.json')]:
   with (task/(script+'.log')).open('w') as log:
    subprocess.run([sys.executable,str(task/script)],cwd=isolation,stdout=log,stderr=subprocess.STDOUT,check=True)
   actual=clean(json.loads((task/result).read_text()));expected=clean(json.loads((HERE/result).read_text()));assert actual==expected,result
   results.append(dict(script=script,result=result,semantic_equal=True))
  out=dict(date='2026-10-03',source_hashes_checked=len(version['source_sha256']),results=results,passed=True,scope='冻结源码及正式概率输入隔离数学重算；忽略耗时和隔离绝对依赖路径；没有正式会话或真人证据')
  (HERE/'隔离复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
 finally:shutil.rmtree(isolation)
if __name__=='__main__':main()
