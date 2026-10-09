import sharp from 'sharp';
import {fileURLToPath} from 'node:url';
const base=fileURLToPath(new URL('../',import.meta.url));
const {data,info}=await sharp(`${base}art/third/contact.png`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
for(let i=0;i<data.length;i+=4){const r=data[i],g=data[i+1],b=data[i+2];if(g>80&&g>r*1.3&&g>b*1.3){const excess=Math.min(g-r,g-b);data[i+3]=Math.round(255*Math.max(0,1-excess/65));data[i+1]=Math.min(g,Math.max(r,b));}}
await sharp(data,{raw:info}).trim({background:'#00000000',threshold:20}).png().toFile(`${base}public/assets/third/contact.png`);
console.log('Contact sprite prepared.');
