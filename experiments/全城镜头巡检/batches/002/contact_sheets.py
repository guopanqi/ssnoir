import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
p=Path(__file__).parent
rows=json.loads((p/'frames.json').read_text());names=[n for n in dict.fromkeys(r['place'] for r in rows) if (p/'frames-geometry'/(n+'-100.png')).exists()]
font=ImageFont.truetype('/System/Library/Fonts/STHeiti Medium.ttc',20)
for start in range(0,len(names),3):
 group=names[start:start+3];im=Image.new('RGB',(1600,210*len(group)),(245,245,245));draw=ImageDraw.Draw(im)
 for y,name in enumerate(group):
  draw.text((5,y*210),name,fill='black',font=font)
  for x,r in enumerate([r for r in rows if r['place']==name]):
   src=Image.open(p/'frames-geometry'/Path(r['path']).name).convert('RGB');src.thumbnail((320,180));im.paste(src,(x*320,y*210+30));draw.text((x*320+250,y*210),str(round(r['progress']*100))+'%',fill='black',font=font)
 im.save(p/('sheet-%02d.png'%(start//3+1)))
