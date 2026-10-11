import compiledStdBase64 from "virtual:ssnoir-lips-stdlib";
import { base64Bytes } from "./base64";
import { GameScriptSession } from "./script-session";
import { GameRuntimeState } from "./game-state";
import { GameActionRunner } from "./action-runner";
import { GameSceneDirector } from "./scene-director";
import type { RuntimeNode } from "./node-converter";

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
  getPlaces():RuntimeNode[];
  enterPlace(name:string):Promise<RuntimeNode>;
  refresh():Promise<RuntimeNode>;
}
export async function createWorldHostRuntime(random:()=>number=Math.random):Promise<WorldHost> {
  const director=await GameSceneDirector.create(sources(),base64Bytes(compiledStdBase64),random);
  let root=await director.render();
  const host:WorldHost={
    director,
    state:director.state,
    session:director.world,
    get runner(){return director.actions;},
    get mode(){return director.mode;},
    get root(){return root;},
    getPlaces:()=>director.mode==="world"?root.children.filter(n=>n.isPlace&&!n.disabled):[],
    async enterPlace(name:string){
      if(director.mode!=="world")throw new Error("place entry only in the city");
      const node=await director.worldActions.enterPlace(name);
      await director.flush();
      root=await director.render();
      return node;
    },
    async refresh(){
      await director.flush();
      root=await director.render();
      return root;
    }
  };
  return host;
}
