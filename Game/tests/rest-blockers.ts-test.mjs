import test from "node:test";
import assert from "node:assert/strict";
import { collectRequired, sameRequired } from "../src/runtime/rest-blockers.ts";
function node(name,extras={}) {
  return {name,title:name,disabled:false,isPlace:false,arrivals:[],children:[],
    requiredBeforeRest:null,resolve:null,...extras};
}
test("required rest action is derived from authored place and retains navigation target",()=>{
  const action=node("旅馆清晨",{requiredBeforeRest:"先起床看看",resolve:{kind:"instant"}});
  const root=node("世界",{children:[node("家",{isPlace:true,children:[action]})]});
  const required=collectRequired(root,true);
  assert.deepEqual(required,[{id:"旅馆清晨",reason:"先起床看看",location:"家",node:"旅馆清晨"}]);
  assert(sameRequired(required,[...required]));
  assert(!sameRequired(required,[]));
});
test("required event cannot be disabled, informational, or outside a world place",()=>{
  const bad=node("坏事件",{requiredBeforeRest:"必须处理",resolve:{kind:"note"},disabled:true});
  const root=node("世界",{children:[node("家",{isPlace:true,children:[bad]})]});
  assert.throws(()=>collectRequired(root,true),/no enabled processing path/);
  bad.disabled=false;
  assert.throws(()=>collectRequired(root,true),/no enabled processing path/);
  bad.resolve={kind:"instant"};
  assert.throws(()=>collectRequired(root,false),/must belong to a world place/);
  assert.equal(collectRequired(root,true).length,1);
});
