import compiledStdBase64 from "virtual:ssnoir-lips-stdlib";
import { base64Bytes } from "./base64";
import { GameScriptSession } from "./script-session";
import { GameRuntimeState } from "./game-state";
import { ActionTurnState } from "./turn-state";
import { GameActionRunner } from "./action-runner";
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
  root:RuntimeNode;
  getPlaces():RuntimeNode[];
  enterPlace(name:string):Promise<RuntimeNode>;
  refresh():Promise<RuntimeNode>;
}
export async function createWorldHostRuntime(random:()=>number=Math.random):Promise<WorldHost> {
  const state=new GameRuntimeState();
  const session=await GameScriptSession.create(
    "ssnoir-authored-world",state,sources(),base64Bytes(compiledStdBase64)
  );
  await session.loadFile("scenes/world/world.scm");
  const runner=await GameActionRunner.create(session,state,new ActionTurnState(random));
  let root=await runner.prepare();
  return {
    state,session,runner,
    get root(){return root;},
    getPlaces:()=>root.children.filter(n=>n.isPlace&&!n.disabled),
    async enterPlace(name:string){const node=await runner.enterPlace(name);root=await runner.prepare();return node;},
    async refresh(){root=await runner.prepare();return root;}
  };
}
