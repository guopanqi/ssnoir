import {GameRuntimeState} from "./game-state.ts";
import {GameScriptSession,type ScriptSources} from "./script-session.ts";
import {GameActionRunner} from "./action-runner.ts";
import {ActionTurnState} from "./turn-state.ts";
import type {RuntimeNode} from "./node-converter.ts";

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
      "__ssnoir-callback":()=>self?.callback??false,
      "__ssnoir-encounter-result":()=>self?.endValue??false
    });
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
      this.ended=false;await this.finish(this.endValue);
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
        "__summon-helper!":()=>{throw new Error("summon-helper action requires encounter support handler");},
        "__ssnoir-mode":()=>this.mode
      });
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
