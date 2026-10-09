import { createHash } from 'node:crypto';
import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';

// This fingerprints src/ only, not documentation, dependencies or the capture tool.
// Capture and continuation checks must use the same ordering and byte contract.
export async function sourceFingerprint(root) {
  const hash = createHash('sha256');
  async function visit(directory) {
    const entries = (await readdir(directory, { withFileTypes: true }))
      .sort((a, b) => a.name.localeCompare(b.name));
    for (const entry of entries) {
      const file = path.join(directory, entry.name);
      if (entry.isDirectory()) await visit(file);
      else {
        hash.update(path.relative(root, file).split(path.sep).join('/'));
        hash.update(await readFile(file));
      }
    }
  }
  await visit(path.join(root, 'src'));
  return hash.digest('hex');
}
