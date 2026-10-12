/**
 * Port of Engine/Runtime/Core/{ActorState,TeamState}.cs.
 * One actor roster shared by World and Encounter VMs.
 */
export const SKILLS = ["violence", "knowledge", "sharpness", "social"] as const;
export type Skill = typeof SKILLS[number];
export type Stats = Record<Skill, number>;
export interface ActorSave {
  id: string; name: string; role: "protagonist"|"companion";
  status: "active"|"away"; composure: number; stats: Stats;
  actionSlotCount: number; fixedDieValue: number|null; fixedDieLabel: string;
  spentGrowthPoints: number; isDrunk: boolean; hangoverSlotId: number|null;
  permanentDiePenalty: number; permanentDiePenaltyLabel: string;
  actionDice: number[]; actionDiceSlotIds: number[];
  temporary: boolean;
}
export interface TeamSave {
  actors: ActorSave[]; supports: string[]; carriedSupport: string;
}
const checkInt = (n:number,min:number,max:number,what:string) => {
  if (!Number.isSafeInteger(n)||n<min||n>max) throw new Error(what+" outside "+min+".."+max);
};
const ident = (s:string, what:string) => {
  if(typeof s!=="string"||!s.trim())throw new Error("invalid "+what);
  return s;
};
function clone(a:ActorSave):ActorSave {
  return {...a,stats:{...a.stats},actionDice:[...a.actionDice],actionDiceSlotIds:[...a.actionDiceSlotIds]};
}
function validateStats(stats:Stats):void {
  if(!stats||Object.keys(stats).length!==4)throw new Error("exactly four actor stats required");
  for(const key of SKILLS)checkInt(stats[key],-2,4,key+" stat");
}
function validateActor(a:ActorSave):void {
  ident(a.id,"actor id");ident(a.name,"actor name");
  if(a.role!=="protagonist"&&a.role!=="companion")throw new Error("invalid actor role");
  if(a.status!=="active"&&a.status!=="away")throw new Error("invalid actor status");
  checkInt(a.actionSlotCount,1,4,"actor slots");
  checkInt(a.composure,0,a.role==="protagonist"?5:a.actionSlotCount*2,"composure");
  validateStats(a.stats);
  if(a.fixedDieValue!==null){checkInt(a.fixedDieValue,1,6,"fixed die");ident(a.fixedDieLabel,"fixed die label");}
  else if(a.fixedDieLabel!=="")throw new Error("fixed die label without fixed value");
  checkInt(a.spentGrowthPoints,0,100000,"spent growth");
  if(typeof a.isDrunk!=="boolean")throw new Error("invalid drunk actor flag");
  if(a.hangoverSlotId!==null)checkInt(a.hangoverSlotId,0,a.actionSlotCount-1,"hangover slot");
  checkInt(a.permanentDiePenalty,-2,0,"permanent die penalty");
  if(a.permanentDiePenalty!==0)ident(a.permanentDiePenaltyLabel,"permanent penalty label");
  if(a.actionDice.length!==a.actionDiceSlotIds.length)throw new Error("die value and slot arrays differ");
  const unique=new Set<number>();
  for(let i=0;i<a.actionDice.length;i++){
    checkInt(a.actionDice[i],1,6,"action die");
    checkInt(a.actionDiceSlotIds[i],0,a.actionSlotCount-1,"action die slot");
    if(unique.has(a.actionDiceSlotIds[i]))throw new Error("duplicate die slot");
    unique.add(a.actionDiceSlotIds[i]);
  }
  if(typeof a.temporary!=="boolean")throw new Error("invalid temporary flag");
}
export class TeamState {
  private actors:ActorSave[]=[];
  private supports:string[]=[];
  private carriedSupport="";
  constructor(){this.reset();}
  reset():void {
    this.actors=[{
      id:"player",name:"尼尔",role:"protagonist",status:"active",composure:5,
      stats:{violence:-1,knowledge:1,sharpness:0,social:0},
      actionSlotCount:4,fixedDieValue:null,fixedDieLabel:"",
      spentGrowthPoints:0,isDrunk:false,hangoverSlotId:null,permanentDiePenalty:0,
      permanentDiePenaltyLabel:"",actionDice:[],actionDiceSlotIds:[],temporary:false
    }];
    this.supports=[];this.carriedSupport="";
  }
  find(id:string):ActorSave|undefined{return this.actors.find(a=>a.id===id);}
  require(id:string):ActorSave {
    const actor=this.find(id);if(!actor)throw new Error("actor '"+id+"' not found");return actor;
  }
  roster():ActorSave[]{return this.actors.map(clone);}
  composure(id:string):number{return this.require(id).composure;}
  status(id:string):string{return this.require(id).status;}
  setStatus(id:string,status:"active"|"away"):void {
    if(status!=="active"&&status!=="away")throw new Error("invalid status");
    this.require(id).status=status;
  }
  stat(id:string,skill:string):number {
    if(!SKILLS.includes(skill as Skill))throw new Error("invalid skill "+skill);
    return this.require(id).stats[skill as Skill];
  }
  recruit(id:string,name:string,stats:Stats,temporary=false):void {
    ident(id,"actor id");ident(name,"actor name");
    if(this.find(id))throw new Error("actor already recruited: "+id);
    validateStats(stats);
    this.actors.push({
      id,name,role:"companion",status:"active",composure:2,stats:{...stats},
      actionSlotCount:1,fixedDieValue:null,fixedDieLabel:"",
      spentGrowthPoints:0,isDrunk:false,hangoverSlotId:null,permanentDiePenalty:0,
      permanentDiePenaltyLabel:"",actionDice:[],actionDiceSlotIds:[],temporary
    });
  }
  dismiss(id:string):void {
    const a=this.require(id);if(a.role!=="companion")throw new Error("cannot dismiss protagonist");
    this.actors=this.actors.filter(x=>x.id!==id);
  }
  dismissTemporary():void {this.actors=this.actors.filter(a=>!a.temporary);}
  grantSupport(id:string):void {
    ident(id,"support id");
    if(!this.supports.includes(id))this.supports.push(id);
    if(!this.carriedSupport)this.carriedSupport=id;
  }
  hasSupport(id:string):boolean {return this.supports.includes(id);}
  setCarriedSupport(id:string):void {
    if(id!==""&&!this.hasSupport(id))throw new Error("support not granted: "+id);
    this.carriedSupport=id;
  }
  getCarriedSupport():string|false{return this.carriedSupport||false;}
  getSupports():string[]{return [...this.supports];}
  dieProfile(id:string,slots:number,fixedValue:number|null,label:string):void {
    const a=this.require(id);
    checkInt(slots,1,4,"slot count");
    if(fixedValue!==null){checkInt(fixedValue,1,6,"fixed die");ident(label,"fixed die label");}
    const full=a.composure===(a.role==="protagonist"?5:a.actionSlotCount*2);
    a.actionSlotCount=slots;a.fixedDieValue=fixedValue;a.fixedDieLabel=fixedValue===null?"":label;
    if(full)a.composure=a.role==="protagonist"?5:slots*2;
    else a.composure=Math.min(a.composure,a.role==="protagonist"?5:slots*2);
    // An already-rolled pool belongs to previous profile: reissue on next turn.
    a.actionDice=[];a.actionDiceSlotIds=[];
  }
  permanentDiePenalty(id:string,label:string,penalty:number):void {
    checkInt(penalty,-2,0,"permanent penalty");
    if(penalty!==0)ident(label,"permanent penalty label");
    const a=this.require(id);a.permanentDiePenalty=penalty;a.permanentDiePenaltyLabel=penalty===0?"":label;
  }
  setDrunk(id:string,drunk=true):void {this.require(id).isDrunk=drunk;}
  applyHangover():void {this.require("player").hangoverSlotId=0;}
  spend(id:string,amount:number):number {
    checkInt(amount,0,100000,"composure cost");
    const a=this.require(id),overflow=Math.max(0,amount-a.composure);
    a.composure=Math.max(0,a.composure-amount);
    if(a.role==="companion"&&a.composure===0)a.status="away";
    return a.role==="protagonist"?overflow:0;
  }
  restoreComposure(id:string,amount:number):void {
    checkInt(amount,0,100000,"composure recovery");
    const a=this.require(id);
    a.composure=Math.min(a.role==="protagonist"?5:a.actionSlotCount*2,a.composure+amount);
    if(a.role==="companion"&&a.composure>0)a.status="active";
  }
  setComposure(id:string,value:number):number {
    checkInt(value,0,100000,"composure value");
    const a=this.require(id);
    return value<a.composure?this.spend(id,a.composure-value):(this.restoreComposure(id,value-a.composure),0);
  }
  /**
   * Original TeamState.RollActionDice: slot IDs are stable, dice are ephemeral.
   * A serious injury removes the final protagonist slot, not an arbitrary
   * array index. Hangover affects slot 0 on the NEXT city refresh only.
   */
  rollDice(random:()=>number,isEncounter=false,consumeHangover=true,injuryCostsDie=false):void {
    for(const actor of this.actors){
      if(!isEncounter&&consumeHangover)actor.isDrunk=false;
      const applyHangover=!isEncounter&&consumeHangover&&actor.hangoverSlotId!==null;
      actor.actionDice=[];actor.actionDiceSlotIds=[];
      if(actor.status!=="active")continue;
      let count=actor.actionSlotCount;
      if(actor.role==="protagonist"&&injuryCostsDie)count--;
      for(let slot=0;slot<count;slot++){
        const n=random();
        if(!Number.isFinite(n)||n<0||n>=1)throw new Error("invalid random d6");
        const raw=1+Math.floor(6*n);
        const penalty=slot===0?actor.permanentDiePenalty:0;
        const hangover=applyHangover&&actor.hangoverSlotId===slot?-1:0;
        const die=actor.fixedDieValue??Math.max(1,raw+penalty+hangover);
        actor.actionDice.push(die);actor.actionDiceSlotIds.push(slot);
      }
      if(applyHangover)actor.hangoverSlotId=null;
    }
  }
  dice(actorId:string):{slotId:number;value:number}[] {
    const actor=this.require(actorId);
    return actor.actionDice.map((value,i)=>({value,slotId:actor.actionDiceSlotIds[i]}));
  }
  spendDie(actorId:string,slotId:number):number {
    const actor=this.require(actorId),at=actor.actionDiceSlotIds.indexOf(slotId);
    if(at<0)throw new Error("spent or unknown action slot "+actorId+"/"+slotId);
    const [result]=actor.actionDice.splice(at,1);
    actor.actionDiceSlotIds.splice(at,1);
    return result;
  }
  diceSnapshot():Record<string,{slotIds:number[];dice:number[]}> {
    return Object.fromEntries(this.actors.map(a=>[a.id,{slotIds:[...a.actionDiceSlotIds],dice:[...a.actionDice]}]));
  }
  restoreDice(saved:Record<string,{slotIds:number[];dice:number[]}>):void {
    const next=this.snapshot(true);
    for(const actor of next.actors){
      const entry=saved[actor.id];
      actor.actionDice=entry?[...entry.dice]:[];
      actor.actionDiceSlotIds=entry?[...entry.slotIds]:[];
    }
    this.restore(next,true);
  }

  beginCityDay():string[]{
    const returned:string[]=[];
    for(const a of this.actors)if(a.role==="companion"){
      if(a.status==="away")returned.push(a.name);
      a.composure=a.actionSlotCount*2;a.status="active";
    }
    return returned;
  }
  restore(data:TeamSave,allowTemporary=false):void {
    if(!data||!Array.isArray(data.actors)||!Array.isArray(data.supports))throw new Error("invalid team save");
    const ids=new Set<string>();
    for(const a of data.actors){
      validateActor(a);
      if(ids.has(a.id))throw new Error("duplicate actor id");ids.add(a.id);
      if(a.temporary&&!allowTemporary)throw new Error("temporary companion cannot appear in save");
    }
    if(data.actors.filter(a=>a.id==="player"&&a.role==="protagonist").length!==1||
       data.actors.some(a=>a.role==="protagonist"&&a.id!=="player"))
      throw new Error("save requires exactly one protagonist");
    if(data.supports.some(s=>typeof s!=="string"||!s.trim()) ||
       new Set(data.supports).size!==data.supports.length)throw new Error("invalid support ids");
    if(typeof data.carriedSupport!=="string"||
       data.carriedSupport!==""&&!data.supports.includes(data.carriedSupport))
      throw new Error("invalid carried support");
    this.actors=data.actors.map(clone);this.supports=[...data.supports];
    this.carriedSupport=data.carriedSupport;
  }
  snapshot(allowTemporary=false):TeamSave {
    if(!allowTemporary&&this.actors.some(a=>a.temporary))throw new Error("temporary helper cannot be saved");
    return {actors:this.actors.map(clone),supports:[...this.supports],carriedSupport:this.carriedSupport};
  }
}
