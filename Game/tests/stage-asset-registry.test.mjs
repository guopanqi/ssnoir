import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
test("first authored stage characters use bounded actual Resources assets on all hosts",()=>{
  const code=readFileSync("src/foundation/stage-assets.ts","utf8");
  assert.match(code,/尼尔_silhouette\.png\?inline/);
  assert.match(code,/夜莺_silhouette\.png\?inline/);
  assert.match(code,/弗兰克_silhouette\.png\?inline/);
  assert.match(code,/贝恩斯_silhouette\.png\?inline/);
  assert.doesNotMatch(code,/import\.meta\.glob/);
  assert.match(readFileSync("src/foundation/render.ts","utf8"),/new StageImageRegistry\(/);
});
