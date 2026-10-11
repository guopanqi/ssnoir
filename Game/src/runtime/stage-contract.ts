import type {StoryValue} from "./story-events.ts";
export type StageKind="spawn"|"prop"|"prop-at"|"move"|"path"|"path-relative"|"remove"|"pose"|"light"|"effect"|"sound"|"say"|"remote-say"|"pause";
export interface StageCommand {
  kind:StageKind;id:string;asset:string;anchor:string;layer:"back"|"middle"|"front";
  x:number;y:number;seconds:number;relative:boolean;points:Array<[number,number]>;
  speaker:string;text:string;
}
export interface StageBeat {commands:StageCommand[];seconds:number;requiresInput:boolean}
function err(s:string):never{throw Error("stage: "+s);}
function seq(raw:StoryValue,who:string):StoryValue[] {
  if(!Array.isArray(raw))err(who+" must be a list");return raw;
}
function id(raw:StoryValue,who:string):string{
  if(typeof raw!=="string"||!raw.trim())err(who+" must be a nonempty string");
  return raw;
}
function number(raw:StoryValue,who:string,position=false):number{
  if(typeof raw!=="number"||!Number.isFinite(raw)||position&&(raw< -30||raw>30))err("invalid "+who);
  return raw;
}
function layer(v:StoryValue):StageCommand["layer"]{
  if(v!=="back"&&v!=="middle"&&v!=="front")err("invalid layer");return v;
}
/** Source: Engine/Runtime/Scripting/NativeFunctions.cs ParseStoryStage. */
export function parseStage(args:StoryValue[]):StageBeat[]{
  if(args.length!==1)err("expected list of beats");
  const beats=seq(args[0],"beats");
  if(!beats.length)err("empty stage");
  const actors=new Set<string>(),props=new Set<string>(),result:StageBeat[]=[];
  for(const beat of beats){
    const rows=seq(beat,"beat");
    if(!rows.length)err("empty beat");
    const commands:StageCommand[]=[],writes=new Set<string>();
    let hasSay=false;
    for(const row of rows){
      const p=seq(row,"command");if(!p.length)err("empty command");
      const kind=id(p[0],"operation") as StageKind;
      const a=p.slice(1);
      const command:StageCommand={kind,id:"",asset:"",anchor:"",layer:"middle",x:0,y:0,seconds:0,
        relative:false,points:[],speaker:"",text:""};
      if(kind==="spawn"||kind==="prop"){
        if(kind==="spawn"?(a.length!==4&&a.length!==5):a.length!==5)
          err(kind+" wrong argument count");
        command.id=id(a[0],"entity id");command.asset=id(a[1],"asset");
        command.x=number(a[2],"x",true);
        command.y=a.length===5?number(a[3],"y",true):0;
        command.layer=layer(a[a.length-1]);
        if(actors.has(command.id)||props.has(command.id))err("duplicate entity "+command.id);
        if(writes.has(command.id+"|all"))err("concurrent spawn");
        writes.add(command.id+"|all");(kind==="prop"?props:actors).add(command.id);
      }else if(kind==="prop-at"){
        if(a.length!==6)err("prop-at count");
        command.id=id(a[0],"prop id");command.asset=id(a[1],"asset");
        command.anchor=id(a[2],"anchor");
        if(!actors.has(command.anchor)&&!props.has(command.anchor))err("unknown prop anchor");
        command.x=number(a[3],"dx",true);command.y=number(a[4],"dy",true);
        command.layer=layer(a[5]);
        if(actors.has(command.id)||props.has(command.id))err("duplicate prop");
        writes.add(command.id+"|all");props.add(command.id);
      }else if(kind==="move"){
        if(a.length!==3&&a.length!==4)err("move count");
        command.id=id(a[0],"entity id");command.x=number(a[1],"x",true);
        command.y=a.length===4?number(a[2],"y",true):Number.NaN;
        command.seconds=number(a[a.length-1],"seconds");
      }else if(kind==="path"||kind==="path-relative"){
        if(a.length!==3)err("path count");
        command.id=id(a[0],"entity id");command.seconds=number(a[1],"seconds");
        if(command.seconds<=0)err("nonpositive path duration");
        const points=seq(a[2],"points");if(points.length<2)err("path too short");
        command.points=points.map(row=>{
          const pair=seq(row,"point");if(pair.length!==2)err("point count");
          return [number(pair[0],"point x",true),number(pair[1],"point y",true)];
        });
        command.relative=kind==="path-relative";
      }else if(["remove","pose","light","effect"].includes(kind)){
        const counts:{[key:string]:number}={remove:1,pose:2,light:2,effect:4};
        if(a.length!==counts[kind])err(kind+" count");
        command.id=id(a[0],"target");
        if(kind!=="remove")command.asset=id(a[1],"asset/state");
        if(kind==="light"&&!["normal","surge","faint","ember"].includes(command.asset))
          err("unknown light state");
        if(kind==="effect"){
          if(!["受击闪光","线条迸射"].includes(command.asset))err("unknown effect");
          command.x=number(a[2],"dx",true);command.y=number(a[3],"dy",true);
        }
      }else if(kind==="sound"){
        if(a.length!==2)err("sound count");
        command.asset=id(a[0],"sound asset");command.x=number(a[1],"pan");
      }else if(kind==="pause"){
        if(a.length!==1)err("pause count");command.seconds=number(a[0],"duration");
      }else if(kind==="say"||kind==="remote-say"){
        if(a.length!==1)err("say count");
        const line=seq(a[0],"original dialogue line");
        if(line.length!==6&&line.length!==7)err("wrong original line shape");
        command.speaker=id(line[0],"speaker");
        if(typeof line[1]!=="string")err("line text not string");
        command.text=line[1];
        if(hasSay||rows.length!==1)err("say must occupy own beat");
        if(kind==="say"&&command.speaker!=="世界"&&!actors.has(command.speaker))
          err("speaker is not on stage");
        hasSay=true;
      }else err("unknown command "+kind);
      if(command.seconds<0||!Number.isFinite(command.seconds))err("invalid duration");
      if(["move","path","path-relative","pose","light","effect","remove"].includes(kind)){
        if(!actors.has(command.id)&&!props.has(command.id))err("unknown target "+command.id);
        if((kind==="pose"||kind==="light")&&!actors.has(command.id))err("actor required");
        const channel=kind==="pose"?"pose":kind==="light"?"light":
          ["move","path","path-relative"].includes(kind)?"position":"all";
        if(kind!=="effect"){
          if(writes.has(command.id+"|all")||
             channel==="all"&&["position","pose","light"].some(c=>writes.has(command.id+"|"+c))||
             writes.has(command.id+"|"+channel))err("concurrent write "+command.id);
          writes.add(command.id+"|"+channel);
        }
        if(kind==="remove"){actors.delete(command.id);props.delete(command.id);}
      }
      commands.push(command);
    }
    result.push({commands,seconds:Math.max(0,...commands.map(x=>x.seconds)),requiresInput:hasSay});
  }
  return result;
}
