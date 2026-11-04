import compiledStdBase64 from "virtual:ssnoir-lips-stdlib";
import { base64Bytes } from "./base64";
import { GameScriptSession } from "./script-session";
import { GameRuntimeState } from "./game-state";
import { GameActionRunner } from "./action-runner";
import { GameSceneDirector } from "./scene-director";
import { WorldSaveController } from "./world-save";
import { WorldTurnController, type DayAdvance } from "./world-turn";
import type { RuntimeNode } from "./node-converter";
import {convertDossier,type DossierEntry} from "./dossier-converter";

// Vite resolves these imports at BUILD time, not at Mini Game runtime.
// No second editable copy of authored content; this is the exact Unity content tree.
const originalContent=import.meta.glob(
  "../../../UnityClient/Assets/Resources/Content/{scripts,scenes}/**/*.scm",
  {query:"?raw",import:"default",eager:true}
) as Record<string,string>;
const MARK="/Content/";
function sources():Record<string,string> {
  const result:Record<string,string>={};
  for(const [path,text] of Object.entries(originalContent)){
    const i=path.indexOf(MARK);
    if(i<0)throw new Error("Malformed source path "+path);
    const key=path.slice(i+MARK.length);
    if(Object.hasOwn(result,key))throw new Error("Duplicate authored script "+key);
    result[key]=text;
  }
  for(const required of ["scripts/stdlib.scm","scripts/engine.scm","scripts/theatre.scm","scenes/world/world.scm"])
    if(!Object.hasOwn(result,required))throw new Error("Missing bundled original "+required);
  return result;
}

export interface WorldHost {
  state:GameRuntimeState;
  session:GameScriptSession;
  runner:GameActionRunner;
  director:GameSceneDirector;
  mode:"world"|"scenario";
  root:RuntimeNode;
  lastNavigation:{durationMs:number;materializations:number;modeChanged:boolean}|null;
  getPlaces():RuntimeNode[];
  getDossier():Promise<DossierEntry[]>;
  enterPlace(name:string):Promise<RuntimeNode>;
  leavePlace():Promise<void>;
  refresh():Promise<RuntimeNode>;
  takeDayAdvance():DayAdvance|null;
  endDay():Promise<DayAdvance>;
  save():Promise<string>;
  load(text:string):Promise<void>;
}
export async function createWorldHostRuntime(random:()=>number=Math.random):Promise<WorldHost> {
  const bootstrapAt=performance.now();
  const authored=sources(),stdlib=base64Bytes(compiledStdBase64);
  let director=await GameSceneDirector.create(authored,stdlib,random);
  // GameSceneDirector.create already materializes the initial world tree.
  let root=director.getPreparedRoot()??await director.render();
  console.info("[SSNoir] web world bootstrap",Math.round(performance.now()-bootstrapAt),"ms");
  let lastNavigation:WorldHost["lastNavigation"]=null;
  let lastDayAdvance:DayAdvance|null=null;
  const host:WorldHost={
    get director(){return director;},
    get state(){return director.state;},
    get session(){return director.world;},
    get runner(){return director.actions;},
    get mode(){return director.mode;},
    get root(){return root;},
    get lastNavigation(){return lastNavigation;},
    getPlaces:()=>director.mode==="world"?root.children.filter(n=>n.isPlace&&!n.disabled):[],
    async getDossier(){
      if(director.mode!=="world")throw new Error("city dossier unavailable in scenario");
      return convertDossier(await director.world.evaluate("(get-dossier)"));
    },
    async enterPlace(name:string){
      if(director.mode!=="world")throw new Error("place entry only in the city");
      const started=Date.now(),beforeMode=director.mode;
      const originalPrepare=director.worldActions.prepare.bind(director.worldActions);
      let materializations=0;
      // Measure actual get-render-data invocations, not abstract API calls.
      // This is scoped to the serialized world UI queue, and always restored.
      director.worldActions.prepare=async (...args)=>{
        materializations++;
        return originalPrepare(...args);
      };
      try{
        const node=await director.worldActions.enterPlace(name);
        // An arrival may open a scenario; only that mode transition needs
        // a different interpreter's render tree after flush().
        await director.flush();
        const modeChanged=director.mode!==beforeMode;
        root=modeChanged?(director.getPreparedRoot()??await director.render()):
          (director.worldActions.getPreparedRoot()??await director.render());
        lastNavigation={durationMs:Date.now()-started,materializations,modeChanged};
        console.log("[SSNoir] enter place",name,JSON.stringify(lastNavigation));
        return node;
      }finally{
        director.worldActions.prepare=originalPrepare;
      }
    },
    async leavePlace(){
      if(director.mode!=="world")throw new Error("leave place requires city mode");
      await director.world.evaluate('(on-enter-place "世界")');
      await director.flush();
      root=await director.render();
    },
    takeDayAdvance(){const advance=lastDayAdvance;lastDayAdvance=null;return advance;},
    async refresh(){
      if(director.consumeWorldTurnRequest())
        lastDayAdvance=await new WorldTurnController(director.state,director.world).endDay(random);
      await director.flush();
      root=await director.render();
      return root;
    },
    async endDay(){
      lastDayAdvance=null;
      if(director.mode!=="world")throw new Error("cannot end the city day during a scenario");
      const result=await new WorldTurnController(director.state,director.world).endDay(random);
      await director.flush();
      root=await director.render();
      return result;
    },
    async save(){
      if(director.mode!=="world")throw new Error("cannot save inside a scenario");
      const data=await new WorldSaveController(director.state,director.world).capture();
      return WorldSaveController.encode(data);
    },
    async load(text:string){
      // Do not swap the active world until the fresh interpreter and every
      // authored module have restored without an exception.
      const t0=performance.now();
      const snapshot=WorldSaveController.parse(text);
      const t1=performance.now();
      const candidate=await GameSceneDirector.create(authored,stdlib,random,false);
      const t2=performance.now();
      await WorldSaveController.restore(snapshot,async()=>({
        state:candidate.state,session:candidate.world
      }));
      const t3=performance.now();
      const restoredRoot=await candidate.render();
      const t4=performance.now();
      console.info("[SSNoir] web restore stages",JSON.stringify({
        parseMs:Math.round(t1-t0),
        bootstrapMs:Math.round(t2-t1),
        worldLoadMs:Math.round(t3-t2),
        rematerializeMs:Math.round(t4-t3),
        totalMs:Math.round(t4-t0)
      }));
      director=candidate;
      root=restoredRoot;
      lastDayAdvance=null;
    }
  };
  return host;
}
