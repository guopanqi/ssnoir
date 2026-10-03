"""最后一页：逐步试玩之后的固定行动序列分支复核。"""
import gzip
import json
from pathlib import Path
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[3]
PLANS={
 '保护来源': ['撕掉这页','遮掉名字','抄下这页','遮掉名字','抄下这页','送出抄本'],
 '全抄不遮': ['抄下这页','抄下这页','抄下这页','送出抄本'],
 '全部烧掉': ['撕掉这页','撕掉这页','撕掉这页','送出抄本'],
 '直接送走': ['送出抄本'],
 '遮住后离开': ['遮掉名字','送出抄本'],
 '等搜查队': ['end-turn'],
}
EXPECTED={
 '保护来源': "('交出抄本 5 ())",
 '全抄不遮': "('交出抄本 6 (艾米 乔 露丝))",
 '全部烧掉': "('交出抄本 0 ())",
 '直接送走': "('交出抄本 0 (艾米 乔 露丝))",
 '遮住后离开': "('交出抄本 0 (乔 露丝))",
 '等搜查队': "('交出抄本 0 (艾米 乔 露丝))",
}

if __name__=='__main__':
 results=[]
 for name,plan in PLANS.items():
  p=subprocess.Popen(['dotnet','run','--no-build','--project','tools/content-validator/SSNoir.ContentValidator.csproj','--','--session','encounters/实验·最后一页','--seed','7'],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,text=True)
  def send(c):
   p.stdin.write(json.dumps(c,ensure_ascii=False)+'\n');p.stdin.flush()
   r=json.loads(p.stdout.readline());assert r['type']!='error',r
   return r
  o=json.loads(p.stdout.readline())['observation']
  for card in plan:
   candidates=[a for a in o['Operations'] if (a['Card'] or a['Kind'])==card]
   assert candidates,(name,card)
   a=min(candidates,key=lambda a:a['DieValue'] or 0)
   o=send({'command':'act','version':o['Version'],'operationId':a['Id'],'reason':'固定分支复核：'+name})['observation']
  assert o['EncounterResult']==EXPECTED[name],(name,o['EncounterResult'])
  with tempfile.TemporaryDirectory(prefix='noir-pages-') as scratch:
   raw=Path(scratch)/'record.json';send({'command':'save','path':str(raw)})
   directory=ROOT/'docs'/'实验'/'最后一页'/'对照记录';directory.mkdir(parents=True,exist_ok=True)
   with gzip.open(directory/f'{name}-7.json.gz','wb') as target:target.write(raw.read_bytes())
  send({'command':'quit'});p.wait()
  results.append({'policy':name,'seed':7,'result':o['EncounterResult']})
  print(name,o['EncounterResult'],flush=True)
 (ROOT/'docs'/'实验'/'最后一页'/'对照结果.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
