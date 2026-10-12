import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {spawn} from "node:child_process";
import readline from "node:readline";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {WorldTurnController} from "../src/runtime/world-turn.ts";

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
const tavernPath=pathTo(tsRoot,"醉酒的尼尔");
assert.ok(tavernPath,"original opening must start at the old-street tavern");
const tavern=tsRoot.children.find(node=>node.isPlace&&pathTo(node,"醉酒的尼尔"));
assert.ok(tavern,"authored prologue must live in an enterable tavern");
assert.equal(pathTo(tsRoot,"有人敲门"),null,"Nightingale door is day-three content");
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
  csharp=await act(csharp,findAction(csharp,"enter-place",tavern.name));
  await ts.worldActions.enterPlace(tavern.name);
  await ts.flush();
  for(const branch of tavernPath.slice(2,-1))
    csharp=await act(csharp,findAction(csharp,"navigate",branch));
  csharp=await act(csharp,findAction(csharp,"action","醉酒的尼尔"));
  await ts.worldActions.performSlots("醉酒的尼尔",[]);
  await ts.flush();
  const turn=new WorldTurnController(ts.state,ts.world);
  for(const expectedDay of [2,3]){
    csharp=await act(csharp,findAction(csharp,"end-turn","结束回合"));
    const tsDay=await turn.endDay(()=>.42);
    assert.equal(tsDay.after,expectedDay,"authored calendar advances once");
    assert.equal(Number(prop(csharp,"WorldDay")),expectedDay,"C# daily transition matches TS");
  }
  // The C# session navigation stack is still inside the tavern after sleep.
  while(prop(csharp,"Operations").some(op=>prop(op,"Kind")==="back")){
    const back=prop(csharp,"Operations").find(op=>prop(op,"Kind")==="back");
    csharp=await act(csharp,back);
  }
  await ts.worldActions.prepare();
  const dayThree=ts.worldActions.getPreparedRoot();
  const doorPath=pathTo(dayThree,"有人敲门");
  assert.ok(doorPath,"Nightingale must knock at home on day three");
  const home=dayThree.children.find(node=>node.isPlace&&pathTo(node,"有人敲门"));
  assert.ok(home,"the original door is now in Neil's home");
  csharp=await act(csharp,findAction(csharp,"enter-place",home.name));
  await ts.worldActions.enterPlace(home.name);
  await ts.flush();
  for(const branch of doorPath.slice(2,-1))
    csharp=await act(csharp,findAction(csharp,"navigate",branch));
  csharp=await act(csharp,findAction(csharp,"action","有人敲门"));
  await ts.worldActions.performSlots("有人敲门",[]);
  await ts.flush();
  const cMoney=Number(prop(csharp,"Inventory")["金钱"]);
  assert.equal(cMoney,35,"original Schemy/C# chapter-one opening reward");
  assert.equal(ts.state.getItemCount("金钱"),cMoney,
    "new TS/LIPS game must exactly match the C# authored payout");
  assert.equal(Number(prop(csharp,"WorldDay")),Number(ts.state.getGlobal("世界日")),
    "world calendar must not advance when accepting the original commission");
  console.log("PASS: original tavern → 2 nights → door / 20-gold commission parity, "+
    home.name);
}finally{
  child.stdin.write('{"command":"quit"}\n');
  child.stdin.end();
  child.kill();
  reader.close();
}
