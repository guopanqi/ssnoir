import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';

const ROOT=path.resolve('src/assets');
const ALLOWED_COLORS=new Set(['#010205','#f2efe6','#e2b63d','none']);
const FORBIDDEN=[
  /<script\b/i,
  /<foreignObject\b/i,
  /<image\b/i,
  /\bhref\s*=/i,
  /url\s*\(/i,
  /javascript\s*:/i
];

const files=(await readdir(ROOT)).filter(name=>name.endsWith('.svg')).sort();
const errors=[];

for(const file of files){
  const full=path.join(ROOT,file);
  const svg=await readFile(full,'utf8');

  if(!/<svg\b[^>]*\bviewBox\s*=\s*["'][^"']+["']/i.test(svg)){
    errors.push(`${file}: missing viewBox`);
  }

  for(const pattern of FORBIDDEN){
    if(pattern.test(svg)) errors.push(`${file}: forbidden SVG feature ${pattern}`);
  }

  for(const match of svg.matchAll(/(?:fill|stroke)\s*=\s*["']([^"']+)["']/gi)){
    const value=match[1].trim().toLowerCase();
    if(value.startsWith('#') && !ALLOWED_COLORS.has(value)){
      errors.push(`${file}: disallowed palette color ${value}`);
    }
  }

  for(const match of svg.matchAll(/stroke-width\s*=\s*["']([^"']+)["']/gi)){
    const width=Number.parseFloat(match[1]);
    if(!Number.isFinite(width) || width<0.4 || width>3.0){
      errors.push(`${file}: stroke-width ${match[1]} outside 0.4–3.0 source-unit range`);
    }
  }
}

if(!files.length) errors.push('No SVG assets found in src/assets.');

if(errors.length){
  console.error('Vector asset validation failed:');
  for(const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`Validated ${files.length} SVG assets: ${files.join(', ')}`);
