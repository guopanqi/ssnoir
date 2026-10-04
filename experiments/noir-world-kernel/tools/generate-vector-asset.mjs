import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { normalizeAssetRequest } from '../src/ai/AssetRequest.js';
import { normalizeGeneratedSvg } from '../src/ai/normalizeSvg.js';
import { RecraftVectorProvider } from '../src/ai/providers/RecraftVectorProvider.js';

const ROOT=process.cwd();
const requestPath=process.argv[2];
if(!requestPath){
  console.error('Usage: npm run generate:vector -- ai/requests/<request>.json');
  process.exit(2);
}

const absolute=path.resolve(ROOT,requestPath);
const request=normalizeAssetRequest(JSON.parse(await readFile(absolute,'utf8')));
const provider=new RecraftVectorProvider();
const result=await provider.generate(request);
const normalized=normalizeGeneratedSvg(result.svg,{
  type:request.type,
  targetHeight:request.targetHeight
});

const generatedDir=path.join(ROOT,'src/assets/generated');
const runDir=path.join(ROOT,'ai/runs');
await mkdir(generatedDir,{recursive:true});
await mkdir(runDir,{recursive:true});

const slug=request.id.replace(/[^a-z0-9._-]+/gi,'-').toLowerCase();
const file=`${slug}.svg`;
await writeFile(path.join(generatedDir,file),normalized.svg+'\n');

const manifestPath=path.join(generatedDir,'manifest.generated.json');
let manifest=[];
try{manifest=JSON.parse(await readFile(manifestPath,'utf8'));}catch{}
const entry={
  id:request.id,
  type:request.type,
  file,
  pivot:normalized.meta.pivot,
  scale:normalized.meta.defaultScale,
  tags:['generated','recraft'],
  provider:'recraft',
  model:result.model,
  seed:result.seed
};
manifest=manifest.filter(item=>item.id!==entry.id);
manifest.push(entry);
manifest.sort((a,b)=>a.id.localeCompare(b.id));
await writeFile(manifestPath,JSON.stringify(manifest,null,2)+'\n');

const provenance={
  generatedAt:new Date().toISOString(),
  request,
  provider:result.provider,
  model:result.model,
  seed:result.seed,
  credits:result.credits,
  styleId:result.styleId,
  normalized:normalized.meta,
  prompt:result.prompt
};
await writeFile(path.join(runDir,`${slug}.json`),JSON.stringify(provenance,null,2)+'\n');

console.log(JSON.stringify({asset:entry,provenance:`ai/runs/${slug}.json`},null,2));
