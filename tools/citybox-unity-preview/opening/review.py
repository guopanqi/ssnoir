"""在正式 Main 的独立缓存中采样开场运镜候选，不替换游戏开场。"""
from pathlib import Path
import argparse,json,os,re,subprocess,sys,time,hashlib
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'tools/unity'))
from staging import sync_project
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--route',type=Path)
p.add_argument('--baseline',action='store_true',help='回放当前开场原有的三段 PlayStoryCamera')
p.add_argument('--out',required=True,type=Path)
p.add_argument('--fps',type=int,default=8)
p.add_argument('--height',type=int,default=540)
a=p.parse_args()
project=ROOT/'.cache/citybox-unity-preview-formal'
assert (project/'.ssnoir-world-preview').is_file(),'先用正式世界预览建立缓存'
a.out=a.out.resolve()
assert not (a.route and a.baseline),'候选与原版分别录制'
assert 1<=a.fps<=60 and 180<=a.height<=2160,'无效采样率或尺寸'
assert ROOT/'UnityClient' not in a.out.parents and project not in a.out.parents and a.out!=project,'不能输出到工程中'
assert not a.out.exists() or not any(a.out.iterdir()),'输出目录必须是新轮次'
a.out.mkdir(parents=True,exist_ok=True)
sync_project(ROOT/'UnityClient',project)
script=project/'Assets/Editor/OpeningCameraReview.cs'
script.write_bytes(Path(__file__).with_name('OpeningCameraReview.cs').read_bytes())
inputs=[project/'Assets/Scenes/Main.unity',project/'Assets/Resources/City/WorldPalette.asset',project/'Assets/Resources/Models/Environment/City.fbx',script]
if a.route:
 (a.out/'route.json').write_bytes(a.route.read_bytes());inputs.append(a.out/'route.json')
input_hashes={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in inputs}
env=dict(os.environ,SSNOIR_OPENING_OUTPUT=str(a.out),SSNOIR_OPENING_ROUTE=str(a.out/'route.json') if a.route else '',SSNOIR_OPENING_FPS=str(a.fps),SSNOIR_OPENING_HEIGHT=str(a.height),SSNOIR_OPENING_BASELINE='1' if a.baseline else '0')
with (a.out/'launch.log').open('w') as log:
 r=subprocess.Popen(['/Applications/Unity/Unity.app/Contents/MacOS/Unity','-batchmode','-job-worker-count','2','-buildTarget','WebGL','-projectPath',str(project),'-executeMethod','SSNoir.Editor.OpeningCameraReview.Render','-logFile',str(a.out/'unity.log')],env=env,stdout=log,stderr=subprocess.STDOUT)
 started=time.monotonic();shutdown_terminated=False
 while r.poll() is None:
  complete=a.out/'result.json';logtext=(a.out/'unity.log').read_text(errors='replace') if (a.out/'unity.log').exists() else ''
  if complete.exists() and time.time()-complete.stat().st_mtime>15 and 'Shutdown' in logtext:
   r.terminate();r.wait(timeout=20);shutdown_terminated=True;break
  if time.monotonic()-started>600:
   r.terminate();r.wait(timeout=20);raise TimeoutError(str(a.out/'unity.log'))
  time.sleep(.25)
 if shutdown_terminated:
  result=json.loads((a.out/'result.json').read_text());result['ownedEditorTerminatedAfterShutdown']=True
  (a.out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
text=(a.out/'unity.log').read_text(errors='replace')
assert (r.returncode==0 or shutdown_terminated) and (a.out/'result.json').is_file(),str(a.out/'unity.log')
assert not re.search(r'Exception:|error CS\d+|Shader error|Assertion failed',text),str(a.out/'unity.log')
result=json.loads((a.out/'result.json').read_text())
result['inputSha256']=input_hashes
(a.out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
if a.route or a.baseline:
 result=json.loads((a.out/'result.json').read_text());realtime=result['fps']==0
 if realtime:
  result=json.loads((a.out/'result.json').read_text());samples=result['samples']
  concat=[]
  for i,row in enumerate(samples):
   concat.append("file '"+str(a.out/f'frames/{i:05d}.png')+"'")
   if i<len(samples)-1:concat.append('duration '+str(max(.001,samples[i+1]['time']-row['time'])))
  (a.out/'frames.txt').write_text('\n'.join(concat)+'\n')
  video=['ffmpeg','-y','-loglevel','error','-safe','0','-f','concat','-i',str(a.out/'frames.txt'),'-fps_mode','vfr']
 else:video=['ffmpeg','-y','-loglevel','error','-framerate',str(a.fps),'-i',str(a.out/'frames/%05d.png')]
 subprocess.run(video+['-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(a.out/'review.mp4')],check=True)
 from PIL import Image,ImageDraw
 frames=sorted((a.out/'frames').glob('*.png'));selected=[frames[round(i*(len(frames)-1)/19)] for i in range(20)]
 width=400;height=round(a.height*width/(a.height*16//9));sheet=Image.new('RGB',(width*4,(height+28)*5),(15,18,24));draw=ImageDraw.Draw(sheet)
 for i,f in enumerate(selected):
  x=i%4*width;y=i//4*(height+28);im=Image.open(f);im.thumbnail((width,height));sheet.paste(im,(x,y));draw.text((x+8,y+height+4),f"{samples[int(f.stem)]['time'] if realtime else int(f.stem)/a.fps:.2f}s",fill='white')
 sheet.save(a.out/'index.jpg',quality=92)
print(a.out)
