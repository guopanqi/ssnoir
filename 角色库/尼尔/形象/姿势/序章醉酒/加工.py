"""拆分 Gemini 四姿势，同一缩放、统一鞋底；原图黑色负空间与灰度描线导入。"""
from pathlib import Path
import json,re,uuid
from PIL import Image,ImageChops,ImageFilter
ROOT=Path(__file__).resolve().parents[5]
DEST=ROOT/'UnityClient/Assets/Resources/Portraits/Neon'
TEMPLATE=(DEST/'尼尔_silhouette.png.meta').read_text()
def extract(im):
    im=im.convert('RGB');r,g,b=im.split()
    key=ImageChops.subtract(ImageChops.darker(r,b),g)
    alpha=key.point(lambda v: max(0,min(255,round((1-v/200)*255))))
    # 清理色键边缘，色键背景 RGB 归零，避免压缩/mipmap 采样出紫边。
    pixels=[]
    for (rr,gg,bb),a in zip(im.getdata(),alpha.getdata()):
        if a<=2:pixels.append((0,0,0,0))
        elif a<255:
            t=a/255;pixels.append((max(0,min(255,round((rr-255*(1-t))/t))),max(0,min(255,round(gg/t))),max(0,min(255,round((bb-255*(1-t))/t))),a))
        else:pixels.append((rr,gg,bb,a))
    pixels=[(r,min(g,max(r,b)),b,a) for r,g,b,a in pixels]
    out=Image.new('RGBA',im.size);out.putdata(pixels);return out

def meta(path):
    path=Path(str(path)+'.meta')
    if not path.exists():path.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,TEMPLATE,count=1))

def save(name,im,scale):
    bbox=im.getchannel('A').point(lambda v:255 if v>96 else 0).getbbox()
    sized=im.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.LANCZOS)
    x=round(512-im.width*scale/2);y=round(1000-bbox[3]*scale)
    out=Image.new('RGBA',(1024,1024));out.alpha_composite(sized,(x,y))
    path=DEST/(name+'.png');out.save(path);meta(path)
    importer=Path(str(path)+'.meta')
    importer.write_text(importer.read_text().replace('grayScaleToAlpha: 0','grayScaleToAlpha: 1').replace('alphaIsTransparency: 1','alphaIsTransparency: 0'))
    mask=Image.new('RGBA',out.size,'white');mask.putalpha(out.getchannel('A'));sp=DEST/(name+'_silhouette.png');mask.save(sp);meta(sp)
    return {'name':name,'sourceSize':list(im.size),'sourceBounds':list(bbox),'scale':scale,'offset':[x,y],'ground':1000}

if __name__=='__main__':
    source=Image.open(Path(__file__).with_name('四姿势-描线.png'));w,h=source.size
    assert w==h and w%2==0
    records=[]
    for i,name in enumerate(['尼尔_醉站','尼尔_醉行','尼尔_扶墙','尼尔_摸钥匙']):
        x=(i%2)*w//2;y=(i//2)*h//2
        records.append(save(name,extract(source.crop((x,y,x+w//2,y+h//2))),1.9*512/(w//2)))
    landlord=extract(Image.open(ROOT/'角色库/房东/形象/抱臂-描线.png'))
    records.append(save('房东_抱臂',landlord,.90*1024/landlord.height))
    Path(__file__).with_name('加工记录.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n')
