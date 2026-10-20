import type {StoryEvent,StoryValue} from "./story-events.ts";

export interface StoryFrame {
  kind:StoryEvent["kind"]; speaker:string; text:string;
  blocking:boolean;commands:StoryValue[];
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
  private static dialogue(args:StoryValue[]):{speaker:string;text:string}[] {
    const seq=args.length===1&&Array.isArray(args[0]) &&
      Array.isArray((args[0] as StoryValue[])[0]) ? args[0] as StoryValue[] : args;
    return seq.flatMap(raw=>{
      if(!Array.isArray(raw))return [{speaker:"",text:StoryPlaybackQueue.text(raw)}];
      const command=raw as StoryValue[];
      // Authored (line speaker text [voice]) and (remote-line ...).
      if(command.length>=3&&["line","remote-line"].includes(String(command[0])))
        return [{speaker:StoryPlaybackQueue.text(command[1]),
          text:StoryPlaybackQueue.text(command[2])}];
      return [{speaker:"",text:StoryPlaybackQueue.text(raw)}];
    });
  }
  private static frames(event:StoryEvent):StoryFrame[]{
    const {kind,args,blocking}=event;
    if(["dialogue","banter","remote-dialogue","remote-banter"].includes(kind)){
      return StoryPlaybackQueue.dialogue(args).map(line=>({
        kind,blocking,commands:args,speaker:line.speaker,text:line.text
      }));
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
