import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {convertNode} from "../src/runtime/node-converter.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function walk(path){
  for(const entry of readdirSync(join(root,path),{withFileTypes:true})){
    const name=join(path,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())walk(name);
    else if(entry.isFile()&&name.endsWith(".scm"))sources[name]=readFileSync(join(root,name),"utf8");
  }
}
walk("scripts");walk("scenes");
const standard=readFileSync("node_modules/lips/dist/std.xcb");

test("ransom night hides unknown routes, disables unavailable ambush, and keeps no-money fallback",async()=>{
  const state=new GameRuntimeState();
  const vm=await GameScriptSession.create("first-letter-night-choices",state,sources,standard);
  await vm.loadFile("scenes/world/world.scm");
  await vm.evaluate(`(three-letters 'load!
    (list (list "story-stage" 1) (list "delivery-day" 5) (list "delivery-pending?" #t)))`);
  const night=async()=>convertNode(await vm.evaluate(`(car (three-letters 'nodes-at "码头"))`));
  const children=async()=>(await night()).children;
  const names=async()=>(await children()).map(n=>n.name);

  assert.equal((await night()).name,"交信之夜");
  assert.deepEqual(await names(),["交付信封"]);
  assert.equal((await children())[0].disabled,false);
  assert.equal(state.getItemCount("金钱"),15,"shortage must not hide the basic option");

  await vm.evaluate(`(dock 'load! (list
    (list "exploration" 4) (list "worker-asked?" #t)
    (list "worker-joined?" #f)))`);
  assert.deepEqual(await names(),["交付信封","设伏抓人"]);
  assert.equal((await children())[1].disabled,true);

  await vm.evaluate(`(photo-case 'load! (list
    (list "stage" 3) (list "source-stage" 3) (list "evidence" "照片")))`);
  assert.deepEqual(await names(),["交付信封","设伏抓人","利用照片尾随"]);
  assert.equal((await children())[2].disabled,false);

  await vm.evaluate(`(dock 'load! (list
    (list "exploration" 4) (list "worker-asked?" #t)
    (list "worker-joined?" #t) (list "worker-shift" 4)))`);
  assert.equal((await children())[1].disabled,false);
  await vm.evaluate(`(three-letters 'load!
    (list (list "story-stage" 1) (list "delivery-day" 5)
          (list "delivery-pending?" #t) (list "photo-follow-choice?" #t)))`);
  assert.deepEqual(await names(),["标注：尾随风险","坚持尾随","再考虑其他办法"]);
  const saved=await vm.evaluateNumber(`(if (assoc-get (three-letters 'save) "photo-follow-choice?" #f) 1 0)`);
  assert.equal(saved,1,"pending confirmation must be saved");
  assert.equal((await children())[1].resolve?.kind,"roll");
});
