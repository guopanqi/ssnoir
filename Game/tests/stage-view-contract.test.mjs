import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
test("stage view and game renderer share a single Pixi canvas and original StagePlayback",()=>{
 const view=readFileSync("src/foundation/stage-view.ts","utf8");
 const host=readFileSync("src/foundation/render.ts","utf8");
 assert.match(view,/export class StageVectorView/);
 assert.match(view,/sync\(player:StagePlayback\)/);
 assert.match(host,/new StageVectorView\(/);
 assert.match(host,/parseStage\(frame.commands\)/);
 assert.match(host,/activeStage\.tick\(seconds\)/);
 assert.doesNotMatch(view,/new (WebGLRenderer|Application)\(/);
});
