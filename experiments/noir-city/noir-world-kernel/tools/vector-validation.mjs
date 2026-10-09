import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';

export const ALLOWED_COLORS=new Set(['#010205','#f2efe6','#e2b63d','none']);
export const FORBIDDEN=[
  /<script\b/i,
  /<foreignObject\b/i,
  /<image\b/i,
  /<style\b/i,
  /\bhref\s*=/i,
  /url\s*\(/i,
  /javascript\s*:/i
];

function inspectPaint(value,label,key,errors){
  const paint=value.trim().toLowerCase();
  if(!ALLOWED_COLORS.has(paint)){
    errors.push(`${label}: disallowed ${key} value ${paint}`);
  }
}

function inspectStyle(style,label,errors){
  for(const part of style.split(';').map(x=>x.trim()).filter(Boolean)){
    const index=part.indexOf(':');
    if(index<0) continue;
    const key=part.slice(0,index).trim().toLowerCase();
    const value=part.slice(index+1).trim();

    if(key==='fill'||key==='stroke') inspectPaint(value,label,`style ${key}`,errors);
    if(key==='stroke-width'){
      const width=Number.parseFloat(value);
      if(!Number.isFinite(width)||width<.4||width>3){
        errors.push(`${label}: style stroke-width ${value} outside 0.4–3.0 source-unit range`);
      }
    }
  }
}

export function validateSvgText(svg,label='asset.svg'){
  const errors=[];
  if(typeof svg!=='string'||!svg.includes('<svg')) errors.push(`${label}: not SVG text`);
  if(Buffer.byteLength(svg,'utf8')>600_000) errors.push(`${label}: exceeds 600 KB`);

  if(!/<svg\b[^>]*\bviewBox\s*=\s*["'][^"']+["']/i.test(svg)){
    errors.push(`${label}: missing viewBox`);
  }

  for(const pattern of FORBIDDEN){
    if(pattern.test(svg)) errors.push(`${label}: forbidden SVG feature ${pattern}`);
  }

  const shapes=(svg.match(/<(?:path|rect|circle|ellipse|polygon|polyline|line)\b/gi)||[]).length;
  if(shapes<1) errors.push(`${label}: contains no vector shapes`);
  if(shapes>180) errors.push(`${label}: too fragmented (${shapes} shapes; max 180)`);

  for(const match of svg.matchAll(/\b(fill|stroke)\s*=\s*["']([^"']+)["']/gi)){
    inspectPaint(match[2],label,match[1].toLowerCase(),errors);
  }

  for(const match of svg.matchAll(/stroke-width\s*=\s*["']([^"']+)["']/gi)){
    const width=Number.parseFloat(match[1]);
    if(!Number.isFinite(width)||width<.4||width>3){
      errors.push(`${label}: stroke-width ${match[1]} outside 0.4–3.0 source-unit range`);
    }
  }

  for(const match of svg.matchAll(/\bstyle\s*=\s*["']([^"']*)["']/gi)){
    inspectStyle(match[1],label,errors);
  }

  return {errors,shapeCount:shapes,bytes:Buffer.byteLength(svg,'utf8')};
}

export async function findSvgFiles(root){
  const out=[];
  async function walk(dir){
    for(const entry of await readdir(dir,{withFileTypes:true})){
      const full=path.join(dir,entry.name);
      if(entry.isDirectory()) await walk(full);
      else if(entry.isFile()&&entry.name.toLowerCase().endsWith('.svg')) out.push(full);
    }
  }
  await walk(root);
  return out.sort();
}

export async function validateSvgTree(root){
  const files=await findSvgFiles(root);
  const errors=[];
  const records=[];
  for(const full of files){
    const rel=path.relative(root,full).replaceAll(path.sep,'/');
    const svg=await readFile(full,'utf8');
    const result=validateSvgText(svg,rel);
    errors.push(...result.errors);
    records.push({file:rel,...result});
  }
  return {files,records,errors};
}
