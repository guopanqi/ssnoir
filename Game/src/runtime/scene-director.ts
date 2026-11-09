import {GameRuntimeState} from "./game-state.ts";
import {GameScriptSession,type ScriptSources} from "./script-session.ts";
import {GameActionRunner} from "./action-runner.ts";
import {ActionTurnState} from "./turn-state.ts";
import {schemeList,type RuntimeNode} from "./node-converter.ts";
import {storyArguments} from "./story-events.ts";
import {WorldTurnController,type DayAdvance} from "./world-turn.ts";

/**
 * One state, two isolated Scheme interpreters; original C# SceneManager
 * start/end-scenario contract. Scheme scripts use the matching start/end-scenario contract.
 * Transitions are applied AFTER a Scheme effect finishes (not nested inside
 * Interpreter.exec), so world callbacks retain their original Scheme closure.
 */
export class GameSceneDirector {
  readonly state:GameRuntimeState;
  readonly world:GameScriptSession;
  readonly worldActions:GameActionRunner;
  scenario:GameScriptSession|null=null;
  scenarioActions:GameActionRunner|null=null;
  private readonly sources:ScriptSources;
  private readonly random:()=>number;
  private pendingStart:{name:string;callback:unknown}|null=null;
  private ended=false;
  private endValue:unknown=false;
  private callback:unknown=false;
  private worldDice:ReturnType<GameRuntimeState["team"]["diceSnapshot"]>|null=null;
  supportUsed=false;
  private pendingEndTurn=false;
  private refreshRequested=false;
  private roundActive=false;
  private autoActions:Array<{
    name:string;title:string;text:string;demands:{actorId:string;count:number}[];
    prelude:unknown;effect:unknown
  }>=[];
  private autoIndex=0;

  private constructor(state:GameRuntimeState,world:GameScriptSession,
    actions:GameActionRunner,sources:ScriptSources,random:()=>number){
    this.state=state;this.world=world;this.worldActions=actions;
    this.sources=sources;this.random=random;
  }

  static async create(sources:ScriptSources,
    random:()=>number=Math.random,
    prepareInitialRoot=true):Promise<GameSceneDirector> {
    const state=new GameRuntimeState();
    let self:GameSceneDirector|undefined;
    const world=await GameScriptSession.create("ssnoir-world-director",state,sources,{
      "start-scenario":(...args)=>{
        if(!self)throw new Error("scene director uninitialized");
        if(args.length<1)throw new Error("start-scenario requires name");
        if(self.scenario||self.pendingStart)throw new Error("scenario already active/pending");
        const name=String(args[0]);
        self.pendingStart={name,callback:args[1]??false};
        return undefined;
      },
      "__end-turn!":()=>{
        if(!self)throw new Error("scene director uninitialized");
        self.pendingEndTurn=true;return undefined;
      },
      "__ssnoir-callback":()=>self?.callback??false,
      "__ssnoir-scenario-result":()=>self?.endValue??false
    },random);
    await world.loadFile("scenes/world/world.scm");
    const actions=await GameActionRunner.create(world,state,new ActionTurnState(random));
    self=new GameSceneDirector(state,world,actions,sources,random);
    // A save restore supplies its own native dice and immediately rebuilds the
    // authored tree after world-load!. Avoid a redundant initial materialization.
    if(prepareInitialRoot){
      state.team.rollDice(random,false,true,state.injury.actionSlotLoss);
      await actions.prepare();
    }
    return self;
  }

  /**
   * Owns the complete temporary Scenario lifecycle. A failed entrance must
   * return the director to the same idle state as a fresh world: resetting
   * scenario references alone leaves queued end-turn/end-scenario signals
   * that execute on the next world flush.
   */
  private clearScenarioLifecycle():void {
    // pendingStart belongs to the world transition queue, not the closing
    // scenario: its result callback may legitimately request another scenario.
    this.scenario=null;
    this.scenarioActions=null;
    this.worldDice=null;
    this.callback=false;
    this.ended=false;
    this.endValue=false;
    this.supportUsed=false;
    this.pendingEndTurn=false;
    this.refreshRequested=false;
    this.roundActive=false;
    this.autoActions=[];
    this.autoIndex=0;
  }

  get mode():"world"|"scenario"{return this.scenario?"scenario":"world";}
  get actions():GameActionRunner {return this.scenarioActions??this.worldActions;}
  getPreparedRoot():RuntimeNode|null{return this.actions.getPreparedRoot();}
  async render():Promise<RuntimeNode>{return this.actions.prepare();}
  /** Take the world's Scheme rest request only after its action has returned. */
  consumeWorldTurnRequest():boolean {
    if(this.mode!=="world"||!this.pendingEndTurn)return false;
    this.pendingEndTurn=false;
    return true;
  }
  requestStart(name:string,callback:unknown=false):void {
    if(this.scenario||this.pendingStart)throw new Error("already in scenario");
    this.pendingStart={name,callback};
  }
  /** Integrate pending start-scenario bridge AFTER caller action ends. */
  async flush():Promise<void> {
    if(this.pendingStart){
      const next=this.pendingStart;this.pendingStart=null;
      await this.start(next.name,next.callback);return;
    }
    if(this.ended){
      this.ended=false;await this.finish(this.endValue);return;
    }
    if(this.refreshRequested){
      this.refreshRequested=false;
      if(!this.scenario)throw new Error("scenario dice refresh requested outside scenario");
      this.state.team.rollDice(this.random,true,false,this.state.injury.actionSlotLoss);
    }
    if(this.pendingEndTurn){
      this.pendingEndTurn=false;
      if(this.scenario)await this.endRound();
      else throw new Error("world end-turn requires WorldTurnController");
    }
  }

  async start(name:string,callback:unknown=false):Promise<RuntimeNode>{
    if(this.scenario)throw new Error("cannot nest scenarios");
    if(typeof name!=="string"||!name.trim()||
       name.includes("/")||name.includes("\\")||name.includes(".."))
      throw new Error("invalid scenario name");
    const sourceName="scenes/scenarios/"+name+".scm";
    if(!Object.hasOwn(this.sources,sourceName))
      throw new Error("authored scenario does not exist: "+name);
    const previous=this.state.snapshot();
    this.worldDice=this.state.team.diceSnapshot();
    this.callback=callback;
    try{
      this.state.setGlobal("location","scenario");
      this.supportUsed=false;
      this.ended=false;
      const scenario=await GameScriptSession.create(
        "ssnoir-scenario-"+name,this.state,this.sources,{
        "end-scenario":(...args)=>{
          if(!this.scenario||this.ended)throw new Error("invalid end-scenario");
          if(this.state.hasPendingHospitalization())throw new Error("cannot end while collapsed");
          this.ended=true;this.endValue=args[0]??false;return undefined;
        },
        "__support-used?":()=>this.supportUsed,
        "__refresh-scenario-dice!":()=>{
          this.refreshRequested=true;return undefined;
        },
        "__end-turn!":()=>{
          this.pendingEndTurn=true;return undefined;
        },
        "__summon-helper!":(...args)=>{
          if(args.length!==3)throw new Error("summon-helper expects actor, name, stats");
          const stats:Record<string,number>={};
          for(const entry of schemeList(args[2],"summoned helper stats")){
            const pair=schemeList(entry,"stat entry");
            if(pair.length!==2)throw new Error("invalid helper stat");
            const value=Number(String(pair[1]));
            if(!Number.isSafeInteger(value))throw new Error("invalid helper stat value");
            stats[String(pair[0])]=value;
          }
          const stash=this.state.team.diceSnapshot();
          const id=String(args[0]);
          this.state.team.recruit(id,String(args[1]),stats as import("./team-state.ts").Stats,true);
          this.state.team.rollDice(this.random,true,false,this.state.injury.actionSlotLoss);
          const rolled=this.state.team.diceSnapshot();
          this.state.team.restoreDice({...stash,[id]:rolled[id]});
          return undefined;
        },
        "__auto-action!":(...args)=>{
          if(!this.roundActive||args.length!==7)
            throw new Error("auto-action must occur during opponent rule resolution");
          const demands=schemeList(args[3],"auto action demands").map(row=>{
            const pair=schemeList(row,"auto demand");
            if(pair.length!==2)throw new Error("invalid auto action demand");
            const count=Number(String(pair[1]));
            if(!Number.isSafeInteger(count)||count<=0)throw new Error("invalid auto demand count");
            return {actorId:String(pair[0]),count};
          });
          if(!demands.length)throw new Error("empty forced action demands");
          this.autoActions.push({name:String(args[0]),title:String(args[6]),
            text:String(args[1]),demands,prelude:args[4],effect:args[5]});
          return undefined;
        },
        "__ssnoir-auto-effect":(...args)=>this.autoActions[Number(String(args[0]))]?.effect??false,
        "__ssnoir-mode":()=>this.mode
      },this.random);
      await scenario.loadFile(sourceName);
      const runner=await GameActionRunner.create(scenario,this.state,new ActionTurnState(this.random),{
        isSupportUsed:()=>this.supportUsed,
        setSupportUsed:(used)=>{this.supportUsed=used;},
        getCarriedSupport:()=>this.state.team.getCarriedSupport()
      });
      this.scenario=scenario;this.scenarioActions=runner;
      this.state.team.rollDice(this.random,true,false,this.state.injury.actionSlotLoss);
      this.state.beginActionReport();
      try{await scenario.evaluate("(on-scenario-enter)");this.state.takeActionReport();}
      catch(error){this.state.cancelActionReport();throw error;}
      return runner.prepare();
    }catch(error){
      this.state.restore(previous);
      this.clearScenarioLifecycle();
      throw error;
    }
  }

  /**
   * C# round transition sequence: time tax -> opponent rules in authored
   * registration order -> maintenance -> new dice -> automatic responses.
   * Each opponent rule commits separately; the scene is re-read after it.
   */
  async endRound():Promise<Array<{phase:string;events:ReturnType<GameRuntimeState["takeStoryEvents"]>}>>{
    if(!this.scenario||!this.scenarioActions)throw new Error("scenario required");
    if(this.roundActive)throw new Error("nested round transition");
    // C# SceneManager.BeginRoundTransition does not consult city rest
    // blockers. Chapter duties prevent ending a WORLD day, not taking an
    // scenario turn while those duties are still outstanding.
    this.autoActions=[];this.autoIndex=0;
    const frames:Array<{phase:string;events:ReturnType<GameRuntimeState["takeStoryEvents"]>}>=[];
    const capture=(phase:string)=>frames.push({phase,events:this.state.takeStoryEvents()});
    this.roundActive=true;
    try{
      this.state.beginActionReport();
      try{
        const rawCost=await this.scenario.evaluate("(scenario-time-cost)");
        const timeCost=Number(String(rawCost));
        if(!Number.isSafeInteger(timeCost)||timeCost<0||timeCost>10)
          throw new Error("invalid authored scenario-time-cost");
        const overflow=this.state.team.spend("player",timeCost);
        if(overflow>0)this.state.applyInjury(overflow);
        this.state.takeActionReport();capture("time-tax");
      }catch(error){this.state.cancelActionReport();throw error;}
      if(this.state.hasPendingHospitalization())return frames;
      await this.scenario.evaluate("(__begin-opponent-rules!)");
      for(let step=0;step<1024;step++){
        if(this.ended||!this.scenario)break;
        const pending=await this.scenario.evaluate("(__opponent-rules-pending?)");
        if(pending===false||String(pending)==="#f")break;
        this.state.beginActionReport();
        try{
          await this.scenario.evaluate("(__run-next-opponent-rule!)");
          this.state.takeActionReport();capture("opponent-rule");
        }catch(error){this.state.cancelActionReport();throw error;}
        if(this.state.hasPendingHospitalization())return frames;
        await this.scenarioActions.prepare();
        if(step===1023)throw new Error("opponent rules exceed 1024");
      }
      if(this.ended){await this.flush();capture("scenario-ended");return frames;}
      this.state.team.dismissTemporary();
      this.state.team.rollDice(this.random,true,false,this.state.injury.actionSlotLoss);
      capture("new-dice");
      for(let i=0;i<this.autoActions.length;i++){
        const auto=this.autoActions[i];
        for(const demand of auto.demands){
          const actor=this.state.team.require(demand.actorId);
          if(demand.count>actor.actionSlotCount)throw new Error("auto-action asks for too many slots");
          const available=this.state.team.dice(demand.actorId);
          for(const die of available.slice(0,demand.count))
            this.state.team.spendDie(demand.actorId,die.slotId);
        }
        const prelude=storyArguments([auto.prelude]);
        if(prelude[0]&&Array.isArray(prelude[0])&&(prelude[0] as unknown[]).length)
          this.state.queueStory({kind:"dialogue",blocking:true,args:prelude});
        this.state.beginActionReport();
        try{
          await this.scenario.evaluate("((__ssnoir-auto-effect "+i+"))");
          this.state.takeActionReport();
          capture("forced-action");
        }catch(error){this.state.cancelActionReport();throw error;}
        if(this.state.hasPendingHospitalization())return frames;
        if(this.ended){await this.flush();break;}
      }
      if(this.scenarioActions&&this.scenario)await this.scenarioActions.prepare();
      return frames;
    }finally{
      this.roundActive=false;this.autoActions=[];this.autoIndex=0;
    }
  }

  /**
   * A collapsed player cannot be left inside a Scene with a pending hospital
   * event: that state would reject every subsequent action and end-of-round.
   * Honor the authored collapse-result/retry policy, cancel stored city dice,
   * and force the normal Scheme calendar to the next day. This path is never
   * available as a UI command to skip ordinary rest obligations.
   */
  async resolveHospitalization():Promise<DayAdvance|null>{
    if(!this.state.hasPendingHospitalization())return null;
    const earlier=this.state.takeStoryEvents();
    if(this.scenario){
      const policy=schemeList(await this.scenario.evaluate("(on-scenario-collapse)"),
        "scenario collapse policy");
      const kind=String(policy[0]??"");
      if(kind==="collapse-result"&&policy.length===2){
        await this.finish(policy[1],false);
      }else if(kind==="collapse-retry"&&policy.length===1){
        this.state.team.dismissTemporary();
        this.state.setGlobal("location","world");
        this.clearScenarioLifecycle();
        await this.worldActions.prepare();
      }else throw new Error("invalid authored scenario collapse policy");
      if(this.scenario||this.pendingStart)
        throw new Error("hospitalization must return to the city without starting another scenario");
    }
    const callbackStory=this.state.takeStoryEvents();
    const hospital=this.state.consumeHospitalization();
    // The collapse happens before the scene result callback in the story,
    // then the new day and clinic bill are shown before that callback's talk.
    const heading={kind:"spotlight" as const,blocking:true,immediate:false,
      args:["你倒下了","身体再也撑不住了。眼前的街灯和人影渐渐模糊，你失去了意识。"]};
    const waking={kind:"spotlight" as const,blocking:true,immediate:false,
      args:["在诊所醒来","已经是第二天了。伤口经过处理，身体恢复了，但这次倒下留下了疤痕。\n"+hospital.message]};
    const advance=await new WorldTurnController(this.state,this.world).endDay(this.random,true);
    return {...advance,events:[...earlier,heading,waking,...callbackStory,...advance.events]};
  }

  requestEnd(result:unknown=false):void {
    if(!this.scenario||this.ended)throw new Error("no active scenario to end");
    this.ended=true;this.endValue=result;
  }
  private async finish(result:unknown,restoreCityDice=true):Promise<void>{
    if(!this.scenario||!this.worldDice)throw new Error("no scenario to end");
    this.state.team.dismissTemporary();
    if(restoreCityDice)this.state.team.restoreDice(this.worldDice);
    this.state.setGlobal("location","world");
    this.scenario=null;this.scenarioActions=null;this.worldDice=null;
    if(this.callback!==false&&this.callback!==undefined&&this.callback!==null){
      this.endValue=result;
      await this.world.evaluate(
        "(let ((cb (__ssnoir-callback))) (if cb (cb (__ssnoir-scenario-result)) #f))"
      );
    }
    this.clearScenarioLifecycle();
    await this.worldActions.prepare();
  }
}
