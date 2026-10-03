"""R45隔离恢复；源指纹、实际输入指纹及语义结果分别检查。"""
from pathlib import Path
import json,hashlib,tarfile,tempfile,subprocess,sys,shutil
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def clean(x):
    if isinstance(x,dict):return {k:clean(v) for k,v in x.items() if k not in ('elapsed_seconds','input_sha256')}
    if isinstance(x,list):return [clean(v) for v in x]
    return x
def main():
    version=json.loads((HERE/'版本.json').read_text());assert sha(HERE/'运行快照.tar.gz')==version['snapshot']['sha256']
    isolation=Path(tempfile.mkdtemp(prefix='_隔离复算-',dir=HERE))
    try:
        with tarfile.open(HERE/'运行快照.tar.gz') as tar:
            for member in tar.getmembers():assert (isolation/member.name).resolve().is_relative_to(isolation.resolve())
            tar.extractall(isolation,filter='data')
        for rel,digest in version['source_sha256'].items():assert sha(isolation/rel)==digest,rel
        task=isolation/HERE.relative_to(ROOT);checks=[];input_checks=0
        for script,result in [('专注模型.py','数学结果.json'),('审查两骰枚举.py','审查两骰结果.json'),('承诺前向.py','承诺前向结果.json'),('整合核对.py','整合核对.json')]:
            with (task/(script+'.log')).open('w') as log:
                subprocess.run([sys.executable,str(task/script)],cwd=isolation,stdout=log,stderr=subprocess.STDOUT,check=True)
            actual=json.loads((task/result).read_text());expected=json.loads((HERE/result).read_text())
            for name,digest in actual.get('input_sha256',{}).items():assert sha(task/name)==digest,name;input_checks+=1
            assert clean(actual)==clean(expected),result
            checks.append(dict(script=script,result=result,semantic_equal=True))
        out=dict(batch='R45',source_hashes_checked=len(version['source_sha256']),results=checks,
                 regenerated_input_hashes_checked=input_checks,passed=True,scope='数学隔离重算，独立优化仅局部；正式0真人0')
        (HERE/'隔离复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
    finally:shutil.rmtree(isolation)
if __name__=='__main__':main()
