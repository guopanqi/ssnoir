"""恢复目录中重算独立迭代/严格回放/伤势校准；不生成新游戏。"""
from pathlib import Path
import tempfile,subprocess,json,runpy
HERE=Path(__file__).resolve().parent
ROOT=next(p for p in HERE.parents if (p/'AGENTS.md').exists())
def main():
 with tempfile.TemporaryDirectory(prefix='ssnoir-r21-cpp-') as tmp:
  binary=Path(tmp)/'independent'
  subprocess.run(['clang++','-std=c++17','-O2',str(HERE/'独立迭代.cpp'),'-o',str(binary)],check=True,capture_output=True,text=True)
  out=subprocess.run([str(binary)],input=(HERE/'独立概率输入.txt').read_text(),check=True,capture_output=True,text=True)
 fresh=json.loads(out.stdout);saved=json.loads((HERE/'独立迭代结果.json').read_text())
 errors=[]
 for a,b in zip(fresh['profiles'],saved['profiles']):
  assert (a['reset'],a['cost'])==(b['reset'],b['cost'])
  assert len(a['hands'])==len(b['hands'])==126
  for x,y in zip(a['hands'],b['hands']):
   assert x['hand']==y['hand'];errors.extend(abs(i-j) for i,j in zip(x['value'],y['value']))
 assert len(fresh['profiles'])==len(saved['profiles'])==6 and max(errors)<1e-10
 old=runpy.run_path(str(HERE.parent/'R19-阶段转换与多回合组合/正式对照.py'))
 replay=[old['replay'](p) for p in (HERE/'正式补覆盖').glob('*.json.gz')];assert len(replay)==1
 subprocess.run(['python3',str(HERE/'伤势校准.py')],check=True,capture_output=True,text=True,cwd=ROOT)
 calibration=json.loads((HERE/'伤势校准.json').read_text());assert not calibration['coverage_gaps']
 result=dict(initial_hands=756,independent_maximum_error=max(errors),strict_replays=replay,calibrated_records=calibration['source_records'],calibrated_steps=calibration['checked_steps'],passed=True)
 print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__':main()
