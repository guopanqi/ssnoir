const INK='#010205';
const PAPER='#f2efe6';
const GOLD='#e2b63d';
const PALETTE=new Set([INK,PAPER,GOLD,'none']);

const FORBIDDEN=[
  /<script\b/i,/<foreignObject\b/i,/<image\b/i,/<style\b/i,
  /\bhref\s*=/i,/url\s*\(/i,/javascript\s*:/i
];

function clamp(value,min,max){return Math.max(min,Math.min(max,value));}

function parseColor(value){
  const v=value.trim().toLowerCase();
  if(v==='none') return null;
  if(v==='black') return [0,0,0];
  if(v==='white') return [255,255,255];
  let match=v.match(/^#([0-9a-f]{3})$/i);
  if(match){
    return match[1].split('').map(ch=>parseInt(ch+ch,16));
  }
  match=v.match(/^#([0-9a-f]{6})(?:[0-9a-f]{2})?$/i);
  if(match){
    return [0,2,4].map(i=>parseInt(match[1].slice(i,i+2),16));
  }
  match=v.match(/^rgb\(\s*(\d+(?:\.\d+)?)\s*[, ]\s*(\d+(?:\.\d+)?)\s*[, ]\s*(\d+(?:\.\d+)?)\s*\)$/i);
  if(match) return match.slice(1).map(Number);
  return null;
}

function mapColor(value,{role='fill'}={}){
  const raw=value.trim();
  if(raw.toLowerCase()==='none') return 'none';
  if(PALETTE.has(raw.toLowerCase())) return raw.toLowerCase();
  const rgb=parseColor(raw);
  if(!rgb) throw new Error(`Unsupported generated SVG color: ${raw}`);
  const [r,g,b]=rgb;
  const goldLike=r>145&&g>85&&b<135&&r>g*1.04;
  if(goldLike) return GOLD;
  const luma=.2126*r+.7152*g+.0722*b;
  if(role==='stroke') return luma>70?PAPER:INK;
  return luma>150?PAPER:INK;
}

function parseViewBox(svg){
  const match=svg.match(/\bviewBox\s*=\s*["']\s*([-+]?\d*\.?\d+)\s+([-+]?\d*\.?\d+)\s+([-+]?\d*\.?\d+)\s+([-+]?\d*\.?\d+)\s*["']/i);
  if(!match) throw new Error('Generated SVG must contain a numeric viewBox.');
  const values=match.slice(1).map(Number);
  if(values.some(v=>!Number.isFinite(v))||values[2]<=0||values[3]<=0) throw new Error('Invalid SVG viewBox.');
  return values;
}

export function normalizeGeneratedSvg(source,{type='prop',targetHeight=null}={}){
  if(typeof source!=='string'||!source.includes('<svg')) throw new Error('Provider did not return SVG text.');
  if(Buffer.byteLength(source,'utf8')>600_000) throw new Error('Generated SVG exceeds 600 KB.');
  for(const pattern of FORBIDDEN){
    if(pattern.test(source)) throw new Error(`Generated SVG contains forbidden feature: ${pattern}`);
  }

  let svg=source
    .replace(/<\?xml[^>]*>/gi,'')
    .replace(/<!--([\s\S]*?)-->/g,'')
    .trim();

  const viewBox=parseViewBox(svg);
  const shapeCount=(svg.match(/<(?:path|rect|circle|ellipse|polygon|polyline|line)\b/gi)||[]).length;
  if(shapeCount<1) throw new Error('Generated SVG has no vector shapes.');
  if(shapeCount>180) throw new Error(`Generated SVG is too fragmented (${shapeCount} shapes; max 180).`);

  svg=svg.replace(/\b(fill|stroke)\s*=\s*(["'])([^"']+)\2/gi,(full,key,quote,value)=>{
    const mapped=mapColor(value,{role:key.toLowerCase()});
    return `${key}=${quote}${mapped}${quote}`;
  });

  svg=svg.replace(/\bstyle\s*=\s*(["'])([^"']*)\1/gi,(full,quote,style)=>{
    const cleaned=style
      .split(';')
      .map(part=>part.trim())
      .filter(Boolean)
      .map(part=>{
        const index=part.indexOf(':');
        if(index<0) return part;
        const key=part.slice(0,index).trim().toLowerCase();
        const value=part.slice(index+1).trim();
        if(key==='fill'||key==='stroke') return `${key}:${mapColor(value,{role:key})}`;
        if(key==='stroke-width'){
          const width=Number.parseFloat(value);
          if(!Number.isFinite(width)) throw new Error(`Invalid stroke-width: ${value}`);
          return `stroke-width:${clamp(width,.4,3).toFixed(2)}`;
        }
        return part;
      })
      .join(';');
    return `style=${quote}${cleaned}${quote}`;
  });

  svg=svg.replace(/\bstroke-width\s*=\s*(["'])([^"']+)\1/gi,(full,quote,value)=>{
    const width=Number.parseFloat(value);
    if(!Number.isFinite(width)) throw new Error(`Invalid stroke-width: ${value}`);
    return `stroke-width=${quote}${clamp(width,.4,3).toFixed(2)}${quote}`;
  });

  if(!/\bxmlns\s*=/.test(svg)) svg=svg.replace(/<svg\b/i,'<svg xmlns="http://www.w3.org/2000/svg"');

  const [minX,minY,width,height]=viewBox;
  const desired=Number(targetHeight??(type==='puppet'?3.15:1.5));
  const pivot=[minX+width/2,minY+height];
  const scale=desired/height;

  return {
    svg,
    meta:{
      viewBox,
      pivot:pivot.map(v=>Number(v.toFixed(4))),
      defaultScale:Number(scale.toFixed(7)),
      shapeCount,
      bytes:Buffer.byteLength(svg,'utf8')
    }
  };
}
