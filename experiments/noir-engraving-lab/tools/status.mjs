import assert from 'node:assert/strict';
import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { sourceFingerprint } from './source-fingerprint.mjs';

const root = process.cwd();
const readJson = async (file) => JSON.parse(await readFile(path.join(root, file), 'utf8'));
const checkpoint = await readJson('CHECKPOINT.json');
assert.equal(checkpoint.schemaVersion, 1, 'Unsupported checkpoint schema');
const baseline = checkpoint.acceptedBaseline;
assert.equal(baseline.visuallyReviewed, true, 'Accepted baseline must be visually reviewed');
assert(baseline.reviewDirectory.startsWith('captures/reviews/'), 'Baseline must be durable review evidence');
const manifest = await readJson(`${baseline.reviewDirectory}/manifest.json`);
assert.equal(manifest.sourceSha256, baseline.sourceSha256, 'Checkpoint and capture fingerprint disagree');
await access(path.join(root, baseline.reviewDirectory, 'contact-sheet.jpg'));
for (const shot of manifest.shots) {
  for (const [mode, config] of Object.entries(manifest.modes)) {
    const extension = config.type === 'jpeg' ? 'jpg' : 'png';
    await access(path.join(root, baseline.reviewDirectory, mode, `${shot.name}.${extension}`));
  }
}
assert.deepEqual(Object.keys(manifest.modes).sort(), ['final', 'line', 'preprint', 'shape']);
assert.equal(manifest.shots.length, 4, 'Baseline must cover four benchmark shots');
const current = await sourceFingerprint(root);
const matches = current === baseline.sourceSha256;
console.log(JSON.stringify({
  phase: checkpoint.phase,
  baseline: baseline.reviewDirectory,
  visualCodeCommit: baseline.visualCodeCommit,
  sourceMatchesBaseline: matches,
  currentSourceSha256: current,
  evidenceFilesPresent: true,
  pendingReviews: checkpoint.pendingReviews,
  nextQuestions: checkpoint.nextQuestions,
  instructions: 'Read CONTINUE.md, open baseline images, then select one research question.',
}, null, 2));
// A stale baseline is actionable, not permission to assume old images show new code.
process.exitCode = matches ? 0 : 2;
