import test from "node:test";
import assert from "node:assert/strict";
import { WebGameSession, seededRandom } from "../src/web/session.ts";

function fakeHost() {
  const place = {name:"酒馆",title:"老街酒馆",subtitle:"有人正在讲话",anchor:null,
    isPlace:true,disabled:false,tags:[],requires:[],clocks:[],children:[],arrivals:[],resolve:null};
  const action = {name:"work",title:"打零工",subtitle:"赚一点钱",anchor:null,
    isPlace:false,disabled:false,tags:[],requires:[{type:"die"}],clocks:[],children:[],arrivals:[],
    resolve:{kind:"roll",skill:"social"}};
  const host = {
    mode:"world",
    root:{...place,name:"世界",title:"城市",isPlace:false,children:[place]},
    state:{
      injury:{severity:0},
      team:{
        roster:()=>[{id:"player",name:"尼尔",status:"active",composure:5}],
        dice:()=>[{slotId:0,value:4}]
      },
      getGlobal:key=>key==="世界日"?1:false,
      getItemCount:()=>15, hasRestBlockers:()=>false,
      takeStoryEvents:()=>[]
    },
    runner:{getCarryNodes:()=>[],performSlots:async(name,slots)=>{
      assert.equal(name,"work");assert.deepEqual(slots,[{type:"die",actorId:"player",slotId:0}]);
      return {outcome:"success"};
    }},
    enterPlace:async name=>{
      assert.equal(name,"酒馆");
      host.root={...host.root,children:[{...place,children:[action]}]};
    },
    refresh:async()=>host.root,
    endDay:async()=>({after:2,events:[]}),
    save:async()=> "save",
    load:async()=>{},
    director:{endRound:async()=>[]}
  };
  return host;
}
test("seed controls an independent reproducible random stream",()=>{
  const a=seededRandom(42),b=seededRandom(42),c=seededRandom(43);
  assert.deepEqual(Array.from({length:10},()=>a()),Array.from({length:10},()=>b()));
  assert.notEqual(seededRandom(42)(),c());
  assert.throws(()=>seededRandom(-1),/seed/);
});
test("web session visits an authored place, spends selected die through runtime, and returns",async()=>{
  const session=new WebGameSession(fakeHost());
  assert.equal(session.getSnapshot().entries[0].title,"老街酒馆");
  await session.open("酒馆");
  assert.deepEqual(session.getSnapshot().path,["酒馆"]);
  await session.open("work");
  assert.equal(session.getSnapshot().selectedAction,"work");
  await session.pickDie("player",0);
  assert.equal(session.getSnapshot().selectedAction,null);
  assert.match(session.getSnapshot().message,/打零工/);
  session.back();
  assert.deepEqual(session.getSnapshot().path,[]);
});
test("view-only note does not consume dice or mutate the runtime",async()=>{
  const host=fakeHost();
  host.root.children.push({
    name:"note",title:"一则便条",subtitle:"",isPlace:false,disabled:false,
    tags:[],requires:[],clocks:[],children:[],arrivals:[],resolve:{kind:"observe",text:"线索"}
  });
  const session=new WebGameSession(host);
  await session.open("note");
  assert.equal(session.getSnapshot().message,"线索");
  assert.equal(session.getSnapshot().dice.length,1);
});
