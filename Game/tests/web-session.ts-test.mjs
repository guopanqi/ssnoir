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
      getItemCount:()=>15, listInventory:()=>[{id:"金钱",quantity:15}], getFailure:()=>null, hasRestBlockers:()=>false,
      takeStoryEvents:()=>[],
      snapshot:()=>({restBlockers:[],inventory:{"金钱":15}})
    },
    runner:{getCarryNodes:()=>[],performSlots:async(name,slots)=>{
      assert.equal(name,"work");assert.deepEqual(slots,[{type:"die",actorId:"player",slotId:0}]);
      return {outcome:"success"};
    }},
    getDossier:async()=>[{
      id:"活下去",kind:"主线",primary:true,status:"进行中",
      now:"挣到房租",where:"酒馆",clocks:[],
      steps:[{text:"认识工作",done:true},{text:"赚点钱",done:false},
        {text:"未来的事",done:false}],log:[]
    }],
    getPlaces:()=>[host.root.children[0]],
    takeDayAdvance:()=>null,
    leavePlace:async()=>{},
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
test("authored terminal failure is projected without exposing mutable runtime state",()=>{
  const host=fakeHost();
  const failure={title:"落幕",description:"所有退路都已经用完。"};
  host.state.getFailure=()=>({...failure});
  const game=new WebGameSession(host);
  assert.deepEqual(game.getSnapshot().failure,failure);
  game.getSnapshot().failure.title="被修改的副本";
  assert.deepEqual(host.state.getFailure(),failure,
    "UI cannot modify the authoritative terminal outcome");
});
test("seed controls an independent reproducible random stream",()=>{
  const a=seededRandom(42),b=seededRandom(42),c=seededRandom(43);
  assert.deepEqual(Array.from({length:10},()=>a()),Array.from({length:10},()=>b()));
  assert.notEqual(seededRandom(42)(),c());
  assert.throws(()=>seededRandom(-1),/seed/);
});
test("web session visits an authored place, spends selected die through runtime, and returns",async()=>{
  const session=new WebGameSession(fakeHost());
  assert.equal(session.getSnapshot().entries[0].title,"老街酒馆");
  assert.deepEqual(session.getSnapshot().inventory,[{id:"金钱",quantity:15}]);
  assert.deepEqual(session.getSnapshot().mapPlaces.map(x=>x.id),["酒馆"]);
  await session.open("酒馆");
  assert.deepEqual(session.getSnapshot().path,["酒馆"]);
  await session.open("work");
  assert.equal(session.getSnapshot().selectedAction,"work");
  await session.pickDie("player",0);
  assert.equal(session.getSnapshot().selectedAction,null);
  assert.match(session.getSnapshot().message,/打零工/);
  await session.back();
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

test("web navigation is a real exit, and shortcut returns home without advancing time",async()=>{
  const host=fakeHost();
  let exits=0,enters=0;
  const before=host.state.getGlobal("世界日");
  host.leavePlace=async()=>{exits++;};
  const original=host.enterPlace;
  host.enterPlace=async id=>{enters++;if(id==="酒馆")await original(id);else assert.equal(id,"家");};
  const game=new WebGameSession(host);
  await game.open("酒馆");
  await game.back();
  assert.equal(exits,1);
  assert.deepEqual(game.getSnapshot().path,[]);
  host.root.children.push({...host.root.children[0],name:"家",title:"旅馆",children:[]});
  host.getPlaces=()=>host.root.children.filter(x=>x.isPlace&&!x.disabled);
  await game.goHome();
  assert.equal(enters,2);
  assert.deepEqual(game.getSnapshot().path,["家"]);
  assert.equal(host.state.getGlobal("世界日"),before,"navigation is never a free end-day action");
});
test("an authored sleep action forwards the day transition and clears old path",async()=>{
  const host=fakeHost();
  let day=1;
  let advance=null;
  host.state.getGlobal=k=>k==="世界日"?day:false;
  host.runner.performSlots=async()=>({outcome:"success"});
  host.refresh=async()=>{day=2;advance={after:2,events:[]};return host.root;};
  host.takeDayAdvance=()=>{const value=advance;advance=null;return value;};
  const game=new WebGameSession(host);
  await game.open("酒馆");
  await game.open("work");
  await game.pickDie("player",0);
  assert.equal(game.getSnapshot().day,2);
  assert.deepEqual(game.getSnapshot().path,[]);
  assert.equal(game.getSnapshot().message,"第 2 日");
});
test("pending dice allocation cannot silently switch to another action",async()=>{
  const host=fakeHost();
  const game=new WebGameSession(host);
  await game.open("酒馆");
  await game.open("work");
  await game.open("work");
  assert.match(game.getSnapshot().error,/请完成当前骰子分配/);
  assert.equal(game.getSnapshot().selectedAction,"work");
});

test("dossier is read from the host on demand, not synthesized by the UI",async()=>{
  const host=fakeHost();
  const session=new WebGameSession(host);
  assert.equal(session.getSnapshot().dossierOpen,false);
  assert.equal(session.getSnapshot().dossiers.length,0);
  await session.toggleDossier();
  const opened=session.getSnapshot();
  assert.equal(opened.error,"");
  assert.equal(opened.dossierOpen,true);
  assert.deepEqual(opened.dossiers.map(x=>x.id),["活下去"]);
  assert.equal(opened.dossiers[0].steps[1].text,"赚点钱");
  await session.toggleDossier();
  assert.equal(session.getSnapshot().dossierOpen,false);
});
test("action result reports actual resource, composure, and clock effects in Chinese",async()=>{
  const host=fakeHost();
  const before={
    inventory:{"金钱":15},growthLevel:0,injury:{severity:0},
    team:{actors:[{id:"player",composure:5}]}
  };
  const after={
    inventory:{"金钱":17},growthLevel:0,injury:{severity:0},
    team:{actors:[{id:"player",composure:4}]}
  };
  host.runner.performSlots=async()=>({
    mode:"roll",outcome:"fail",before,after,
    clockEffects:[{label:"进度",displayLabel:"调查进度",delta:1}]
  });
  const session=new WebGameSession(host);
  await session.open("酒馆");
  await session.open("work");
  await session.pickDie("player",0);
  const report=session.getSnapshot().lastAction;
  assert.equal(report.title,"打零工");
  assert.equal(report.result,"失败");
  assert.deepEqual(report.changes,[
    {label:"冷静",delta:-1},{label:"金钱",delta:2},
    {label:"调查进度",delta:1},{label:"行动骰",delta:-1}
  ]);
  assert.equal(session.getSnapshot().message,"打零工 · 失败");
});


test("bubble recovery pays only on visible same-day banter, once; queued during dialogue waits",()=>{
  const host=fakeHost();
  let composure=2,paid=0;
  host.state.team.find=()=>({id:"player"});
  host.state.team.restoreComposure=(_actor,amount)=>{composure+=amount;paid++;};
  host.state.notify=()=>{};
  const wrapped=(id,day)=>["player","休息一下吧。","",0,[],"尼尔","",
    ["ssnoir-bubble-recovery","player",2,"冷静+2",day,id]];
  host.state.takeStoryEvents=()=>[
    {kind:"dialogue",blocking:true,immediate:true,args:[[["line","夜莺","先听我说。"]]]},
    {kind:"banter",blocking:false,immediate:true,args:[[wrapped(11,1)]]},
    {kind:"banter",blocking:false,immediate:true,args:[[wrapped(11,1)]]}
  ];
  const game=new WebGameSession(host);
  assert.equal(composure,2,"not paid while dialogue covers the bubble");
  game.continueStory();
  assert.equal(composure,4,"paid once the bubble becomes visible");
  assert.equal(paid,1);
  assert.match(game.getSnapshot().message,/冷静\+2/);
  game.continueStory();
  assert.equal(paid,1);
  const other=fakeHost();
  let stalePaid=0;
  other.state.team.find=()=>({id:"player"});
  other.state.team.restoreComposure=()=>{stalePaid++;};
  other.state.notify=()=>{};
  other.state.getGlobal=key=>key==="世界日"?2:false;
  other.state.takeStoryEvents=()=>[
    {kind:"banter",blocking:false,immediate:true,args:[[wrapped(12,1)]]}
  ];
  new WebGameSession(other);
  assert.equal(stalePaid,0,"next-day discarded banter never heals");
});


test("authored clock-node annotations remain visible as progress gauges",()=>{
  const host=fakeHost();
  const clock={label:"歌曲进程",displayLabel:"歌曲进程",current:2,max:5,
    style:"gauge",note:"每回合推进一段"};
  host.root.children.push({
    name:"钟：歌曲进程",title:"钟：歌曲进程",subtitle:"",
    isPlace:false,disabled:false,tags:[],requires:[],clocks:[],children:[],arrivals:[],
    resolve:{kind:"note",noteTitle:"歌曲进程",noteText:"",noteClock:clock}
  });
  const session=new WebGameSession(host);
  const view=session.getSnapshot().entries.find(x=>x.id==="钟：歌曲进程");
  assert.deepEqual(view.clocks,[clock]);
  assert.equal(view.action,"note");
});


test("scenario opponent round reports actual composure tax and resource consequences",async()=>{
  const host=fakeHost();
  let composure=5,money=15;
  host.mode="scenario";
  host.state.team.roster=()=>[{id:"player",name:"尼尔",status:"active",composure}];
  host.state.getItemCount=()=>money;
  host.state.snapshot=()=>({
    restBlockers:[],inventory:{"金钱":money},growthLevel:0,
    injury:{severity:0},team:{actors:[{id:"player",composure}]}
  });
  host.director.endRound=async()=>{
    composure-=1;money+=3;
    return [{phase:"opponent-rule",events:[]}];
  };
  const game=new WebGameSession(host);
  await game.endTurn();
  const state=game.getSnapshot();
  assert.equal(state.error,"");
  assert.equal(state.actors[0].composure,4);
  assert.equal(state.money,18);
  assert.equal(state.lastAction.title,"回合结算");
  assert.deepEqual(state.lastAction.changes,[
    {label:"冷静",delta:-1},{label:"金钱",delta:3}
  ]);
});
