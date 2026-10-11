import test from "node:test";
import assert from "node:assert/strict";
import {InjuryState} from "../src/runtime/injury.ts";

test("injury mirrors the single C# skill-specific 0/1-3/4-6/7 scale",()=>{
 const i=new InjuryState();
 assert.equal(i.band,0);
 assert.equal(i.injure(1,()=>0),false);
 assert.equal(i.part,"手");
 assert.equal(i.skill,"violence");
 assert.equal(i.band,1);
 assert.equal(i.modifier,-1);
 i.injure(3);
 assert.equal(i.band,2);
 assert.equal(i.modifier,0);
 assert.equal(i.losesDie,true);
 i.heal(1);
 assert.equal(i.band,1);
 i.heal(20);
 assert.deepEqual(i.snapshot(),{severity:0,part:"",scars:[]});
});
test("save injury/scars rejects bad parts and retains all known scar records",()=>{
 const i=new InjuryState();
 i.injure(2,()=>0.6);
 i.addScar(3);
 const save=i.snapshot();
 const restored=new InjuryState();
 restored.restore(JSON.parse(JSON.stringify(save)));
 assert.deepEqual(restored.snapshot(),save);
 assert.equal(restored.scarCount("眼"),1);
 assert.throws(()=>restored.restore({...save,part:"脚"}),/invalid injury save/);
 assert.deepEqual(restored.snapshot(),save);
});
