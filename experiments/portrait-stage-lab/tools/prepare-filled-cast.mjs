import sharp from 'sharp';
import fs from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const base=fileURLToPath(new URL('../',import.meta.url));
const [file,...args]=process.argv.slice(2);const names=args.filter(a=>!a.startsWith('--'));
const flipArg=args.find(a=>a.startsWith('--flip='));const flipped=flipArg?flipArg.slice(7).split(','):[];
if(!file||!names.length||flipped.some(n=>!names.includes(n)))throw Error('Usage: prepare-filled-cast.mjs sheet.png name [...] [--flip=name,...]');
const {data,info}=await sharp(`${base}art/filled-cast/${file}`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
// Edge-connected keying protects opaque gray skin that resembles a muted backdrop.
const backdrop=[data[0],data[1],data[2]];
if(backdrop[1]<=backdrop[0]||backdrop[1]<=backdrop[2])throw Error('Expected a green backdrop');
const count=info.width*info.height,mask=new Uint8Array(count),queue=new Int32Array(count);let tail=0;
const visit=p=>{if(mask[p])return;const i=p*4;if(Math.hypot(data[i]-backdrop[0],data[i+1]-backdrop[1],data[i+2]-backdrop[2])<42){mask[p]=1;queue[tail++]=p;}};
for(let x=0;x<info.width;x++){visit(x);visit((info.height-1)*info.width+x);}
for(let y=0;y<info.height;y++){visit(y*info.width);visit(y*info.width+info.width-1);}
for(let head=0;head<tail;head++){const p=queue[head],x=p%info.width,y=Math.floor(p/info.width);if(x)visit(p-1);if(x<info.width-1)visit(p+1);if(y)visit(p-info.width);if(y<info.height-1)visit(p+info.width);}
for(let p=0;p<count;p++){
 const i=p*4,r=data[i],g=data[i+1],b=data[i+2];
 if(mask[p])data[i+3]=0;
 else if(g>r*1.3&&g>b*1.3){const excess=Math.min(g-r,g-b);data[i+3]=Math.round(255*Math.max(0,1-excess/70));data[i+1]=Math.min(g,Math.max(r,b));}
}
const spans=[];let start=-1;
for(let x=0;x<=info.width;x++){
 let count=0;if(x<info.width)for(let y=0;y<info.height;y++)if(data[(y*info.width+x)*4+3]>128)count++;
 if(count>3&&start<0)start=x;
 if(count<=3&&start>=0){const previous=spans.at(-1);if(previous&&start-previous.right<=3)previous.right=x;else spans.push({left:start,right:x});start=-1;}
}
if(spans.length!==names.length)throw Error(`Expected ${names.length} separated figures; found ${spans.length}: ${JSON.stringify(spans)}`);
const parts=[];
for(let n=0;n<names.length;n++){
 const {left,right}=spans[n];let x0=right,x1=left,y0=info.height,y1=0;
 for(let y=0;y<info.height;y++)for(let x=left;x<right;x++)if(data[(y*info.width+x)*4+3]>128){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
 if(x1<=x0||y1<=y0)throw Error(`Empty sprite ${names[n]}`);
 parts.push({x0,x1,y0,y1});
}
const top=Math.max(0,Math.min(...parts.map(p=>p.y0))-5),bottom=Math.min(info.height,Math.max(...parts.map(p=>p.y1))+6);
const width=Math.max(...parts.map(p=>p.x1-p.x0+1))+12;
for(let n=0;n<names.length;n++){
 const p=parts[n],w=p.x1-p.x0+1;const tile=sharp(data,{raw:info}).extract({left:p.x0,top,width:w,height:bottom-top});if(flipped.includes(names[n]))tile.flop();const png=await tile.png().toBuffer();
 await sharp({create:{width,height:bottom-top,channels:4,background:'#00000000'}}).composite([{input:png,left:Math.floor((width-w)/2),top:0}]).png().toFile(`${base}public/assets/filled-cast/${names[n]}.png`);
}
await fs.writeFile(`${base}art/filled-cast/${file}.crop.json`,JSON.stringify({file,info,parts,top,bottom,width,names,flipped},null,2));console.log(`Prepared ${names.join(', ')}.`);
