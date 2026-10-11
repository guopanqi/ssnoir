import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {spawn} from "node:child_process";
import readline from "node:readline";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";

const base="../UnityClient/Assets/Resources/Content",sources={};
function scan(dir){for(const entry of readdirSync(join(base,dir),{withFileTypes:true})){
  const key=join(dir,entry.name).replaceAll("\\","/");
  if(entry.isDirectory())scan(key);
  else if(key.endsWith(".scm"))sources[key]=readFileSync(join(base,key),"utf8");
}}
scan("scripts");scan("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
const ts=await GameSceneDirector.create(sources,std,()=>.42);
const tsRoot=await ts.render();
function pathTo(node,name,parents=[]){
  if(node.name===name)return [...parents,node.name];
  for(const child of node.children){
    const found=pathTo(child,name,[...parents,node.name]);
    if(found)return found;
  }
  return null;
}
const doorPath=pathTo(tsRoot,"有人敲门");
assert.ok(doorPath,"SSNoir's original world no longer exposes the opening");
const place=tsRoot.children.find(node=>node.isPlace&&pathTo(node,"有人敲门"));
assert.ok(place,"opening should live in an enterable place");
assert.equal(ts.state.getItemCount("金钱"),15);

const child=spawn("dotnet",[
  "run","--no-build","--project",
  "../tools/content-validator/SSNoir.ContentValidator.csproj",
  "--","--session","world","--seed","7"
],{cwd:process.cwd(),stdio:["pipe","pipe","pipe"]});
let diagnostic="";
child.stderr.setEncoding("utf8");
child.stderr.on("data",chunk=>diagnostic+=chunk);
const reader=readline.createInterface({input:child.stdout,crlfDelay:Infinity});
const lines=reader[Symbol.asyncIterator]();
async function receive(){
  for(let index=0;index<100;index++){
    const next=await lines.next();
    if(next.done)throw Error("C# session quit prematurely: "+diagnostic);
    try{
      const data=JSON.parse(next.value);
      if(data.type==="error")throw Error("C# session: "+data.message);
      if(data.type==="observation"||data.type==="result")return data.observation;
    }catch(error){
      if(String(error).includes("C# session:"))throw error;
    }
  }
  throw Error("no C# observation");
}
const prop=(obj,key)=>obj[key]??obj[key.toLowerCase()]??obj[key[0].toLowerCase()+key.slice(1)];
function findAction(observation,kind,name){
  const rows=prop(observation,"Operations");
  const op=rows.find(item=>prop(item,"Kind")===kind&&String(prop(item,"Label")).includes(name));
  if(!op)throw Error("C# "+kind+" not found for "+name+": "+
    rows.map(x=>prop(x,"Kind")+":"+prop(x,"Label")).join(" / "));
  return op;
}
async function act(observation,op){
  const version=prop(observation,"Version"),id=prop(op,"Id");
  child.stdin.write(JSON.stringify({command:"act",version,operationId:id})+"\n");
  return receive();
}
let csharp=await receive();
try{
  assert.equal(Number(prop(csharp,"Inventory")["金钱"]),15);
  csharp=await act(csharp,findAction(csharp,"enter-place",place.name));
  await ts.worldActions.enterPlace(place.name);
  await ts.flush();
  const relative=pathTo(place,"有人敲门");
  assert.ok(relative);
  for(const branch of relative.slice(1,-1))
    csharp=await act(csharp,findAction(csharp,"navigate",branch));
  const door=findAction(csharp,"action","有人敲门");
  csharp=await act(csharp,door);
  await ts.worldActions.prepare();
  await ts.worldActions.performSlots("有人敲门",[]);
  await ts.flush();
  const cMoney=Number(prop(csharp,"Inventory")["金钱"]);
  assert.equal(cMoney,35,"original Schemy/C# chapter-one opening reward");
  assert.equal(ts.state.getItemCount("金钱"),cMoney,
    "new TS/LIPS game must exactly match the C# authored payout");
  assert.equal(Number(prop(csharp,"WorldDay")),Number(ts.state.getGlobal("世界日")),
    "world calendar must not advance when accepting the original commission");
  console.log("PASS: original opening C# vs TypeScript cash and day parity, "+place.name);
}finally{
  child.stdin.write('{"command":"quit"}\n');
  child.stdin.end();
  child.kill();
  reader.close();
}
