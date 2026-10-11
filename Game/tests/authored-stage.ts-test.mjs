import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameSceneDirector} from "../src/runtime/scene-director.ts";
import {parseStage} from "../src/runtime/stage-contract.ts";
import {StagePlayback} from "../src/runtime/stage-playback.ts";

const root="../UnityClient/Assets/Resources/Content",sources={};
function collect(path){
  for(const entry of readdirSync(join(root,path),{withFileTypes:true})){
    const key=join(path,entry.name).replaceAll("\\","/");
    if(entry.isDirectory())collect(key);
    else if(entry.isFile()&&key.endsWith(".scm"))
      sources[key]=readFileSync(join(root,key),"utf8");
  }
}
collect("scripts");collect("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");

/** Extracts the original source expression, accounting for strings and comments. */
function balanced(source,start){
  let depth=0,quote=false,escape=false,comment=false;
  for(let i=start;i<source.length;i++){
    const ch=source[i];
    if(comment){if(ch==="\n")comment=false;continue;}
    if(quote){
      if(escape){escape=false;continue;}
      if(ch==="\\"){escape=true;continue;}
      if(ch==='"')quote=false;
      continue;
    }
    if(ch===';'){comment=true;continue;}
    if(ch==='"'){quote=true;continue;}
    if(ch==='(')depth++;
    if(ch===')'&&--depth===0)return source.slice(start,i+1);
  }
  throw Error("unterminated original authored stage expression");
}

test("real Frank ship-completion play-stage! executes original authored dialogue and exit beat",async()=>{
  const director=await GameSceneDirector.create(sources,std,()=>.42);
  director.state.takeStoryEvents(); // Ignore unrelated startup story.
  const frank=sources["scenes/world/人物/弗兰克.scm"];
  const start=frank.indexOf("(play-stage!");
  assert.ok(start>=0,"Frank ship settlement must keep its original stage scene");
  await director.world.evaluate(balanced(frank,start)); // Exactly the authored Scheme, unedited.
  const event=director.state.takeStoryEvents().find(x=>x.kind==="stage");
  assert.ok(event,"the native bridge must enqueue a stage event");
  const beats=parseStage(event.args);
  assert.ok(beats.length>=9);
  const player=new StagePlayback(beats);
  player.start();
  const spoken=[];
  for(let i=0;!player.complete&&i<100;i++){
    if(player.requiresInput){
      spoken.push([player.caption.speaker,player.caption.text]);
      const previous=player.beatIndex;
      player.advance();
      if(player.beatIndex===previous)player.advance(); // finish typewriter, then acknowledge
    }else player.tick(.35);
  }
  assert.ok(player.complete,"original stage must reach its authored ending");
  assert.ok(spoken.some(([who,text])=>who==="贝恩斯"&&text==="十分钟。"));
  assert.ok(spoken.some(([who,text])=>who==="尼尔"&&text==="今天算是。"));
  assert.equal(player.entities.has("贝恩斯"),false,"Baines exits the stage");
  assert.ok(player.entities.has("弗兰克"),"Frank remains at settlement");
});
