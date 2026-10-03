"""隔离R43源码/规则，逐项核对重算输入指纹与数学语义。"""
from pathlib import Path
import hashlib,json,tarfile,tempfile,subprocess,sys,shutil
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
            for m in tar.getmembers():assert (isolation/m.name).resolve().is_relative_to(isolation.resolve())
            tar.extractall(isolation,filter='data')
        for rel,digest in version['source_sha256'].items():assert sha(isolation/rel)==digest,rel
        task=isolation/HERE.relative_to(ROOT);checks=[];input_checks=0
        for script,result in [('隐藏反馈模型.py','数学结果.json'),('独立固定真值复核.py','独立复核结果.json'),
                              ('后验响应前向.py','后验响应前向结果.json'),('整合核对.py','整合核对.json')]:
            with (task/(script+'.log')).open('w') as log:
                p=subprocess.run([sys.executable,str(task/script)],cwd=isolation,stdout=log,stderr=subprocess.STDOUT,check=True)
            if script=='隐藏反馈模型.py':
                # 登记事实来自刚返回的子进程；不是从旧状态文件推断运行完成。
                reg=json.loads((task/'执行登记.json').read_text());reg.update(status='隔离执行完成',exit_code=p.returncode,live_session_id=None)
                (task/'执行登记.json').write_text(json.dumps(reg,ensure_ascii=False,indent=2)+'\n')
                assert json.loads((task/'公开策略.json').read_text())==json.loads((HERE/'公开策略.json').read_text())
            actual=json.loads((task/result).read_text());expected=json.loads((HERE/result).read_text())
            for name,digest in actual.get('input_sha256',{}).items():assert sha(task/name)==digest,name;input_checks+=1
            assert clean(actual)==clean(expected),result
            checks.append(dict(script=script,result=result,semantic_equal=True))
        out=dict(batch='R43',source_hashes_checked=len(version['source_sha256']),results=checks,public_policy_equal=True,
                 regenerated_input_hashes_checked=input_checks,passed=True,
                 scope='忽略耗时及其引起的数学JSON字节指纹变化；实际重算输入指纹逐项核对，公开策略完全一致；独立优化范围仍仅局部，正式0真人0')
        (HERE/'隔离复核.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
    finally:shutil.rmtree(isolation)
if __name__=='__main__':main()
