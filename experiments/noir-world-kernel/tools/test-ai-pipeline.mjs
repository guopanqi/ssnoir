import assert from 'node:assert/strict';
import { normalizeAssetRequest, buildVectorPrompt } from '../src/ai/AssetRequest.js';
import { normalizeGeneratedSvg } from '../src/ai/normalizeSvg.js';
import { validateSvgText } from './vector-validation.mjs';

const request=normalizeAssetRequest({
  id:'generated.test.detective',
  type:'puppet',
  subject:'full-body private detective in a long coat and narrow fedora',
  view:'three-quarter walking pose',
  seed:42,
  styleMatch:'precise'
});
const prompt=buildVectorPrompt(request);
assert.match(prompt,/transparent empty background/i);
assert.match(prompt,/flat black silhouette/i);
assert.equal(request.styleMatch,'precise');

assert.throws(()=>normalizeAssetRequest({
  id:'bad',
  type:'puppet',
  subject:'x',
  styleMatch:'maximum'
}),/styleMatch/);

assert.throws(()=>normalizeAssetRequest({
  id:'bad',
  type:'puppet',
  subject:'x',
  model:'recraftv4_1_vector',
  styleMatch:'regular'
}),/V4\/V4\.1/);

assert.throws(()=>normalizeAssetRequest({
  id:'bad',
  type:'puppet',
  subject:'x',
  targetHeight:-2
}),/targetHeight/);

const mock=`<svg viewBox="0 0 100 300" xmlns="http://www.w3.org/2000/svg">
  <path fill="#121212" stroke="#faf5e9" stroke-width="5.7" d="M20 20 L80 20 L70 280 L30 280 Z"/>
  <path fill="#d9a52d" stroke="none" d="M45 40 L55 40 L55 50 L45 50 Z"/>
</svg>`;

const result=normalizeGeneratedSvg(mock,{type:'puppet',targetHeight:3.15});
assert.match(result.svg,/#010205/i);
assert.match(result.svg,/#f2efe6/i);
assert.match(result.svg,/#e2b63d/i);
assert.doesNotMatch(result.svg,/stroke-width="5\.7"/);
assert.equal(result.meta.pivot[0],50);
assert.equal(result.meta.pivot[1],300);
assert.equal(result.meta.shapeCount,2);
assert(Math.abs(result.meta.defaultScale-.0105)<.000001);

const validation=validateSvgText(result.svg,'normalized-test.svg');
assert.deepEqual(validation.errors,[]);
assert.equal(validation.shapeCount,2);

assert.throws(()=>normalizeGeneratedSvg('<svg viewBox="0 0 10 10"><image href="x.png"/></svg>'),/forbidden/i);
assert.throws(()=>normalizeGeneratedSvg('<svg viewBox="0 0 10 10"><path fill="url(#g)" d="M0 0"/></svg>'),/forbidden/i);

console.log('AI vector pipeline mock test passed.');
