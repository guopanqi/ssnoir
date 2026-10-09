import { createHash } from 'node:crypto';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

export async function sourceFingerprint(root) {
  const hash=createHash('sha256');
  const roots=['src','tools','index.html','package.json'];

  async function visit(target) {
    const stat = await import('node:fs/promises').then(m=>m.stat(target));
    if(stat.isDirectory()){
      const entries=(await readdir(target,{withFileTypes:true})).sort((a,b)=>a.name.localeCompare(b.name));
      for(const entry of entries) await visit(path.join(target,entry.name));
      return;
    }
    hash.update(path.relative(root,target).split(path.sep).join('/'));
    hash.update(await readFile(target));
  }

  for(const rel of roots) await visit(path.join(root,rel));
  return hash.digest('hex');
}
