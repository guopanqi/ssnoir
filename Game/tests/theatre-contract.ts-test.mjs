import test from "node:test";
import assert from "node:assert/strict";
import {parseTheatre} from "../src/runtime/theatre-contract.ts";
const object=(id,shape,geometry,props=[],parent="")=>
  [id,parent,shape,geometry,"#FFFFFF",shape==="image"?"Portraits/Neon/尼尔":"", "",props];
const group=object("scene","group",[]);
const line=object("wire","line",[2,[[0,0],[120,12]]],[["reveal",0]],"scene");
const person=object("neil","image",[200,300],[["x",120],["y",400]],"scene");

test("Unity theatre wire parser accepts nested authored tween, subtitle and background light",()=>{
  const scene=[1600,900,"#101010",[group,line,person]];
  const program=["sequence",["parallel",
    ["tween","wire","reveal",[3,1,"smooth"]],
    ["tween","neil","x",[3,600,"smooth"]]],
    ["say","尼尔","你在吗？"],
    ["clear-caption"]];
  const out=parseTheatre([scene,program]);
  assert.equal(out.objects.length,3);
  assert.deepEqual(out.cue.children.map(x=>x.kind),["parallel","say","clear-caption"]);
  assert.equal(out.cue.children[0].seconds,3);
});
test("Unity theatre concurrent property writes and malformed parent/shape fail loudly",()=>{
  const scene=[1600,900,"#000000",[group,line,person]];
  assert.throws(()=>parseTheatre([scene,["parallel",
    ["tween","wire","reveal",[1,1,"linear"]],
    ["tween","wire","reveal",[2,1,"smooth"]]]]),/concurrent/);
  assert.throws(()=>parseTheatre([[100,100,"#000000",[person,group]],["wait",1]]),/parent/);
  assert.throws(()=>parseTheatre([scene,["tween","neil","reveal",[1,1,"linear"]]]),/line/);
  assert.throws(()=>parseTheatre([scene,["sound","narration","Sounds/x",false,5,0]]),/range/);
});
