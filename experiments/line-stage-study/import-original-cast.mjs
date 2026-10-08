import sharp from '../portrait-stage-lab/node_modules/sharp/lib/index.js';
import fs from 'node:fs/promises';
for(const [actor,name] of [['neil','尼尔'],['night','夜莺']]){
 const folder='experiments/line-stage-study/assets/original-neon';await fs.mkdir(folder,{recursive:true});
 const source=`UnityClient/Assets/Resources/Portraits/Neon/${name}.png`;
 const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,right=0,top=info.height,bottom=0;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const i=(y*info.width+x)*4,m=Math.max(data[i],data[i+1],data[i+2]);
  if(m>20){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y)}
  // Black-backed neon is additive light: unpremultiply into transparent pixels.
  data[i+3]=m;for(let c=0;c<3;c++)data[i+c]=m?Math.round(data[i+c]*255/m):0;
 }
 await sharp(data,{raw:info}).extract({left,top,width:right-left+1,height:bottom-top+1}).png().toFile(`${folder}/${actor}-neutral.png`);
}
