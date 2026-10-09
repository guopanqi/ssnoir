// 基础立绘的确定性加工；补画腿脚由生图工具完成，这里只等比缩放和定位。
import sharp from '../../experiments/portrait-stage-lab/node_modules/sharp/lib/index.js';
import fs from 'node:fs/promises';
import path from 'node:path';

const root=path.resolve(import.meta.dirname,'../..');
const manifestPath=path.join(root,'角色库/基础立绘/制作基准.json');
const config=JSON.parse(await fs.readFile(manifestPath,'utf8'));
const out=path.join(root,'角色库/基础立绘/成品');
await fs.mkdir(out,{recursive:true});
const results=[];
for(const actor of config.characters){
 const source=path.join(root,actor.source);
 const {data,info}=await sharp(source).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const points=[];let left=info.width,right=-1,top=info.height,bottom=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const i=(y*info.width+x)*info.channels;
  if(Math.max(data[i],data[i+1],data[i+2])<60)continue;
  points.push([x,y]);left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
 }
 if(right<left||bottom>=info.height-1)throw Error(`${actor.name}: 源图为空或触底，必须先补齐鞋部`);
 const crown=top+actor.crownInset;
 if(crown>=bottom)throw Error(`${actor.name}: 头顶标定无效`);
 const scale=config.referenceBodyPixels*actor.relativeHeight/(bottom-crown);
 const feet=points.filter(([,y])=>y>bottom-(bottom-top)*.06);
 const footCenter=actor.footCenter??(Math.min(...feet.map(p=>p[0]))+Math.max(...feet.map(p=>p[0])))/2;
 // 保留光晕；源图带完整上下留白，输出共用同一画布与接地点。
 const scaledWidth=Math.round(info.width*scale),scaledHeight=Math.round(info.height*scale);
 const x=Math.round(config.center-footCenter*scale),y=Math.round(config.ground-bottom*scale);
 if(left*scale+x<12||right*scale+x>config.canvas-12||top*scale+y<12)throw Error(`${actor.name}: 标定导致主体越界`);
 const resized=await sharp(source).resize(scaledWidth,scaledHeight).png().toBuffer();
 const cropLeft=Math.max(0,-x),cropTop=Math.max(0,-y);
 const width=Math.min(scaledWidth-cropLeft,config.canvas-Math.max(0,x));
 const height=Math.min(scaledHeight-cropTop,config.canvas-Math.max(0,y));
 const tile=await sharp(resized).extract({left:cropLeft,top:cropTop,width,height}).png().toBuffer();
 const target=path.join(out,actor.name+'.png');
 await sharp({create:{width:config.canvas,height:config.canvas,channels:3,background:'#000000'}})
  .composite([{input:tile,left:Math.max(0,x),top:Math.max(0,y)}]).png().toFile(target);
 results.push({name:actor.name,source:actor.source,sourceSize:[info.width,info.height],bounds:[left,top,right,bottom],crown,footCenter,scale,ground:config.ground,bodyPixels:config.referenceBodyPixels*actor.relativeHeight});
}
await fs.writeFile(path.join(out,'加工记录.json'),JSON.stringify(results,null,2)+'\n');
console.log(`已统一 ${results.length} 张基础立绘；成品尚未复制至 Unity。`);
