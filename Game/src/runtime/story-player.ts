import type {StoryEvent,StoryValue} from "./story-events.ts";

export interface BubbleRecovery {
  actorId:string; amount:number; notice:string; worldDay:number; id:number;
}
export interface StoryFrame {
  kind:StoryEvent["kind"]; speaker:string; text:string;
  blocking:boolean;commands:StoryValue[];
  recovery?:BubbleRecovery;
}
/**
 * Transport-neutral narrative stepping. Actual stage actor motions are passed
 * as command arrays to the host presentation layer, not executed as JS or
 * omitted. Blocking frames must be explicitly acknowledged by the player.
 */
export class StoryPlaybackQueue {
  private pending:StoryFrame[]=[];
  private currentFrame:StoryFrame|null=null;
  private chatter:StoryFrame[]=[];

  private static text(value:StoryValue):string {
    return value===null?"":Array.isArray(value)?value.map(StoryPlaybackQueue.text).join(" "):String(value);
  }
  private static dialogue(args:StoryValue[]):{
    speaker:string;text:string;commands:StoryValue[];recovery?:BubbleRecovery
  }[] {
    const seq=args.length===1&&Array.isArray(args[0]) &&
      Array.isArray((args[0] as StoryValue[])[0]) ? args[0] as StoryValue[] : args;
    return seq.map(raw=>{
      if(!Array.isArray(raw))
        return {speaker:"",text:StoryPlaybackQueue.text(raw),commands:[raw]};
      const command=raw as StoryValue[];
      // Retain support for legacy tagged tests and older scripts.
      if(command.length>=3&&["line","remote-line"].includes(String(command[0])))
        return {speaker:StoryPlaybackQueue.text(command[1]),
          text:StoryPlaybackQueue.text(command[2]),commands:command};
      // engine.scm (line ...) emits identity, text, voice, dwell, stage,
      // display, anchor, optional appearance: seven/eight data fields.
      // A bubble recovery appends a tagged, purely serializable instruction.
      const end=command.at(-1);
      let recovery:BubbleRecovery|undefined;
      if(Array.isArray(end)&&end[0]==="ssnoir-bubble-recovery"){
        if(end.length!==6||typeof end[1]!=="string"||!Number.isSafeInteger(end[2])||
           (end[2] as number)<=0||typeof end[3]!=="string"||
           !Number.isSafeInteger(end[4])||!Number.isSafeInteger(end[5]))
          throw new Error("malformed bubble recovery metadata");
        recovery={actorId:end[1],amount:end[2] as number,
          notice:end[3],worldDay:end[4] as number,id:end[5] as number};
      }
      const size=command.length-(recovery?1:0);
      if((size===7||size===8)&&
         typeof command[0]==="string"&&typeof command[1]==="string"&&
         typeof command[3]==="number"&&Array.isArray(command[4])&&
         typeof command[5]==="string"&&typeof command[6]==="string"&&
         (size===7||typeof command[7]==="string"))
        return {speaker:(command[5] as string)||command[0],text:command[1] as string,
          commands:command,...(recovery?{recovery}:{})};
      if(recovery)throw new Error("bubble recovery requires an authored line");
      return {speaker:"",text:StoryPlaybackQueue.text(raw),commands:command};
    });
  }
  private static frames(event:StoryEvent):StoryFrame[]{
    const {kind,args,blocking}=event;
    if(["dialogue","banter","remote-dialogue","remote-banter"].includes(kind)){
      return StoryPlaybackQueue.dialogue(args).map(line=>{
        if(line.recovery&&kind!=="banter"&&kind!=="remote-banter")
          throw new Error("bubble recovery only permitted in banter");
        return {kind,blocking,commands:line.commands,
          speaker:line.speaker,text:line.text,
          ...(line.recovery?{recovery:line.recovery}:{})};
      });
    }
    if(kind==="intertitle"){
      if(args.length!==2||typeof args[0]!=="boolean"||!Array.isArray(args[1]))
        throw new Error("invalid intertitle story event");
      return (args[1] as StoryValue[]).map(page=>{
        if(!Array.isArray(page)||page.length!==2||typeof page[0]!=="string"||typeof page[1]!=="string")
          throw new Error("invalid intertitle page");
        return {kind,blocking,commands:[args[0],page],speaker:"尼尔 · 独白",text:page[0]};
      });
    }
    if(kind==="camera"){
      if(args.length!==4||typeof args[0]!=="string"||typeof args[1]!=="number"||
         !Number.isFinite(args[1])||typeof args[2]!=="string"||typeof args[3]!=="boolean")
        throw new Error("invalid camera story event");
      return [{kind,blocking,commands:args,speaker:"镜头",
        text:(args[3]?"切镜至 ":"镜头经过 ")+args[0]+(args[2]?"，来到 "+args[2]:"")}];
    }
    if(kind==="spotlight")return [{
      kind,blocking,commands:args,speaker:StoryPlaybackQueue.text(args[0]),
      text:StoryPlaybackQueue.text(args[1])
    }];
    if(kind==="narration")return [{
      kind,blocking,commands:args,speaker:"旁白",
      text:StoryPlaybackQueue.text(args[0])
    }];
    // Stage/Theatre/Motion/Video: retain all structured commands for
    // an actual renderer; display a human-readable caption until provided.
    return [{kind,blocking,commands:args,speaker:kind,
      text:args.map(StoryPlaybackQueue.text).join(" ").slice(0,240)}];
  }
  push(events:StoryEvent[]):void {
    for(const event of events){
      for(const frame of StoryPlaybackQueue.frames(event)){
        if(frame.blocking)this.pending.push(frame);
        else this.chatter.push(frame);
      }
    }
    if(!this.currentFrame)this.advance();
  }
  get current():StoryFrame|null{return this.currentFrame;}
  get blocked():boolean{return this.currentFrame!==null;}
  advance():StoryFrame|null {
    this.currentFrame=this.pending.shift()??null;
    return this.currentFrame;
  }
  takeBanter():StoryFrame[]{const b=this.chatter;this.chatter=[];return b;}
  pendingCount():number{return this.pending.length+(this.currentFrame?1:0);}
}
