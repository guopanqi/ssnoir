import type {StageBeat,StageCommand} from "./stage-contract.ts";

export interface StageEntity {
  id:string;kind:"actor"|"prop";asset:string;layer:"back"|"middle"|"front";
  x:number;y:number;pose:string;light:string;
}
export interface StageCaption {speaker:string;text:string;remote:boolean}
export interface StageEffectEvent {kind:"effect"|"sound";id:string;asset:string;x:number;y:number;at:number}
type Motion={id:string;from:[number,number];to:[number,number];points:[number,number][];duration:number;elapsed:number};

/**
 * Port of Unity StoryStagePlayer + StageState's StageMotion.
 * Deterministic in explicit seconds, no browser timer or asset side effects.
 * Timed commands within a beat run concurrently; only say/remote-say await a click.
 * Movement is linear; paths evaluate the original C# de Casteljau Bezier points.
 */
export class StagePlayback {
  readonly entities=new Map<string,StageEntity>();
  readonly effects:StageEffectEvent[]=[];
  caption:StageCaption|null=null;
  complete=false;
  started=false;
  elapsed=0;
  beatIndex=-1;
  readonly beats:StageBeat[];
  private remaining=0;
  private motions:Motion[]=[];
  private waiting=false;
  private revealed=false;

  constructor(beats:StageBeat[]){
    if(!beats.length)throw Error("stage: missing beats");
    this.beats=beats;
  }
  start():void{
    if(this.started)throw Error("stage already started");
    this.started=true;
    this.advanceBeat();
  }
  get requiresInput():boolean{return this.waiting;}
  get visibleCharacters():number {
    if(!this.caption)return 0;
    return this.revealed?this.caption.text.length:
      Math.min(this.caption.text.length,Math.max(0,1+Math.floor(this.elapsed/0.048)));
  }
  advance():void {
    if(!this.waiting||this.complete)return; // Unity doesn't skip animation beats.
    if(!this.revealed&&this.visibleCharacters<this.caption!.text.length) {
      this.revealed=true;return;
    }
    this.waiting=false;this.caption=null;this.advanceBeat();
  }
  tick(seconds:number):void {
    if(!this.started)throw Error("stage not started");
    if(!Number.isFinite(seconds)||seconds<0)throw Error("invalid stage delta");
    let budget=seconds;
    let steps=0;
    while(!this.complete&&!this.waiting){
      if(++steps>10000)throw Error("stage exceeded immediate beat budget");
      const step=Math.min(budget,this.remaining);
      this.remaining-=step;this.elapsed+=step;
      for(const motion of this.motions){
        motion.elapsed=Math.min(motion.duration,motion.elapsed+step);
        const t=motion.duration<=0?1:motion.elapsed/motion.duration;
        const points=motion.points.length?motion.points:[motion.from,motion.to];
        const work=points.map(x=>[...x] as [number,number]);
        for(let n=work.length-1;n>0;n--)
          for(let i=0;i<n;i++){
            work[i][0]=work[i][0]+(work[i+1][0]-work[i][0])*t;
            work[i][1]=work[i][1]+(work[i+1][1]-work[i][1])*t;
          }
        const entity=this.require(motion.id);
        entity.x=work[0][0];entity.y=work[0][1];
      }
      budget-=step;
      if(this.remaining>0)break;
      if(this.beatIndex===this.beats.length-1){this.complete=true;this.caption=null;break;}
      this.advanceBeat();
      if(budget===0&&this.remaining>0)break;
    }
    if(this.waiting&&this.caption&&seconds>0)this.elapsed+=seconds;
    this.effects.splice(0,this.effects.findIndex(e=>this.elapsed-e.at<=.2) < 0 ?
      this.effects.filter(e=>this.elapsed-e.at>.2).length :
      this.effects.findIndex(e=>this.elapsed-e.at<=.2));
  }
  private require(id:string):StageEntity {
    const value=this.entities.get(id);
    if(!value)throw Error("missing stage entity "+id);
    return value;
  }
  private begin(command:StageCommand):void {
    const {kind,id}=command;
    if(kind==="spawn"||kind==="prop"||kind==="prop-at"){
      if(this.entities.has(id))throw Error("stage duplicate entity "+id);
      const pos=kind==="prop-at"?this.require(command.anchor):null;
      this.entities.set(id,{id,kind:kind==="spawn"?"actor":"prop",asset:command.asset,
        layer:command.layer,x:(pos?.x??0)+command.x,y:(pos?.y??0)+command.y,pose:"基础",light:"normal"});
    }else if(kind==="remove"){
      this.require(id);this.entities.delete(id);
    }else if(kind==="pose"){
      this.require(id).pose=command.asset;
    }else if(kind==="light"){
      this.require(id).light=command.asset;
    }else if(kind==="move"||kind==="path"||kind==="path-relative"){
      const entity=this.require(id);
      const from:[number,number]=[entity.x,entity.y];
      let points:[number,number][]=[];
      let to:[number,number];
      if(kind==="move")to=[command.x,Number.isNaN(command.y)?entity.y:command.y];
      else{
        points=command.points.map(p=>[
          p[0]+(command.relative?entity.x:0),
          p[1]+(command.relative?entity.y:0)]);
        to=points[points.length-1];
      }
      this.motions.push({id,from,to,points,duration:command.seconds,elapsed:0});
      if(command.seconds===0){entity.x=to[0];entity.y=to[1];}
    }else if(kind==="say"||kind==="remote-say"){
      this.caption={speaker:command.speaker,text:command.text,remote:kind==="remote-say"};
      this.waiting=true;this.revealed=false;this.elapsed=0;
    }else if(kind==="sound"||kind==="effect"){
      const origin=kind==="effect"?this.require(id):null;
      this.effects.push({kind,id,asset:command.asset,x:(origin?.x??0)+command.x,
        y:(origin?.y??0)+command.y,at:this.elapsed});
    }
  }
  private advanceBeat():void {
    if(this.complete)return;
    this.beatIndex++;
    if(this.beatIndex>=this.beats.length){this.complete=true;return;}
    const beat=this.beats[this.beatIndex];
    this.remaining=beat.seconds;
    this.motions=[];
    for(const command of beat.commands)this.begin(command);
  }
}
