import sharp from '../portrait-stage-lab/node_modules/sharp/lib/index.js';
import fs from 'node:fs/promises';
const [file,folder,...flips]=process.argv.slice(2);
const nightOnly=flips.includes('--night-only'),singleNeil=flips.includes('--single-neil'),singleNight=flips.includes('--single-night');
if(!file||!folder)throw Error('Usage: node prepare-cast.mjs sheet.png folder [indices to mirror]');
const {data,info}=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
for(let p=0;p<info.width*info.height;p++){
 const i=p*4,r=data[i],g=data[i+1],b=data[i+2];
 if(g>100&&g>r*1.35&&g>b*1.35){const excess=g-Math.max(r,b);data[i+3]=Math.round(255*Math.max(0,1-excess/95));data[i+1]=Math.max(r,b);}
}
const occupied=[];for(let x=0;x<info.width;x++){let n=0;for(let y=0;y<info.height;y++)if(data[(y*info.width+x)*4+3]>128)n++;occupied[x]=n>3;}
const spans=[];let start=-1;
for(let x=0;x<=info.width;x++){if(occupied[x]&&start<0)start=x;if(!occupied[x]&&start>=0){const prev=spans.at(-1);if(prev&&start-prev.right<8)prev.right=x;else spans.push({left:start,right:x});start=-1;}}
if(spans.length!==(singleNeil||singleNight?1:nightOnly?2:4))throw Error(`Expected four separate sprites, got ${JSON.stringify(spans)}`);
const parts=spans.map(({left,right})=>{let top=info.height,bottom=0;for(let x=left;x<right;x++)for(let y=0;y<info.height;y++)if(data[(y*info.width+x)*4+3]>128){top=Math.min(top,y);bottom=Math.max(bottom,y)}let footLeft=right,footRight=left;for(let y=Math.max(top,bottom-Math.round((bottom-top)*.07));y<=bottom;y++)for(let x=left;x<right;x++)if(data[(y*info.width+x)*4+3]>128){footLeft=Math.min(footLeft,x);footRight=Math.max(footRight,x)}return {left,right,top,bottom,footCenter:(footLeft+footRight)/2};});
// Normalize each pair together; preserve pose height differences, align feet exactly.
const names=singleNight?['night-neutral']:singleNeil?['neil-neutral']:nightOnly?['night-neutral','night-vulnerable']:['neil-neutral','neil-question','night-neutral','night-vulnerable'];await fs.mkdir(`experiments/portrait-stage/line-stage-study/assets/${folder}`,{recursive:true});
for(let i=0;i<names.length;i++){
 const part=parts[i],pair=singleNeil||singleNight||nightOnly?parts:parts.slice(i<2?0:2,i<2?2:4),pairHeight=Math.max(...pair.map(p=>p.bottom-p.top+1)),pairWidth=Math.ceil(2*Math.max(...pair.flatMap(p=>[p.footCenter-p.left,p.right-p.footCenter])));
 const width=part.right-part.left,height=part.bottom-part.top+1;
 let tile=sharp(data,{raw:info}).extract({left:part.left,top:part.top,width,height});if(flips.includes(String(i)))tile=tile.flop();
 const crop=await tile.png().toBuffer();
 await sharp({create:{width:pairWidth+16,height:pairHeight+12,channels:4,background:'#00000000'}}).composite([{input:crop,left:Math.round((pairWidth+16)/2-(part.footCenter-part.left)),top:pairHeight+6-height}]).png().toFile(`experiments/portrait-stage/line-stage-study/assets/${folder}/${names[i]}.png`);
}
await fs.writeFile(file+'.crop.json',JSON.stringify({info,parts,flips,names},null,2));console.log(`Prepared ${folder}`);
