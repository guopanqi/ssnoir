import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { validateSvgTree } from './vector-validation.mjs';

const ROOT=path.resolve('src/assets');
const GENERATED=path.join(ROOT,'generated');
const MANIFEST=path.join(GENERATED,'manifest.generated.json');

const result=await validateSvgTree(ROOT);
const errors=[...result.errors];

let manifest=[];
try{
  manifest=JSON.parse(await readFile(MANIFEST,'utf8'));
  if(!Array.isArray(manifest)) errors.push('generated/manifest.generated.json: root must be an array');
}catch(error){
  errors.push(`generated/manifest.generated.json: ${error.message}`);
}

const ids=new Set();
const files=new Set();
for(const [index,entry] of manifest.entries()){
  const prefix=`generated manifest[${index}]`;
  if(!entry||typeof entry!=='object'){
    errors.push(`${prefix}: must be an object`);
    continue;
  }
  if(!/^[a-z0-9][a-z0-9._-]+$/i.test(String(entry.id??''))) errors.push(`${prefix}: invalid id`);
  if(ids.has(entry.id)) errors.push(`${prefix}: duplicate id ${entry.id}`);
  ids.add(entry.id);

  if(!['puppet','prop'].includes(entry.type)) errors.push(`${prefix}: unsupported type ${entry.type}`);
  if(typeof entry.file!=='string'||!entry.file.endsWith('.svg')) errors.push(`${prefix}: invalid file`);
  if(files.has(entry.file)) errors.push(`${prefix}: duplicate file ${entry.file}`);
  files.add(entry.file);

  if(!Array.isArray(entry.pivot)||entry.pivot.length!==2||entry.pivot.some(v=>!Number.isFinite(Number(v)))){
    errors.push(`${prefix}: pivot must be [x,y]`);
  }
  if(!Number.isFinite(Number(entry.scale))||Number(entry.scale)<=0){
    errors.push(`${prefix}: scale must be > 0`);
  }
}

const generatedFiles=result.records
  .map(record=>record.file)
  .filter(file=>file.startsWith('generated/')&&file!=='generated/manifest.generated.json')
  .map(file=>file.slice('generated/'.length));

for(const file of files){
  if(!generatedFiles.includes(file)) errors.push(`generated manifest: missing SVG file ${file}`);
}
for(const file of generatedFiles){
  if(!files.has(file)) errors.push(`generated/${file}: not registered in manifest.generated.json`);
}

if(!result.files.length) errors.push('No SVG assets found in src/assets.');

if(errors.length){
  console.error('Vector asset validation failed:');
  for(const error of errors) console.error(`- ${error}`);
  process.exit(1);
}

console.log(`Validated ${result.files.length} SVG assets recursively; generated catalog entries: ${manifest.length}.`);
