import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {convertNode} from "../src/runtime/node-converter.ts";
import {convertDossier} from "../src/runtime/dossier-converter.ts";
const root="../UnityClient/Assets/Resources/Content/";
const sources=Object.fromEntries(["stdlib","engine","theatre"].map(x=>[
  "scripts/"+x+".scm",readFileSync(root+"scripts/"+x+".scm","utf8")
]));
const std=readFileSync("node_modules/lips/dist/std.xcb");
const vm=()=>GameScriptSession.create("presentation-contract",new GameRuntimeState(),sources,std);

test("C#-equivalent clock display semantics are preserved in node and annotation",async()=>{
  const session=await vm();
  const expr=await session.evaluate(`(node "码头" :clocks
    (list (list 'clock "压力" 2 6 'countdown "经理催促" "最后期限")
          (list 'clock "调查" 4 8 'gauge "" "线索进度"))
    :children (list (clock-node "钟：工资" (list 'clock "工资" 3 5 'readout "今天挣的" "工资"))))`);
  const node=convertNode(expr);
  assert.equal(node.clocks[0].style,"countdown");
  assert.deepEqual(node.clocks[0],{label:"压力",displayLabel:"最后期限",current:2,max:6,
    style:"countdown",note:"经理催促"});
  assert.equal(node.clocks[1].style,"gauge");
  assert.equal(node.children[0].resolve.noteClock.style,"readout");
  assert.equal(node.children[0].resolve.noteTitle,"工资");
  assert.equal(node.children[0].resolve.noteText,"今天挣的");
  await assert.rejects(session.evaluate('(list 1) (invalid-call)'));
  const malformed=await session.evaluate('(node "错误时钟" :clocks (list (list \'clock "X" 2 3 \'progress)))');
  assert.throws(()=>convertNode(malformed),/unknown style/);
});
test("support and carried item metadata survive original node conversion",async()=>{
  const session=await vm();
  const value=await session.evaluate('(node "香烟" :carry-item "香烟" :support "弗兰克" :resolve (note "支援" "一次机会"))');
  const node=convertNode(value);
  assert.equal(node.carryItemId,"香烟");
  assert.equal(node.supportId,"弗兰克");
});
test("dossier converter accepts authored shape and rejects duplicate story identities",async()=>{
  const session=await vm();
  const source=`(list (list 'dossier "夜莺" ':kind '主线 ':primary #t ':status '进行中
    ':now "找到线索" ':where "老街" ':clocks
      (list (list 'clock "三天" 2 3 'countdown "催促" "期限"))
    ':steps (list (list 'step "检查信封" #t) (list 'step "找到莱恩" #f))
    ':log (list (list 'journal-entry 2 "有人见过他"))))`;
  const dossier=convertDossier(await session.evaluate(source));
  assert.equal(dossier[0].id,"夜莺");
  assert.equal(dossier[0].primary,true);
  assert.equal(dossier[0].clocks[0].displayLabel,"期限");
  assert.deepEqual(dossier[0].steps.map(x=>x.done),[true,false]);
  assert.equal(dossier[0].log[0].day,2);
  assert.throws(()=>convertDossier([{}]),/proper Scheme list|dossier/);
});
