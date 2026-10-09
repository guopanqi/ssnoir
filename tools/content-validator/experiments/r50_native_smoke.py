#!/usr/bin/env python3
"""R50 C#正式会话：动态修正可见、甲乙行动执行、终局和严格回放。"""
from __future__ import annotations
import json
import subprocess
import tempfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
CMD=['dotnet','run','--no-build','--project','tools/content-validator/SSNoir.ContentValidator.csproj','--']
ENTRY='encounters/研究·动作回声'


def one(seed:int, growth:int, pattern:str, output:Path)->dict:
    p=subprocess.Popen(CMD+['--session',ENTRY,'--seed',str(seed),'--growth',str(growth)],
      cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
    def read():
        line=p.stdout.readline()
        if not line:
            raise RuntimeError(f'process exited code={p.poll()} seed={seed}: {p.stderr.read()}')
        obj=json.loads(line)
        if obj.get('type')=='error':
            raise AssertionError(obj)
        return obj
    def cmd(value):
        p.stdin.write(json.dumps(value,ensure_ascii=False)+'\n')
        p.stdin.flush()
        return read()
    try:
        obs=read()['observation']
        def options(obs,label):
            return [o for o in obs['Operations'] if o['Card']==label]
        a=options(obs,'推进甲')
        b=options(obs,'推进乙')
        assert a and b
        assert a[0]['Prepared']==b[0]['Prepared'],'开局不得有回声'
        steps=0
        verified_mod=False
        while obs['EncounterResult'] is None and steps<9:
            if steps == 0:
                opts=options(obs,'推进甲')
                die=min(opts,key=lambda o:o['DieValue'])
            else:
                goal = ('推进乙' if (pattern == 'switch' and steps % 2 == 1)
                         else '推进甲' if pattern == 'switch' else '推进乙')
                opts=options(obs,goal)
                if not opts:
                    opts=[o for o in obs['Operations'] if o['Kind']=='end-turn']
                die=max(opts,key=lambda o:o['DieValue'] or 0)
            resp=cmd({'command':'act','version':obs['Version'],'operationId':die['Id'],
                      'reason':'R50原生动作回声核对'})
            obs=resp['observation']
            steps+=1
            a=options(obs,'推进甲')
            b=options(obs,'推进乙')
            if a and b:
                pair=next(((x,y) for x in a for y in b if x['DieValue']==y['DieValue']),None)
                if pair is not None:
                    assert abs(pair[0]['Prepared']-pair[1]['Prepared'])==2, pair
                    verified_mod=True
        assert verified_mod, '未读取到真实动态准备值'
        assert obs['EncounterResult'] is not None, '必须终局'
        path=output/f'{seed}-{growth}-{pattern}.json'
        saved=cmd({'command':'save','path':str(path)})
        assert saved['type']=='saved'
        assert cmd({'command':'quit'})['type']=='bye'
        p.wait(timeout=10)
        assert p.returncode==0
        check=subprocess.run(CMD+['--replay',str(path)],cwd=ROOT,capture_output=True,text=True,timeout=90)
        assert check.returncode==0,check.stdout+check.stderr
        return {'seed':seed,'growth':growth,'pattern':pattern,'result':obs['EncounterResult'],
                'steps':steps,'dynamic_odds_verified':verified_mod,'replay':'pass'}
    finally:
        if p.poll() is None:
            p.kill()
            p.wait(timeout=10)


def main():
    with tempfile.TemporaryDirectory(prefix='r50-') as d:
        folder=Path(d)
        rows=[one(seed,growth,pattern,folder) for growth in (1,2)
              for seed in (7,19,41) for pattern in ('switch','focus')]
    assert len(rows)==12
    print(json.dumps({'official_sessions':len(rows),'cases':rows},ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
