import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameRuntimeState} from "../src/runtime/game-state.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){
  for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
    const path=join(dir,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())scan(path);
    else if(entry.isFile()&&path.endsWith(".scm"))sources[path]=readFileSync(join(root,path),"utf8");
  }
}
scan("scripts");scan("scenes");

test("authored get-kwarg retains its semantics for quoted symbol keys, missing, duplicate and non-symbol keys",async()=>{
  const vm=await GameScriptSession.create("keyword-equality",new GameRuntimeState(),sources);
  const cases=[
    "(get-kwarg '(:name \"A\" :anchor \"dock\" :name \"B\") ':name \"none\")",
    "(get-kwarg '(:name \"A\" :anchor \"dock\") ':anchor #f)",
    "(get-kwarg '(:name \"A\" :anchor \"dock\") ':required-before-rest \"fallback\")",
    "(get-kwarg '(:name \"A\" :subtitle \"text\") ':subtitle #f)",
    "(get-kwarg '(:name \"A\" :last) ':last 93)",
    "(get-kwarg '() ':name 93)",
    "(get-kwarg '(3 \"num\" 5 \"other\") 3 #f)",
    "(get-kwarg '((a b) \"list\" (a c) \"other\") '(a b) #f)",
    "(get-kwarg '(:a #f :b 0) ':a #t)",
    "(get-kwarg '(:a 1 :b 2 :c 3) ':c 0)"
  ];
  const expected=['A','dock','fallback','text','93','93','num','list','false','3'];
  for(const [i,expr] of cases.entries())
    assert.equal(String(await vm.evaluate(expr)),expected[i],expr);
  await vm.evaluate("(define test-kwargs (list ':a 1 ':b 2))");
  assert.equal(String(await vm.evaluate("(get-kwarg test-kwargs ':b #f)")),"2");
  await vm.evaluate('(set-car! (cdr (cdr (cdr test-kwargs))) 9)');
  assert.equal(String(await vm.evaluate("(get-kwarg test-kwargs ':b #f)")),"9",
    "keyword lookups must read live mutable Scheme lists");
});


test("bundled Scenario paths accept authored Unicode punctuation but never traversal",async()=>{
  const virtualSources={...sources,
    "scenes/scenarios/雨夜跟车（识别版）.scm":'(define __unicode_scenario_path_test 17)'};
  const vm=await GameScriptSession.create("unicode-content-path",
    new GameRuntimeState(),virtualSources);
  await vm.loadFile("scenes/scenarios/雨夜跟车（识别版）.scm");
  assert.equal(await vm.evaluateNumber("__unicode_scenario_path_test"),17);
  await assert.rejects(vm.loadFile("scenes/../world/world.scm"),/Invalid content script path/);
});
