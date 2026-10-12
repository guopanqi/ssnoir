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
  mode:"world"|"encounter";
  root:RuntimeNode;
  lastNavigation:{durationMs:number;materializations:number;modeChanged:boolean}|null;
  getPlaces():RuntimeNode[];
  getDossier():Promise<DossierEntry[]>;
  enterPlace(name:string):Promise<RuntimeNode>;
  refresh():Promise<RuntimeNode>;
  endDay():Promise<DayAdvance>;
  save():Promise<string>;
  load(text:string):Promise<void>;
}
export async function createWorldHostRuntime(random:()=>number=Math.random):Promise<WorldHost> {
  const authored=sources(),stdlib=base64Bytes(compiledStdBase64);
  let director=await GameSceneDirector.create(authored,stdlib,random);
  // GameSceneDirector.create already materializes the initial world tree.
  let root=director.getPreparedRoot()??await director.render();
  let lastNavigation:WorldHost["lastNavigation"]=null;
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
      if(director.mode!=="world")throw new Error("city dossier unavailable in encounter");
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
        // An arrival may open an encounter; only that mode transition needs
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
    async refresh(){
      await director.flush();
      root=await director.render();
      return root;
    },
    async endDay(){
      if(director.mode!=="world")throw new Error("cannot end the city day during an encounter");
      const result=await new WorldTurnController(director.state,director.world).endDay(random);
      await director.flush();
      root=await director.render();
      return result;
    },
    async save(){
      if(director.mode!=="world")throw new Error("cannot save inside an encounter");
      const data=await new WorldSaveController(director.state,director.world).capture();
      return WorldSaveController.encode(data);
    },
    async load(text:string){
      // Do not swap the active world until the fresh interpreter and every
      // authored module have restored without an exception.
      const snapshot=WorldSaveController.parse(text);
      const candidate=await GameSceneDirector.create(authored,stdlib,random);
      await WorldSaveController.restore(snapshot,async()=>({
        state:candidate.state,session:candidate.world
      }));
      const restoredRoot=await candidate.render();
      director=candidate;
      root=restoredRoot;
    }
  };
  return host;
}
