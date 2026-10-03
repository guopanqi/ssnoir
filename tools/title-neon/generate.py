#!/usr/bin/env python3
"""Generate the title sign centreline skeletons; run after editing the lettering below."""
import json, pathlib, uuid
root=pathlib.Path(__file__).resolve().parents[2] / 'UnityClient/Assets/Resources/Title'
root.mkdir(parents=True, exist_ok=True)
# Hand-drawn centreline lettering. Coordinates use a 1000 x 300 sign board.
paths=[]
def curve(start, segments):
 p=[start]; a=start
 for b,c,d in segments:
  for i in range(1,33):
   t=i/32; s=1-t
   p.append([s**3*a[0]+3*s*s*t*b[0]+3*s*t*t*c[0]+t**3*d[0],s**3*a[1]+3*s*s*t*b[1]+3*s*t*t*c[1]+t**3*d[1]])
  a=d
 paths.append([[x/1000,1-y/300] for x,y in p])
curve((48,212),[((80,150),(98,78),(114,66)),((134,38),(57,47),(51,94)),((45,116),(112,118),(143,88)),((197,32),(229,99),(143,135)),((239,129),(201,220),(108,213)),((73,210),(78,191),(109,183))])
curve((174,187),[((184,195),(195,199),(203,192))])
# Flowing e, l, l, e, v, i, l, l, e: connected exits at the baseline.
x=203
for letter,w in [('e',79),('l',60),('l',60),('e',79),('v',83),('i',47),('l',60),('l',60),('e',79)]:
 if letter=='e':
  curve((x,192),[((x+59,166),(x+65,134),(x+33,147)),((x-5,164),(x+6,230),(x+w,192))])
 elif letter=='l':
  curve((x,192),[((x+45,133),(x+71,53),(x+43,66)),((x+5,92),(x+5,235),(x+w,192))])
 elif letter=='v':
  curve((x,192),[((x+17,166),(x+21,141),(x+23,150)),((x+13,243),(x+42,224),(x+65,146)),((x+62,173),(x+73,184),(x+w,192))])
 else:
  curve((x,192),[((x+16,170),(x+21,145),(x+22,147)),((x+8,216),(x+21,217),(x+w,192))])
  curve((x+30,121),[((x+31,119),(x+33,117),(x+34,115))])
 x+=w
curve((x,192),[((931,142),(975,192),(891,231)),((743,302),(378,260),(165,267))])
(root/'Belleville.neon.json').write_text(json.dumps({'paths':paths,'accent':'#FF386D'},indent=2)+'\n')
# Narrow single-stroke marquee capitals.
glyphs={'T':[[[0,0],[1,0]],[[.5,0],[.5,1]]],'H':[[[0,0],[0,1]],[[1,0],[1,1]],[[0,.5],[1,.5]]],'E':[[[1,0],[0,0],[0,1],[1,1]],[[0,.5],[.8,.5]]],'B':[[[0,1],[0,0],[.7,0],[1,.2],[.7,.5],[0,.5]],[[.7,.5],[1,.7],[.8,1],[0,1]]],'A':[[[0,1],[.5,0],[1,1]],[[.23,.57],[.77,.57]]],'L':[[[0,0],[0,1],[1,1]]],'D':[[[0,1],[0,0],[.65,0],[1,.3],[1,.7],[.65,1],[0,1]]],'S':[[[1,.1],[.7,0],[.15,0],[0,.2],[.15,.45],[.85,.55],[1,.8],[.85,1],[.2,1],[0,.9]]],'O':[[[.2,0],[.8,0],[1,.2],[1,.8],[.8,1],[.2,1],[0,.8],[0,.2],[.2,0]]],'F':[[[0,1],[0,0],[1,0]],[[0,.5],[.8,.5]]]}
paths=[]; x=0
for ch in 'THE BALLADS OF':
 if ch==' ': x+=25; continue
 for line in glyphs[ch]: paths.append([[x+u*20,1-v] for u,v in line])
 x+=32
paths=[[[u/x,v] for u,v in p] for p in paths]
(root/'Ballads.neon.json').write_text(json.dumps({'paths':paths,'accent':'#BD78FF'},indent=2)+'\n')
for p in root.glob('*.json'):
 meta=p.with_name(p.name+'.meta')
 if meta.exists(): continue
 meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
