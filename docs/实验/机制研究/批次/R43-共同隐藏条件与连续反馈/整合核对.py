"""协调者核对正式概率表、输入指纹、全初始族与前向终端。"""
from pathlib import Path
from fractions import Fraction as F
import runpy,re,json,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    m=runpy.run_path(str(HERE/'隐藏反馈模型.py'));i=runpy.run_path(str(HERE/'独立固定真值复核.py'))
    source=(ROOT/'Engine/Runtime/Core/FateStrip.cs').read_text()
    entries=re.findall(r'(<= 1|[2-6]|_) => \((\d), (\d)\)',source);assert len(entries)==7
    rows={k:(int(b),int(n),6-int(b)-int(n)) for k,b,n in entries};count=0
    for prepared in range(1,9):
        key='<= 1' if prepared<=1 else '_' if prepared>=7 else str(prepared)
        assert m['COUNTS'][prepared]==rows[key];count+=3
    for d in range(1,7):
        for method in (0,1):
            for truth in (0,1):
                faces=i['faces'](d,method,truth);assert tuple(faces.count(g) for g in range(3))==m['row'](d,method,truth)
                count+=3
    primary=json.loads((HERE/'数学结果.json').read_text());reg=json.loads((HERE/'执行登记.json').read_text())
    for rel,digest in primary['source_sha256'].items():assert sha(ROOT/rel)==digest,rel
    assert reg['live_session_id'] is None and reg['exit_code']==0
    for filename in ('独立复核结果.json','后验响应前向结果.json'):
        out=json.loads((HERE/filename).read_text())
        for name,digest in out['input_sha256'].items():assert sha(HERE/name)==digest,name
    assert len(primary['roots'])==126 and primary['unique_initial_orders']==1296
    for row in primary['roots']:
        v={k:F(x) for k,x in row['values'].items()}
        assert v['free']==v['greedy'] and v['free']>=v['best_initial_order']>=v['highest'] and v['free']>=v['fixed_method']
    forward=json.loads((HERE/'后验响应前向结果.json').read_text());mass=sum((F(t['mass']) for t in forward['aggregate_terminal']),F(0));assert mass==1
    assert F(forward['aggregate']['success'])==F(primary['totals']['free']['fraction'])
    out=dict(batch='R43',passed=True,official_and_independent_probability_components=count,source_and_input_hashes_checked=True,
             initial_hands=126,greedy_free_each_root_equal=True,terminal_mass=str(mass),scope='协调者静态与共享输出整合检查，独立优化/策略执行范围另看独立复核；正式0真人0')
    (HERE/'整合核对.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n');print(json.dumps(out,ensure_ascii=False))
if __name__=='__main__':main()
