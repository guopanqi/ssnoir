"""已逐步试玩后的对照探针：固定策略复核分支，不用于替代玩家评价。"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]


def run(scene, policy, seed):
    p = subprocess.Popen(['dotnet', 'run', '--no-build', '--project', 'tools/content-validator/SSNoir.ContentValidator.csproj', '--', '--session', 'encounters/实验·'+scene, '--seed', str(seed)], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    def read():
        r = json.loads(p.stdout.readline())
        if r['type'] == 'error': raise RuntimeError(r['message'])
        return r
    def send(c):
        p.stdin.write(json.dumps(c, ensure_ascii=False)+'\n'); p.stdin.flush()
        return read()
    o = read()['observation']
    for _ in range(45):
        if o['EncounterResult'] is not None: break
        ops = o['Operations']
        clocks = {c['Label']:c['Current'] for c in o['Clocks']}
        def card(name, high=False):
            possible = [x for x in ops if x['Card']==name]
            return (max if high else min)(possible, key=lambda x:x['DieValue'] or 0) if possible else None
        choice = None
        if scene == '逼他出门':
            if policy=='过度施压': choice=card('敲侧门')
            elif policy=='不理他': pass
            elif policy=='撤离': choice=card('离开典当铺')
            else:
                if card('堵住后门'): choice=card('堵住后门', True)
                elif clocks['压力']<2: choice=card('敲侧门')
        else:
            if policy=='投降': choice=card('举起双手')
            elif policy=='不动': pass
            else:
                calm=o['Actors'][0]['Composure']
                if calm<=2 and o['Inventory'].get('香烟',0)>0: choice=card('抽烟')
                if not choice: choice=card('穿过货棚' if policy=='直冲货棚' else '绕到栈桥', True)
        if not choice: choice=next(x for x in ops if x['Kind']=='end-turn')
        o=send({'command':'act','version':o['Version'],'operationId':choice['Id'], 'reason':'固定对照策略：'+policy})['observation']
    else: raise RuntimeError('45步未终止')
    with tempfile.TemporaryDirectory(prefix='noir-probe-') as scratch:
        raw=Path(scratch)/'record.json'
        send({'command':'save','path':str(raw)})
        directory=ROOT/'docs'/'实验'/scene/'对照记录';directory.mkdir(parents=True,exist_ok=True)
        with gzip.open(directory/f'{policy}-{seed}.json.gz','wb') as f: f.write(raw.read_bytes())
    send({'command':'quit'});p.wait()
    result={'scene':scene,'policy':policy,'seed':seed,'result':o['EncounterResult'],'injury':o['Injury'],'calm':o['Actors'][0]['Composure']}
    print(json.dumps(result,ensure_ascii=False),flush=True)
    return result

if __name__=='__main__':
    results=[]
    for scene,policies in [('逼他出门',['稳压2','过度施压','不理他','撤离']),('枪口认路',['直冲货棚','只走栈桥','不动','投降'])]:
        for policy in policies:
            for seed in (7,23): results.append(run(scene,policy,seed))
    (ROOT/'docs'/'实验'/'首批对照结果.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
