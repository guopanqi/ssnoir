import sharp from 'sharp';
import fs from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const base=fileURLToPath(new URL('../',import.meta.url));
const root=fileURLToPath(new URL('../../../',import.meta.url));
const dest=`${base}public/assets/world-stage/`;
for(const name of ['01-soft-planes','02-emergent-light','03-ink-wash','04-screenprint']){
 const {data,info}=await sharp(name==='02-emergent-light'?`${base}art/world-stage/02-stage.png`:`${base}art/flowing-studies/${name}.png`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const bg=[data[0],data[1],data[2]],n=info.width*info.height,mask=new Uint8Array(n),queue=new Int32Array(n);let tail=0;
 const dist=p=>Math.hypot(...bg.map((v,c)=>data[p*4+c]-v));
 const visit=p=>{if(!mask[p]&&dist(p)<23){mask[p]=1;queue[tail++]=p;}};
 for(let x=0;x<info.width;x++){visit(x);visit((info.height-1)*info.width+x);}
 for(let y=0;y<info.height;y++){visit(y*info.width);visit(y*info.width+info.width-1);}
 for(let h=0;h<tail;h++){const p=queue[h],x=p%info.width,y=Math.floor(p/info.width);if(x)visit(p-1);if(x<info.width-1)visit(p+1);if(y)visit(p-info.width);if(y<info.height-1)visit(p+info.width);}
 for(let p=0;p<n;p++)if(mask[p]){const a=Math.max(0,Math.min(1,(dist(p)-9)/14));for(let c=0;c<3;c++)data[p*4+c]=a>.05?Math.max(0,Math.min(255,(data[p*4+c]-bg[c]*(1-a))/a)):0;data[p*4+3]=Math.round(a*255);}
 if(name==='02-emergent-light'){
  for(let i=0;i<data.length;i+=4)if(data[i+1]>data[i]*1.3&&data[i+1]>data[i+2]*1.3){const excess=Math.min(data[i+1]-data[i],data[i+1]-data[i+2]);data[i+3]=Math.min(data[i+3],Math.round(255*Math.max(0,1-excess/65)));data[i+1]=Math.min(data[i+1],Math.max(data[i],data[i+2]));}
  await sharp(data,{raw:info}).extract({left:285,top:55,width:420,height:969}).resize(228,525).png().toFile(`${dest}${name}.png`);
 }else await sharp(data,{raw:info}).extract({left:220,top:45,width:500,height:875}).resize(300,525).png().toFile(`${dest}${name}.png`);
}
await fs.copyFile(`${base}public/assets/filled-cast/nightingale-wait.png`,`${dest}flat.png`);
await fs.copyFile(`${base}public/assets/filled-cast/neil-listen.png`,`${dest}neil.png`);
const {data,info}=await sharp(`${root}角色库/夜莺/形象/夜莺_neon.png`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
for(let i=0;i<data.length;i+=4){const a=Math.max(data[i],data[i+1],data[i+2])/255;data[i+3]=Math.round(a*255);for(let c=0;c<3;c++)data[i+c]=a?Math.round(data[i+c]/a):0;}
await sharp(data,{raw:info}).extract({left:215,top:45,width:440,height:875}).resize(264,525).png().toFile(`${dest}neon.png`);
console.log('Prepared six comparison directions; source grey backdrops removed.');
const male=await sharp(`${base}art/world-stage/neil-02.png`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
for(let i=0;i<male.data.length;i+=4){const r=male.data[i],g=male.data[i+1],b=male.data[i+2];if(g>r*1.3&&g>b*1.3){const excess=Math.min(g-r,g-b);male.data[i+3]=Math.round(255*Math.max(0,1-excess/65));male.data[i+1]=Math.min(g,Math.max(r,b));}}
await sharp(male.data,{raw:male.info}).trim({background:'#00000000',threshold:10}).resize({height:525}).png().toFile(`${dest}neil-02.png`);
