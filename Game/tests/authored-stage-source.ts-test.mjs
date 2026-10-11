import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {extractStageExpression} from "../src/runtime/authored-stage-source.ts";
test("the preview extracts exact original source without leaking following quest effects",()=>{
  const file=readFileSync("../UnityClient/Assets/Resources/Content/scenes/world/人物/弗兰克.scm","utf8");
  const expr=extractStageExpression(file);
  assert.ok(expr.startsWith("(play-stage!"));
  assert.ok(expr.includes('(stage-say "贝恩斯" "十分钟。"'));
  assert.ok(expr.trimEnd().endsWith('))'));
  assert.equal(expr.includes("(baines 'note-dock-seen!)"),false);
});
