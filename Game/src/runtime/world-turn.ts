import {GameRuntimeState} from "./game-state.ts";
import {GameScriptSession} from "./script-session.ts";

export interface DayAdvance {
  before:number;after:number;
  returnedCompanions:string[];
  events:ReturnType<GameRuntimeState["takeStoryEvents"]>;
  dice:ReturnType<GameRuntimeState["team"]["diceSnapshot"]>;
}
/**
 * C# SceneManager.EndTurn, world-only. The authored on-turn-end drives the
 * calendar and all module transitions. Never increment day in TypeScript.
 * Follow with actor regeneration, then the original on-turn-start.
 */
export class WorldTurnController {
  private inTransition=false;
  readonly state:GameRuntimeState;
  readonly world:GameScriptSession;
  constructor(state:GameRuntimeState,world:GameScriptSession) {this.state=state;this.world=world;}
  async endDay(random:()=>number=Math.random):Promise<DayAdvance>{
    if(this.inTransition)throw new Error("world turn already transitioning");
    if(this.state.getGlobal("location")!=="world")throw new Error("world turn cannot end inside scenario");
    if(this.state.hasRestBlockers())throw new Error("unfinished mandatory rest blockers");
    if(this.state.hasPendingHospitalization())throw new Error("hospitalization must be settled first");
    const before=this.state.getGlobal("世界日");
    if(typeof before!=="number"||!Number.isSafeInteger(before)||before<1)throw new Error("invalid world day");
    this.inTransition=true;
    const stateBefore=this.state.snapshot();
    this.state.beginActionReport();
    try{
      await this.world.evaluate("(on-turn-end)");
      const after=this.state.getGlobal("世界日");
      if(typeof after!=="number"||after!==before+1)throw new Error("authored calendar rule must advance exactly one day");
      this.state.team.dismissTemporary();
      const returned=this.state.team.beginCityDay();
      this.state.team.rollDice(random,false,true,this.state.injury.actionSlotLoss);
      this.state.takeActionReport();
      this.state.beginActionReport();
      await this.world.evaluate("(on-turn-start)");
      this.state.takeActionReport();
      return {before,after,returnedCompanions:returned,
        events:this.state.takeStoryEvents(),dice:this.state.team.diceSnapshot()};
    }catch(error){
      this.state.cancelActionReport();
      this.state.restore(stateBefore);
      throw error;
    }finally{this.inTransition=false;}
  }
}
