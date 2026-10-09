import sharp from 'sharp';
import path from 'node:path';
const base=path.resolve(import.meta.dirname,'..');
const {data,info}=await sharp(path.join(base,'art/noir-manager.png')).ensureAlpha().raw().toBuffer({resolveWithObject:true});
let x0=info.width,x1=0,y0=info.height,y1=0;
for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
 const i=(y*info.width+x)*4;const [r,g,b]=data.subarray(i,i+3);
 if(g>55&&g>r*1.25&&g>b*1.2){data[i+3]=Math.round(255*Math.max(0,1-Math.min(g-r,g-b)/45));data[i+1]=Math.min(g,Math.max(r,b));}
 if(data[i+3]>128){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
}
await sharp(data,{raw:info}).extract({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1}).png().toFile(path.join(base,'public/assets/manager.png'));
