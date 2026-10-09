import json
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[2]
source=root/'UnityClient/Assets/Resources/Portraits/Neon'
data=json.loads((source/'夜莺_低头.neon.json').read_text())
rgb=Image.open(source/'夜莺_低头.png').convert('RGB')
paths=[]
for points in data['paths']:
 colors=[rgb.getpixel((min(rgb.width-1,int(x*rgb.width)),min(rgb.height-1,int((1-y)*rgb.height)))) for x,y in points]
 blue=sum(1 for r,g,b in colors if b>r*1.2 and b>g*1.08)>len(colors)*.4
 paths.append({'points':[[round(x*1024,2),round((1-y)*1024,2)] for x,y in points],'color':'#7387ff' if blue else '#eef0e5'})
im=Image.open(source/'夜莺_低头_silhouette.png').getchannel('A').resize((256,256))
filled={(x,y) for y in range(256) for x in range(256) if im.getpixel((x,y))>127}
edges={}
for x,y in filled:
 for neighbor,a,b in [((x,y-1),(x,y),(x+1,y)),((x+1,y),(x+1,y),(x+1,y+1)),((x,y+1),(x+1,y+1),(x,y+1)),((x-1,y),(x,y+1),(x,y))]:
  if neighbor not in filled:edges.setdefault(a,[]).append(b)
loops=[]
while edges:
 start=next(iter(edges));p=start;loop=[]
 while True:
  loop.append([p[0]*4,p[1]*4]);q=edges[p].pop()
  if not edges[p]:del edges[p]
  p=q
  if p==start:break
 if len(loop)>8:loops.append(loop)
out=root/'experiments/portrait-stage/line-stage-study/assets/path-study/night.json'
out.write_text(json.dumps({'paths':paths,'outline':loops},separators=(',',':')))
print(len(paths),'paths',len(loops),'outlines')

# 黑底 Neon 转成透明光层，保持源图颜色与完整画幅，不生成或重画人物。
rgba = rgb.convert('RGBA')
pixels = []
for r,g,b in rgb.getdata():
 a = max(r,g,b)
 pixels.append((round(r*255/a) if a else 0, round(g*255/a) if a else 0, round(b*255/a) if a else 0, a))
rgba.putdata(pixels)
rgba.save(out.with_name('night-original.png'))
