import type {TheatreCommand,TheatreObject,TheatreProgram,TheatreProperty} from "./theatre-contract.ts";
export interface TheatreObjectState {
  node:TheatreObject;asset:string;values:Record<TheatreProperty,number>;
}
export interface TheatreCaption {speaker:string;text:string;color:string;seconds:number}
/**
 * Platform-independent counterpart of Unity TheatreSession.cs.
 * One explicit caller-controlled tick drives all time. No setTimeout,
 * browser globals, SceneManager or implicit asynchronous worker.
 */
export class TheatrePlayback {
  readonly objects=new Map<string,TheatreObjectState>();
  readonly audioEvents:Array<{kind:"sound"|"stop-sound";id:string;asset:string;volume:number;pan:number;loop:boolean}>=[];
  caption:TheatreCaption|null=null;
  elapsed=0;
  paused=false;
  complete=false;
  private readonly root:Runner;
  waiting:Runner|null=null;
  captionStarted=0;
  revealed=false;
  started=false;
  readonly scene:TheatreProgram;
  constructor(scene:TheatreProgram) {
    this.scene=scene;
    for(const node of scene.objects){
      const values:Record<TheatreProperty,number>={
        x:0,y:0,"scale-x":1,"scale-y":1,rotation:0,opacity:1,reveal:1,brightness:1
      };
      Object.assign(values,node.initial);
      if(this.objects.has(node.id))throw Error("duplicate theatre state object "+node.id);
      this.objects.set(node.id,{node,asset:node.asset,values});
    }
    this.root=new Runner(this,scene.cue);
  }
  start():void {
    if(this.started)throw Error("theatre has already started");
    this.started=true;this.tick(0);
  }
  tick(seconds:number):void{
    if(!Number.isFinite(seconds)||seconds<0)throw Error("invalid theatre delta");
    if(!this.started)throw Error("theatre has not started");
    if(this.paused||this.complete)return;
    const remaining=this.root.step(seconds,this.elapsed);
    this.elapsed+=seconds-remaining;
    if(this.root.done){
      this.complete=true;this.waiting=null;this.caption=null;
    }
  }
  pause(value:boolean):void{this.paused=value;}
  advance():void {
    if(this.paused||this.complete||!this.waiting)return;
    if(this.visibleCharacters<this.waiting.speech.length){this.revealed=true;return;}
    this.waiting.acknowledged=true;
    this.tick(0);
  }
  get visibleCharacters():number {
    if(!this.caption)return 0;
    return this.revealed?this.caption.text.length:
      Math.min(this.caption.text.length,Math.max(0,1+Math.floor((this.elapsed-this.captionStarted)/.048)));
  }
  setCaption(cue:TheatreCommand,at:number,runner:Runner):void {
    this.caption={speaker:String(cue.data[0]),text:String(cue.data[1]),
      color:cue.kind==="caption-for"?String(cue.data[3]):"#F0CF8A",
      seconds:cue.kind==="caption-for"?Number(cue.data[2]):0};
    this.captionStarted=at;this.revealed=false;
    if(cue.kind==="say"){
      if(this.waiting)throw Error("concurrent theatre dialogue");
      this.waiting=runner;
    }
  }
  audio(kind:"sound"|"stop-sound",id:string,asset="",volume=1,pan=0,loop=false):void{
    this.audioEvents.push({kind,id,asset,volume,pan,loop});
  }
}
class Runner {
  done=false;acknowledged=false;
  private started=false;private index=0;private iterations=0;
  private elapsed=0;private from=0;private children:Runner[]=[];
  readonly host:TheatrePlayback;
  readonly cue:TheatreCommand;
  constructor(host:TheatrePlayback,cue:TheatreCommand){this.host=host;this.cue=cue;}
  get speech():string{return String(this.cue.data[1]??"");}
  private property():TheatreProperty{return String(this.cue.data[1]) as TheatreProperty;}
  step(seconds:number,now:number):number {
    if(this.done)return seconds;
    const cue=this.cue;
    if(!this.started){
      this.started=true;
      this.children=cue.children.map(c=>new Runner(this.host,c));
      if(cue.kind==="tween"||cue.kind==="animate"){
        const state=this.host.objects.get(String(cue.data[0]));
        if(!state)throw Error("unknown theatre object "+String(cue.data[0]));
        this.from=state.values[this.property()];
      }
      if(cue.kind==="image"){
        this.host.objects.get(String(cue.data[0]))!.asset=String(cue.data[1]);
        this.done=true;return seconds;
      }
      if(cue.kind==="clear-caption"){this.host.caption=null;this.done=true;return seconds;}
      if(cue.kind==="sound"){
        this.host.audio("sound",String(cue.data[0]),String(cue.data[1]),
          Number(cue.data[3]),Number(cue.data[4]),Boolean(cue.data[2]));
        this.done=true;return seconds;
      }
      if(cue.kind==="stop-sound"){
        this.host.audio("stop-sound",String(cue.data[0]));
        this.done=true;return seconds;
      }
      if(cue.kind==="say"||cue.kind==="caption-for")this.host.setCaption(cue,now,this);
    }
    if(cue.kind==="sequence"){
      let rest=seconds;
      while(this.index<this.children.length){
        const child=this.children[this.index];
        const left=child.step(rest,now+seconds-rest);
        if(!child.done)return 0;
        rest=left;this.index++;
      }
      this.done=true;return rest;
    }
    if(cue.kind==="parallel"){
      let rest=seconds,all=true;
      for(const child of this.children){
        const left=child.step(seconds,now);
        rest=Math.min(rest,left);all=all&&child.done;
      }
      this.done=all;return all?rest:0;
    }
    if(cue.kind==="during"){
      const left=this.children[0].step(seconds,now);
      const consumed=this.children[0].done?seconds-left:seconds;
      for(let i=1;i<this.children.length;i++)this.children[i].step(consumed,now);
      if(!this.children[0].done)return 0;
      // C# cancels background runners immediately when the main action ends.
      for(let i=1;i<this.children.length;i++)this.children[i].cancel();
      this.done=true;return left;
    }
    if(cue.kind==="repeat"||cue.kind==="loop"){
      let left=seconds;
      for(let iterations=0;iterations<10000;iterations++){
        const next=this.children[0].step(left,now+seconds-left);
        if(!this.children[0].done)return 0;
        this.children[0].cancel();
        this.iterations++;
        if(cue.kind==="repeat"&&this.iterations>=Number(cue.repeatCount)){
          this.done=true;return next;
        }
        if(next===left&&left>0)throw Error("theatre loop made no time progress");
        this.children[0]=new Runner(this.host,cue.children[0]);
        left=next;
        if(left===0){this.children[0].step(0,now+seconds);return 0;}
      }
      throw Error("theatre repeat exceeded step budget");
    }
    if(cue.kind==="say"&&!this.acknowledged)return 0;
    if(cue.kind==="say"){
      this.host.waiting=null;this.done=true;return seconds;
    }
    const duration=cue.kind==="caption-for"?Number(cue.data[2]):cue.seconds;
    const step=Math.min(seconds,Math.max(0,duration-this.elapsed));
    this.elapsed+=step;
    if(cue.kind==="tween"||cue.kind==="animate"){
      const target=this.host.objects.get(String(cue.data[0]))!;
      const key=this.property();
      let keys:[number,number][];
      let smooth=false;
      if(cue.kind==="tween"){
        const raw=cue.data[2] as unknown[];
        keys=[[0,this.from],[Number(raw[0]),Number(raw[1])]];
        smooth=raw[2]==="smooth";
      }else{
        keys=(cue.data[2] as unknown[][]).map(p=>[Number(p[0]),Number(p[1])]);
      }
      let value=keys[keys.length-1][1];
      for(let i=1;i<keys.length;i++){
        if(this.elapsed>keys[i][0])continue;
        const [t0,v0]=keys[i-1], [t1,v1]=keys[i];
        let t=(this.elapsed-t0)/(t1-t0);
        if(smooth)t=t*t*(3-2*t);
        value=v0+(v1-v0)*t;
        break;
      }
      target.values[key]=value;
    }
    this.done=this.elapsed>=duration;
    return this.done?seconds-step:0;
  }
  cancel():void {
    for(const child of this.children)child.cancel();
    if(this.host.waiting===this)this.host.waiting=null;
  }
}
