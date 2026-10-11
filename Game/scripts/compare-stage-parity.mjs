import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import assert from "node:assert/strict";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {parseStage} from "../src/runtime/stage-contract.ts";
const reference=JSON.parse(readFileSync(process.argv[2],"utf8"));
const source=readFileSync(process.argv[3],"utf8");
const root="../UnityClient/Assets/Resources/Content",scripts={};
function scan(dir){for(const entry of readdirSync(join(root,dir),{withFileTypes:true})){
  const name=join(dir,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())scan(name);
  else if(name.endsWith(".scm"))scripts[name]=readFileSync(join(root,name),"utf8");
}}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
const runtime=await GameSceneDirector.create(scripts,std,()=>.42);
runtime.state.takeStoryEvents();
await runtime.world.evaluate(source);
const event=runtime.state.takeStoryEvents().find(e=>e.kind==="stage");
assert.ok(event,"original Scheme stage event missing");
const beats=parseStage(event.args);
const number=value=>{
  if(value===null||value===undefined||value==="NaN"||Number.isNaN(value))return null;
  return Math.round(Number(value)*100000)/100000;
};
const fromCsharp=reference.beats.map(beat=>beat.Commands.map(c=>({
  kind:c.Kind.toLowerCase().replace("propat","prop-at").replace("path",c.Relative?"path-relative":"path"),
  id:c.Id,asset:c.Asset,anchor:c.Anchor,layer:c.Layer,relative:c.Relative,
  x:number(c.X),y:number(c.Y),seconds:number(c.Seconds),
  points:c.Points.map(p=>[number(p.X),number(p.Y)]),
  speaker:c.Line?.Speaker??"",text:c.Line?.Text??""
})));
const fromTS=beats.map(beat=>beat.commands.map(c=>({
  kind:c.kind,id:c.id,asset:c.asset,anchor:c.anchor,layer:c.layer,relative:c.relative,
  x:number(c.x),y:number(c.y),seconds:number(c.seconds),
  points:c.points.map(p=>[number(p[0]),number(p[1])]),
  speaker:c.speaker,text:c.text
})));
assert.deepEqual(fromTS,fromCsharp,
  "TS stage contract must match C# StoryStageSequence from the same unchanged authored expression");
console.log("PASS: original Frank stage "+beats.length+" beats agree with C# semantics");
