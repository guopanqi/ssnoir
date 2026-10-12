import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
import {GameRuntimeState} from "../src/runtime/game-state.ts";
import {GameScriptSession} from "../src/runtime/script-session.ts";
import {GameActionRunner} from "../src/runtime/action-runner.ts";
import {ActionTurnState} from "../src/runtime/turn-state.ts";
const base="../UnityClient/Assets/Resources/Content",sources={};
function walk(p){for(const d of readdirSync(join(base,p),{withFileTypes:true})){
 const file=join(p,d.name).replaceAll("\\","/");
 if(d.isDirectory())walk(file);else if(file.endsWith(".scm"))sources[file]=readFileSync(join(base,file),"utf8");
}}
walk("scripts");walk("scenes");
const std=readFileSync("node_modules/lips/dist/std.xcb");
test("world place entry invokes authored on-enter-place rules without spending dice",async()=>{
 const state=new GameRuntimeState();
 const session=await GameScriptSession.create("world-navigation",state,sources,std);
 await session.loadFile("scenes/world/world.scm");
 const runner=await GameActionRunner.create(session,state,new ActionTurnState(()=>0.5));
 const root=await runner.prepare();
 const places=root.children.filter(c=>c.isPlace&&!c.disabled);
 assert.ok(places.length>0,"original content must expose at least one enabled place");
 const before=runner.turn.available();
 const entered=await runner.enterPlace(places[0].name);
 assert.equal(entered.name,places[0].name);
 assert.deepEqual(runner.turn.available(),before,"navigating should consume no dice");
});

test("ordinary place entry reuses the post-entry render tree without redundant global render",async()=>{
 const state=new GameRuntimeState();
 const session=await GameScriptSession.create("world-navigation-cache",state,sources,std);
 await session.loadFile("scenes/world/world.scm");
 const runner=await GameActionRunner.create(session,state,new ActionTurnState(()=>0.5));
 const root=await runner.prepare();
 const place=root.children.find(n=>n.isPlace&&!n.disabled&&n.arrivals.length===0);
 assert.ok(place,"original world must contain a place with no arrival hooks");
 let calls=0;
 const orig=runner.prepare.bind(runner);
 runner.prepare=async(...args)=>{calls++;return orig(...args);};
 const entry=await runner.enterPlace(place.name);
 assert.equal(entry.name,place.name);
 assert.equal(calls,1,"no-arrival entry must materialize world just once");
 assert.equal(runner.getPreparedRoot()?.children.find(n=>n.name===place.name)?.name,place.name);
});