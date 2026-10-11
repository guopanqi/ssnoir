import {GameRuntimeState} from "./game-state.ts";
import {GameScriptSession,type ScriptSources} from "./script-session.ts";
import {GameActionRunner} from "./action-runner.ts";
import {ActionTurnState} from "./turn-state.ts";
import {schemeList,type RuntimeNode} from "./node-converter.ts";
import {storyArguments} from "./story-events.ts";

/**
 * One state, two isolated Scheme interpreters; original C# SceneManager
 * start/end-encounter contract. Source scripts remain unchanged.
 * Transitions are applied AFTER a Scheme effect finishes (not nested inside
 * Interpreter.exec), so world callbacks retain their original Scheme closure.
 */
export class GameSceneDirector {
  readonly state:GameRuntimeState;
  readonly world:GameScriptSession;
  readonly worldActions:GameActionRunner;
  encounter:GameScriptSession|null=null;
  encounterActions:GameActionRunner|null=null;
  private readonly sources:ScriptSources;
  private readonly std:Uint8Array;
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
    actions:GameActionRunner,sources:ScriptSources,std:Uint8Array,random:()=>number){
    this.state=state;this.world=world;this.worldActions=actions;
    this.sources=sources;this.std=std;this.random=random;
  }

  static async create(sources:ScriptSources,std:Uint8Array,
    random:()=>number=Math.random):Promise<GameSceneDirector> {
    const state=new GameRuntimeState();
    let self:GameSceneDirector|undefined;
    const world=await GameScriptSession.create("ssnoir-world-director",state,sources,std,{
      "start-encounter":(...args)=>{
        if(!self)throw new Error("scene director uninitialized");
        if(args.length<1)throw new Error("start-encounter requires name");
        if(self.encounter||self.pendingStart)throw new Error("encounter already active/pending");
        const name=String(args[0]);
        self.pendingStart={name,callback:args[1]??false};
        return undefined;
      },
      "__end-turn!":()=>{
        if(!self)throw new Error("scene director uninitialized");
        self.pendingEndTurn=true;return undefined;
      },
      "__ssnoir-callback":()=>self?.callback??false,
      "__ssnoir-encounter-result":()=>self?.endValue??false
    },random);
    await world.loadFile("scenes/world/world.scm");
    const actions=await GameActionRunner.create(world,state,new ActionTurnState(random));
    self=new GameSceneDirector(state,world,actions,sources,std,random);
    state.team.rollDice(random,false,true,state.injury.losesDie);
    await actions.prepare();
    return self;
  }

  get mode():"world"|"encounter"{return this.encounter?"encounter":"world";}
  get actions():GameActionRunner {return this.encounterActions??this.worldActions;}
  async render():Promise<RuntimeNode>{return this.actions.prepare();}
  requestStart(name:string,callback:unknown=false):void {
    if(this.encounter||this.pendingStart)throw new Error("already in encounter");
    this.pendingStart={name,callback};
  }
  /** Integrate pending start-encounter bridge AFTER caller action ends. */
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
      if(!this.encounter)throw new Error("encounter dice refresh requested outside encounter");
      this.state.team.rollDice(this.random,true,false,this.state.injury.losesDie);
    }
    if(this.pendingEndTurn){
      this.pendingEndTurn=false;
      if(this.encounter)await this.endRound();
      else throw new Error("world end-turn requires WorldTurnController");
    }
  }

  async start(name:string,callback:unknown=false):Promise<RuntimeNode>{
    if(this.encounter)throw new Error("cannot nest encounters");
    if(typeof name!=="string"||!name.trim()||
       name.includes("/")||name.includes("\\")||name.includes(".."))
      throw new Error("invalid encounter name");
    const sourceName="scenes/encounters/"+name+".scm";
    if(!Object.hasOwn(this.sources,sourceName))
      throw new Error("authored encounter does not exist: "+name);
    const previous=this.state.snapshot();
    this.worldDice=this.state.team.diceSnapshot();
    this.callback=callback;
    try{
      this.state.setGlobal("location","encounter");
      this.supportUsed=false;
      this.ended=false;
      const encounter=await GameScriptSession.create(
        "ssnoir-encounter-"+name,this.state,this.sources,this.std,{
        "end-encounter":(...args)=>{
          if(!this.encounter||this.ended)throw new Error("invalid end-encounter");
          if(this.state.hasPendingHospitalization())throw new Error("cannot end while collapsed");
          this.ended=true;this.endValue=args[0]??false;return undefined;
        },
        "__support-used?":()=>this.supportUsed,
        "__refresh-encounter-dice!":()=>{
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
          this.state.team.rollDice(this.random,true,false,this.state.injury.losesDie);
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
      await encounter.loadFile(sourceName);
      const runner=await GameActionRunner.create(encounter,this.state,new ActionTurnState(this.random));
      this.encounter=encounter;this.encounterActions=runner;
      this.state.team.rollDice(this.random,true,false,this.state.injury.losesDie);
      this.state.beginActionReport();
      try{await encounter.evaluate("(on-encounter-enter)");this.state.takeActionReport();}
      catch(error){this.state.cancelActionReport();throw error;}
      return runner.prepare();
    }catch(error){
      this.state.restore(previous);
      this.worldDice=null;this.callback=false;this.encounter=null;this.encounterActions=null;
      throw error;
    }
  }

  /**
   * C# round transition sequence: time tax -> opponent rules in authored
   * registration order -> maintenance -> new dice -> automatic responses.
   * Each opponent rule commits separately; the scene is re-read after it.
   */
  async endRound():Promise<Array<{phase:string;events:ReturnType<GameRuntimeState["takeStoryEvents"]>}>>{
    if(!this.encounter||!this.encounterActions)throw new Error("encounter required");
    if(this.roundActive)throw new Error("nested round transition");
    // C# SceneManager.BeginRoundTransition does not consult city rest
    // blockers. Chapter duties prevent ending a WORLD day, not taking an
    // encounter turn while those duties are still outstanding.
    this.autoActions=[];this.autoIndex=0;
    const frames:Array<{phase:string;events:ReturnType<GameRuntimeState["takeStoryEvents"]>}>=[];
    const capture=(phase:string)=>frames.push({phase,events:this.state.takeStoryEvents()});
    this.roundActive=true;
    try{
      this.state.beginActionReport();
      try{
        const overflow=this.state.team.spend("player",1);
        if(overflow>0)this.state.applyInjury(overflow);
        this.state.takeActionReport();capture("time-tax");
      }catch(error){this.state.cancelActionReport();throw error;}
      if(this.state.hasPendingHospitalization())throw new Error("hospitalization must resolve before opponent rules");
      await this.encounter.evaluate("(__begin-opponent-rules!)");
      for(let step=0;step<1024;step++){
        if(this.ended||!this.encounter)break;
        const pending=await this.encounter.evaluate("(__opponent-rules-pending?)");
        if(pending===false||String(pending)==="#f")break;
        this.state.beginActionReport();
        try{
          await this.encounter.evaluate("(__run-next-opponent-rule!)");
          this.state.takeActionReport();capture("opponent-rule");
        }catch(error){this.state.cancelActionReport();throw error;}
        if(this.state.hasPendingHospitalization())throw new Error("hospitalization must resolve before next rule");
        await this.encounterActions.prepare();
        if(step===1023)throw new Error("opponent rules exceed 1024");
      }
      if(this.ended){await this.flush();capture("encounter-ended");return frames;}
      this.state.team.dismissTemporary();
      this.state.team.rollDice(this.random,true,false,this.state.injury.losesDie);
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
          await this.encounter.evaluate("((__ssnoir-auto-effect "+i+"))");
          this.state.takeActionReport();
          capture("forced-action");
        }catch(error){this.state.cancelActionReport();throw error;}
        if(this.ended){await this.flush();break;}
      }
      if(this.encounterActions&&this.encounter)await this.encounterActions.prepare();
      return frames;
    }finally{
      this.roundActive=false;this.autoActions=[];this.autoIndex=0;
    }
  }

  requestEnd(result:unknown=false):void {
    if(!this.encounter||this.ended)throw new Error("no active encounter to end");
    this.ended=true;this.endValue=result;
  }
  private async finish(result:unknown):Promise<void>{
    if(!this.encounter||!this.worldDice)throw new Error("no encounter to end");
    this.state.team.dismissTemporary();
    this.state.team.restoreDice(this.worldDice);
    this.state.setGlobal("location","world");
    this.encounter=null;this.encounterActions=null;this.worldDice=null;
    if(this.callback!==false&&this.callback!==undefined&&this.callback!==null){
      this.endValue=result;
      await this.world.evaluate(
        "(let ((cb (__ssnoir-callback))) (if cb (cb (__ssnoir-encounter-result)) #f))"
      );
    }
    this.callback=false;this.supportUsed=false;
    await this.worldActions.prepare();
  }
}
