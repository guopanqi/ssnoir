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
const std=readFileSync("node_modules/lips/dist/std.xcb");

test("fast get-kwarg is equivalent to authored Scheme for missing, duplicate and non-symbol keys",async()=>{
  const vm=await GameScriptSession.create("keyword-equality",new GameRuntimeState(),sources,std);
  const cases=[
    "(get-kwarg '(:name \"A\" :anchor \"dock\" :name \"B\") :name \"none\")",
    "(get-kwarg '(:name \"A\" :anchor \"dock\") :anchor #f)",
    "(get-kwarg '(:name \"A\" :anchor \"dock\") :required-before-rest \"fallback\")",
    "(get-kwarg '(:name \"A\" :subtitle \"text\") :subtitle #f)",
    "(get-kwarg '(:name \"A\" :last) :last 93)",
    "(get-kwarg '() :name 93)",
    "(get-kwarg '(3 \"num\" 5 \"other\") 3 #f)",
    "(get-kwarg '((a b) \"list\" (a c) \"other\") '(a b) #f)",
    "(get-kwarg '(:a #f :b 0) :a #t)",
    "(get-kwarg '(:a 1 :b 2 :c 3) :c 0)"
  ];
  for(const expr of cases){
    vm.setFastKeywordLookup(false);
    const expected=String(await vm.evaluate(expr));
    vm.setFastKeywordLookup(true);
    assert.equal(String(await vm.evaluate(expr)),expected,expr);
  }
  await vm.evaluate('(define test-kwargs (list :a 1 :b 2))');
  assert.equal(String(await vm.evaluate('(get-kwarg test-kwargs :b #f)')),"2");
  await vm.evaluate('(set-car! (cdr (cdr (cdr test-kwargs))) 9)');
  assert.equal(String(await vm.evaluate('(get-kwarg test-kwargs :b #f)')),"9",
    "keyword lookups must read live mutable Scheme lists");
});
