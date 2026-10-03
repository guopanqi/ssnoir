import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { sourceFingerprint } from './source-fingerprint.mjs';

const root = process.cwd();
const checkpoint = JSON.parse(await readFile(path.join(root, 'CHECKPOINT.json'), 'utf8'));
const current = await sourceFingerprint(root);
const baseline = checkpoint.acceptedBaseline;

if (!baseline) {
  console.log(JSON.stringify({
    phase: checkpoint.phase,
    baseline: null,
    sourceMatchesBaseline: false,
    currentSourceSha256: current,
    pendingReviews: checkpoint.pendingReviews,
    nextQuestions: checkpoint.nextQuestions,
    instructions: 'No accepted visual baseline yet. Generate and review a complete capture before accepting one.',
  }, null, 2));
  process.exitCode = 2;
} else {
  const manifest = JSON.parse(await readFile(path.join(root, baseline.reviewDirectory, 'manifest.json'), 'utf8'));
  await access(path.join(root, baseline.reviewDirectory, 'contact-sheet.jpg'));
  const matches = manifest.sourceSha256 === baseline.sourceSha256 && current === baseline.sourceSha256;
  console.log(JSON.stringify({
    phase: checkpoint.phase,
    baseline: baseline.reviewDirectory,
    visualCodeCommit: baseline.visualCodeCommit,
    sourceMatchesBaseline: matches,
    currentSourceSha256: current,
    pendingReviews: checkpoint.pendingReviews,
    nextQuestions: checkpoint.nextQuestions,
  }, null, 2));
  process.exitCode = matches ? 0 : 2;
}
