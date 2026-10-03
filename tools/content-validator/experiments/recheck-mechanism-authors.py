"""复跑既有作者决定，保存当前版本原生轨迹。不是一次新的玩家评估。"""
import gzip,json
from pathlib import Path
import runpy
H=runpy.run_path(str(Path(__file__).with_name('mechanism-study.py')))
ROOT=H['ROOT'];source=ROOT/'docs/实验/机制研究/作者逐步';target=ROOT/'docs/实验/机制研究/作者回归'
if target.exists():raise FileExistsError(target)
target.mkdir()
for path in sorted(source.glob('*.json.gz')):
    old=json.loads(gzip.decompress(path.read_bytes()));setup=old['Setup']
    session=H['Session'](setup['Entry'].split('encounters/')[-1],setup['Seed'],setup['Growth'])
    # 作者原局未使用恢复品；模板会话关掉了库存，故只比较骰、冷静与机制进度。
    try:
        for step in old['Steps']:
            previous=step['Before'];current=session.observation
            old_clocks={c['Label']:c['Current'] for c in previous['Clocks'] if c['Label']!='警戒'}
            now_clocks={c['Label']:c['Current'] for c in current['Clocks']}
            assert old_clocks==now_clocks,(path,step['Version'],old_clocks,now_clocks)
            old_actor=next(a for a in previous['Actors'] if a['Id']=='player')
            now_actor=next(a for a in current['Actors'] if a['Id']=='player')
            assert (old_actor['Dice'],old_actor['Composure'])==(now_actor['Dice'],now_actor['Composure'])
            op=next(x for x in previous['Operations'] if x['Id']==step['OperationId'])
            current_op=next(x for x in current['Operations'] if (x['Kind'],x['Card'],x['DieValue'])==(op['Kind'],op['Card'],op['DieValue']))
            session.act(current_op,'作者原决定回归（非新玩家）：'+step['Reason'])
        assert session.observation['EncounterResult']==old['Steps'][-1]['After']['EncounterResult']
        session.save(target/path.name)
        print(path.name,'回归通过，结果',session.observation['EncounterResult'])
    finally:session.close()
