"""Render deterministic cutout poses without Chromium (also useful for silhouette/contact inspection)."""
import json, subprocess, argparse
from pathlib import Path
import numpy as np, cv2
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
RIG=json.loads((ROOT/'rig.json').read_text())
PART={p['id']:p for p in RIG['parts']}
source_file=ROOT/'assets/source-atlas.png'
if not source_file.exists():source_file=ROOT/'assets/puppet-atlas.png'
source=np.asarray(Image.open(source_file).convert('RGBA').resize((1448,1086),Image.Resampling.LANCZOS))
SRC_W=1448
W,H=1600,900
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
def m_translate(x,y):return np.array([[1,0,x],[0,1,y],[0,0,1]],dtype=np.float64)
def m_rotate(r):return np.array([[np.cos(r),-np.sin(r),0],[np.sin(r),np.cos(r),0],[0,0,1]],dtype=np.float64)
def sample(scene,t):
 js=f"import{{sampleScene}}from './scenes.js';console.log(JSON.stringify(sampleScene('{scene}',{t})));"
 return json.loads(subprocess.check_output(['node','--input-type=module','-e',js],cwd=ROOT,text=True))
def draw(scene,t,mode='stage',output=None):
 S=sample(scene,t)
 bg=np.zeros((H,W,4),dtype=np.uint8);bg[:,:,:3]=(12,13,17);bg[:,:,3]=255
 # same virtual baseline, aspect ratio; neutral panel leaves adequate space for the moving figure.
 if mode=='stage':x=S['x']*W;sc=.47;by=784
 else:x=800;sc=.42;by=784
 angles=S['angles'] if mode!='static' else {}
 mats={}
 def getmat(pid):
  if pid in mats:return mats[pid]
  p=PART[pid];par=PART[p['parent']] if p['parent'] else None
  m=getmat(par['id']) if par else np.eye(3)
  j=p['joint'];q=par['joint'] if par else (0,0)
  m=m@m_translate(j[0]-q[0],j[1]-q[1])@m_rotate(angles.get(pid,0))
  mats[pid]=m
  return m
 # PIL composed with alpha using compositing rather than warp onto opaque canvas.
 bgImage=Image.fromarray(bg,'RGBA')
 for p in sorted(RIG['parts'],key=lambda v:v['z']):
  sx,sy,sw,sh=p['crop'];px,py=p['sourcePivot']
  patch=source[sy:sy+sh,sx:sx+sw]
  # Canvas transform converts source crop pixel (i,j) to stage pixels.
  mat=m_translate(x,by+S['bob'])@np.diag([sc,sc,1])@m_translate(-700,-1055)@getmat(p['id'])@m_translate(sx-px,sy-py)
  warped=cv2.warpAffine(patch,mat[:2,:],(W,H),flags=cv2.INTER_LINEAR,borderMode=cv2.BORDER_CONSTANT,borderValue=(0,0,0,0))
  bgImage=Image.alpha_composite(bgImage,Image.fromarray(warped,'RGBA'))
 d=ImageDraw.Draw(bgImage)
 d.line((0,783,W,783),fill='#65717f',width=2)
 if font:
  f=ImageFont.truetype(font,25)
  d.text((40,35),f'{scene.upper()} / {t:.1f}s / {mode}',font=f,fill='#d7bf8b')
 if output:bgImage.convert('RGB').save(output)
 return bgImage
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--out',default=str(ROOT/'captures')); a=p.parse_args();d=Path(a.out);d.mkdir(exist_ok=True)
 selection=[('drunk',0,'static'),('drunk',4,'stage'),('drunk',8,'stage'),('brace',4,'stage'),('brace',6,'stage'),('run',4,'stage'),('run',6,'stage')]
 for name,time,mode in selection:draw(name,time,mode,d/f'{name}-{time:g}-{mode}.png')
 thumbs=[]
 for name,time,mode in selection:
  im=Image.open(d/f'{name}-{time:g}-{mode}.png');im.thumbnail((700,395));thumbs.append(im)
 contact=Image.new('RGB',(1400,395*4),'#101216')
 for k,im in enumerate(thumbs):contact.paste(im,((k%2)*700,(k//2)*395))
 contact.save(d/'python-preview.jpg',quality=92)
 print('Saved',len(selection),'deterministic poses')
