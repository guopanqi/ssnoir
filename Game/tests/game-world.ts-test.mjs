import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync, readdirSync} from "node:fs";
import {join, relative} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {convertNode} from "../src/runtime/node-converter.ts";
import {convertDossier} from "../src/runtime/dossier-converter.ts";

const contentRoot = "../UnityClient/Assets/Resources/Content";
function scan(dir, dst) {
  for(const entry of readdirSync(join(contentRoot,dir),{withFileTypes:true})) {
    const path=join(dir,entry.name);
    if(entry.isDirectory())scan(path,dst);
    else if(entry.isFile()&&path.endsWith(".scm"))dst[path.replaceAll("\\","/")]=readFileSync(join(contentRoot,path),"utf8");
  }
}
const sourceMap={};scan("scripts",sourceMap);scan("scenes",sourceMap);
const std=readFileSync("node_modules/lips/dist/std.xcb");

test("original chapter-one world.scm loads on the new LIPS runtime and materializes root",async()=>{
  const state=new GameRuntimeState();
  const vm=await GameScriptSession.create("phase2-real-world",state,sourceMap,std);
  const loadResult=await vm.loadFile("scenes/world/world.scm");
  console.log("[PHASE2 WORLD DEBUG]",{
    loadResult:String(loadResult),
    hasWorldDay:String(await vm.evaluate("(bound? 'world-day)")),
    hasModule:String(await vm.evaluate("(bound? '模块卷宗)")),
    hasRender:String(await vm.evaluate("(bound? 'get-render-data)")),
    hasTurn:String(await vm.evaluate("(bound? 'on-turn-end)"))
  });
  const render=await vm.evaluate("(get-render-data)");
  const root=convertNode(render);
  assert.equal(root.name,"世界");
  assert.ok(root.children.length>0,"world should expose places or story nodes");
  const dossier=convertDossier(await vm.evaluate("(get-dossier)"));
  assert.ok(Array.isArray(dossier),"original authored dossier must be convertible");
  // On day one, BEFORE Neil accepts Nightingale's commission, the authored
  // module intentionally exposes no dossier. Entries appear after "有人敲门".
  assert.equal(dossier.length,0,"no commission dossier before the authored opening");
  assert.ok(dossier.every(entry=>entry.id&&Array.isArray(entry.steps)&&Array.isArray(entry.clocks)));
});
