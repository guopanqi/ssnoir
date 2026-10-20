import { GameRuntimeState, type RuntimeSnapshot } from "./game-state.ts";
import { GameScriptSession } from "./script-session.ts";

/** Portable save envelope: no JS/DOM objects and no live Scheme procedures. */
export interface WorldSaveV1 {
  format:"ssnoir-world";version:1; state:RuntimeSnapshot;
  worldDatum:string;settings:Record<string,string|number|boolean>;
}
const FORMAT="ssnoir-world";
const MAX_SAVE_CHARS=4_000_000;
function settingsCopy(value:Record<string,string|number|boolean>):Record<string,string|number|boolean> {
  if(!value||typeof value!=="object"||Array.isArray(value))throw new Error("invalid settings object");
  const copy:Record<string,string|number|boolean>={};
  for(const [name,v] of Object.entries(value)){
    if(!name.trim()||name==="__proto__"||name==="constructor"||name==="prototype"||
       !(typeof v==="boolean"||typeof v==="string"||
         (typeof v==="number"&&Number.isFinite(v)))) throw new Error("invalid setting "+name);
    Object.defineProperty(copy,name,{enumerable:true,writable:true,configurable:true,value:v});
  }
  return copy;
}
/**
 * Original world-save/world-load! own per-module state, in precisely the
 * authored order. Scheme write/read exchange a datum (not executable source).
 * Load into a NEW GameRuntimeState and interpreter; never reconstruct
 * previous VM closures from a snapshot.
 */
export class WorldSaveController {
  readonly state:GameRuntimeState;
  readonly session:GameScriptSession;
  constructor(state:GameRuntimeState,session:GameScriptSession){this.state=state;this.session=session;}

  async capture(settings:Record<string,string|number|boolean>={}):Promise<WorldSaveV1>{
    if(this.state.getGlobal("location")!=="world")throw new Error("cannot save during an encounter");
    if(this.state.hasPendingHospitalization())throw new Error("cannot save during hospitalization");
    const world=await this.session.evaluate(
      '(let ((port (open-output-string))) (write (world-save) port) (get-output-string port))'
    );
    const worldDatum=String(world);
    if(!worldDatum||worldDatum.length>MAX_SAVE_CHARS)throw new Error("invalid world state length");
    // A saved world must never contain encounter-only temporary helpers.
    this.state.team.snapshot();
    return {format:FORMAT,version:1,state:this.state.snapshot(),
      worldDatum,settings:settingsCopy(settings)};
  }

  static parse(text:string):WorldSaveV1 {
    if(typeof text!=="string"||text.length>MAX_SAVE_CHARS)throw new Error("oversize save file");
    const data:unknown=JSON.parse(text);
    if(!data||typeof data!=="object"||Array.isArray(data))throw new Error("invalid save");
    const value=data as Partial<WorldSaveV1>;
    if(value.format!==FORMAT||value.version!==1)throw new Error("unsupported save version");
    if(typeof value.worldDatum!=="string"||value.worldDatum.length===0||
       value.worldDatum.length>MAX_SAVE_CHARS)throw new Error("invalid Scheme save datum");
    settingsCopy(value.settings!);
    if(!value.state||typeof value.state!=="object")throw new Error("invalid native save");
    // Native structure is validated transactionally in restore.
    return value as WorldSaveV1;
  }

  static encode(data:WorldSaveV1):string {return JSON.stringify(data);}

  /**
   * The caller supplies a fresh, fully initialized world VM. This is the same
   * C# order: load world module once, restore typed state, invoke world-load!
   * once. Failed restoration must discard this new VM and keep the prior game.
   */
  static async restore(
    data:WorldSaveV1,makeWorld:()=>Promise<{state:GameRuntimeState;session:GameScriptSession}>
  ):Promise<{state:GameRuntimeState;session:GameScriptSession;settings:Record<string,string|number|boolean>}> {
    if(data.format!==FORMAT||data.version!==1)throw new Error("unsupported world save");
    const settings=settingsCopy(data.settings);
    const fresh=await makeWorld();
    // Native save restores use stricter C# validation than internal action
    // rollback snapshots (which may legitimately include temporary helpers).
    const candidate=fresh.state.team;
    candidate.restore(data.state.team);
    fresh.state.restore(data.state);
    fresh.state.setGlobal("location","world");
    // JSON quoted Scheme strings are safe string literals. `read` returns
    // the saved datum and never evaluates a procedure or authored game action.
    const literal=JSON.stringify(data.worldDatum);
    const nativeDay=data.state.globals?.["世界日"];
    if(typeof nativeDay!=="number"||!Number.isSafeInteger(nativeDay)||nativeDay<1)
      throw new Error("save calendar mismatch: invalid native day");
    // The authored world-load! deliberately writes '世界日 from its Scheme
    // datum. Compare BOTH saved copies before invoking that mutation, or an
    // inconsistent native date is silently overwritten and cannot be detected.
    // Read the datum only once: full content save data can be large.
    await fresh.session.evaluate(
      '(let ((datum (read (open-input-string '+literal+')))) '+
      '(if (and (number? (assoc-get datum "day" #f)) '+
      '(= (assoc-get datum "day" #f) '+nativeDay+')) '+
      '(world-load! datum) (error "save calendar mismatch: authored day differs")))'
    );
    // world-load! owns the authored calendar, while the typed save also
    // carries its public global. Reject divergence instead of silently
    // accepting a world whose daily deadlines disagree with the native UI.
    const nativeDay=fresh.state.getGlobal("世界日");
    const authoredDay=await fresh.session.evaluateNumber("world-day");
    if(typeof nativeDay!=="number"||!Number.isSafeInteger(nativeDay)||nativeDay<1||
       !Number.isSafeInteger(authoredDay)||authoredDay!==nativeDay){
      throw new Error("save calendar mismatch between authored world and native state");
    }
    return {...fresh,settings};
  }
}
