import sharp from 'sharp';
import path from 'node:path';
import fs from 'node:fs/promises';
const base = path.resolve(import.meta.dirname, '..');
const records = [];
for (const [file, names] of [['noir-night-poses.png', ['nightingale-wait', 'nightingale-down']], ['noir-neil-poses.png', ['neil-listen', 'neil-hat']]]) {
  const { data, info } = await sharp(path.join(base, 'art', file)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const [r,g,b] = data.subarray(i,i+3);
    if (g > 65 && g > r * 1.25 && g > b * 1.2) {
      const excess = Math.min(g-r,g-b); data[i+3] = Math.round(255*Math.max(0,1-excess/45)); data[i+1] = Math.min(g, Math.max(r,b));
    }
  }
  const width = Math.floor(info.width/2); const parts=[];
  for(let n=0;n<2;n++) {
    const left=n*width; const colWidth=n===1?info.width-left:width;
    let x0=colWidth,x1=0,y0=info.height,y1=0;
    for(let y=0;y<info.height;y++)for(let x=0;x<colWidth;x++)if(data[(y*info.width+left+x)*4+3]>128){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
    if(x1<=x0||y1<=y0)throw Error(`Empty sprite ${names[n]}`);parts.push({left,x0,x1,y0,y1});
  }
  const top=Math.max(0,Math.min(...parts.map(s=>s.y0))-3),bottom=Math.min(info.height,Math.max(...parts.map(s=>s.y1))+4);
  const outWidth=Math.max(...parts.map(s=>s.x1-s.x0))+12;
  for(let n=0;n<2;n++) {
    const s=parts[n];const spriteWidth=s.x1-s.x0+1;
    const cropped=await sharp(data,{raw:info}).extract({left:s.left+s.x0,top,width:spriteWidth,height:bottom-top}).png().toBuffer();
    const sprite = await sharp({create:{width:outWidth,height:bottom-top,channels:4,background:'#00000000'}}).composite([{input:cropped,left:Math.round((outWidth-spriteWidth)/2),top:0}]).png().toBuffer();
    await sharp(sprite).png().toFile(path.join(base,`public/assets/${names[n]}.png`));
  }
  records.push({file,width:info.width,height:info.height,parts});
}
await fs.writeFile(path.join(base,'art/crop-metadata.json'),JSON.stringify(records,null,2));
console.log('Extracted four aligned sprites.');
