import type {StoryValue} from "./story-events.ts";

/**
 * Pure, host-independent port of Engine/Runtime/Scripting/TheatreParser.cs.
 * Validates the authored scene/program wire data before a renderer sees it.
 * Graphic/audio assets are not silently substituted here.
 */
export type TheatreProperty = "x"|"y"|"scale-x"|"scale-y"|"rotation"|"opacity"|"reveal"|"brightness";
export type TheatreShape = "group"|"line"|"polygon"|"glow"|"image"|"light"|"focus";
export type TheatreCommandKind = "sequence"|"parallel"|"during"|"repeat"|"loop"|"tween"|"animate"|
  "wait"|"say"|"caption-for"|"clear-caption"|"image"|"sound"|"stop-sound"|"volume";
export interface TheatreObject {
  id:string;parent:string;shape:TheatreShape;geometry:StoryValue; color:string;
  asset:string;light:string;initial:Partial<Record<TheatreProperty,number>>;
}
export interface TheatreCommand {
  kind:TheatreCommandKind;data:StoryValue[];children:TheatreCommand[];seconds:number;
  writes:string[];repeatCount?:number;
}
export interface TheatreProgram {
  width:number;height:number;background:string;objects:TheatreObject[];cue:TheatreCommand;
}
function error(where:string,why:string):never {throw new Error("theatre "+where+": "+why);}
function list(value:StoryValue,where:string):StoryValue[]{
  if(!Array.isArray(value))error(where,"expected list");
  return value;
}
function text(value:StoryValue,where:string):string {
  if(typeof value!=="string")error(where,"expected string");
  return value;
}
function id(value:StoryValue,where:string):string {
  const v=text(value,where);
  if(!v.trim())error(where,"empty ID");return v;
}
function num(value:StoryValue,where:string,positive=false):number {
  if(typeof value!=="number"||!Number.isFinite(value)||Math.abs(value)>10000||positive&&value<=0)
    error(where,"expected "+(positive?"positive ":"")+"finite number");
  return value;
}
function property(value:StoryValue):TheatreProperty {
  const p=text(value,"property");
  if(!["x","y","scale-x","scale-y","rotation","opacity","reveal","brightness"].includes(p))
    error("property","unknown "+p);
  return p as TheatreProperty;
}
function validateValue(prop:TheatreProperty,value:StoryValue):number{
  const n=num(value,prop);
  if((prop==="opacity"||prop==="reveal")&&(n<0||n>1) ||
     prop==="brightness"&&(n<0||n>4) ||
     (prop==="scale-x"||prop==="scale-y")&&n===0)
    error("property","out of range: "+prop);
  return n;
}
function points(raw:StoryValue,min:number):void {
  const seq=list(raw,"points");
  if(seq.length<min)error("points","expected at least "+min+" points");
  for(const p of seq){
    const xy=list(p,"point");if(xy.length!==2)error("point","expected x,y");
    num(xy[0],"point x");num(xy[1],"point y");
  }
}
function parseObject(raw:StoryValue, known:Map<string,TheatreObject>):TheatreObject {
  const parts=list(raw,"object");
  if(parts.length!==8)error("object","requires eight fields");
  const name=id(parts[0],"object ID"), parent=text(parts[1],"parent");
  const shape=text(parts[2],"shape") as TheatreShape;
  if(!["group","line","polygon","glow","image","light","focus"].includes(shape))
    error(name,"unknown shape "+shape);
  if(known.has(name))error(name,"duplicate object");
  if(parent){
    const node=known.get(parent);
    if(!node||!["group","light"].includes(node.shape))error(name,"parent must be earlier group/light");
  }
  if(shape==="focus"&&parent)error(name,"focus must be root");
  const geometry=parts[3],geom=list(geometry,"geometry");
  if(shape==="line"){
    if(geom.length!==2)error(name,"line width and points required");
    num(geom[0],"line width",true);points(geom[1],2);
    const line=geom[1] as StoryValue[];
    for(let i=1;i<line.length;i++){
      const a=line[i-1] as number[],b=line[i] as number[];
      if((a[0]-b[0])**2+(a[1]-b[1])**2<0.0001)error(name,"zero length line segment");
    }
  }else if(shape==="polygon")points(geometry,3);
  else if(["glow","image","focus"].includes(shape)){
    if(geom.length!==2)error(name,"geometry expects width,height");
    const w=num(geom[0],"width",true),h=num(geom[1],"height",true);
    if(shape==="focus"&&(w>=h||h>=1))error(name,"focus radii invalid");
  }else if(shape==="light"){
    if(geom.length!==1)error(name,"light radius required");
    num(geom[0],"light radius",true);
  }else if(geom.length!==0)error(name,"group cannot have geometry");
  const color=text(parts[4],"color");
  if(!/^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(color))
    error(name,"color must be #RRGGBB or #RRGGBBAA");
  const asset=text(parts[5],"asset"),light=text(parts[6],"light");
  if(shape==="image"){if(!asset.trim()||asset.startsWith("/")||asset.includes("..")||asset.includes("\\")||asset.includes(":"))error(name,"unsafe image asset");}
  else if(asset)error(name,"only image can have asset");
  if(shape!=="image"&&light)error(name,"only image may bind light");
  const initial:Partial<Record<TheatreProperty,number>>={};
  for(const rawProp of list(parts[7],"initial")){
    const pair=list(rawProp,"initial property");
    if(pair.length!==2)error(name,"initial property requires key/value");
    const key=property(pair[0]);
    if(key in initial)error(name,"duplicate initial property "+key);
    if(shape==="focus"&&!["x","y","opacity"].includes(key))error(name,"invalid focus property");
    if(key==="reveal"&&shape!=="line")error(name,"reveal only valid on lines");
    initial[key]=validateValue(key,pair[1]);
  }
  const value={id:name,parent,shape,geometry,color,asset,light,initial};
  known.set(name,value);
  return value;
}
function writes(c:TheatreCommand):string[] {return [...new Set([...c.writes,...c.children.flatMap(writes)])];}
function parseCue(raw:StoryValue, known:Map<string,TheatreObject>,
  budget:{used:number}, depth:number, background:boolean):TheatreCommand {
  if(depth>32||++budget.used>10000)error("program","complexity exceeds limit");
  const parts=list(raw,"cue");
  if(!parts.length)error("cue","empty");
  const kind=text(parts[0],"cue op") as TheatreCommandKind;
  const data=parts.slice(1),children:TheatreCommand[]=[];
  let seconds=0,ownWrites:string[]=[],repeatCount:number|undefined;
  if(["sequence","parallel","during","repeat","loop"].includes(kind)){
    if(!data.length)error(kind,"empty time structure");
    const items=kind==="repeat"?data.slice(1):data;
    if(kind==="repeat"){
      repeatCount=num(data[0],"repeat count",true);
      if(!Number.isSafeInteger(repeatCount)||repeatCount>10000||items.length!==1)
        error(kind,"requires count 1..10000 and one body");
    }
    if(kind==="loop"&&(data.length!==1||!background))
      error(kind,"loop requires a background in during");
    if(kind==="during"&&items.length<2)error(kind,"requires main and background");
    for(let i=0;i<items.length;i++)
      children.push(parseCue(items[i],known,budget,depth+1,
        background||(kind==="during"&&i>0)));
    if(["parallel","during"].includes(kind)){
      const set=new Set<string>();
      for(const child of children)for(const write of writes(child)){
        if(set.has(write))error(kind,"concurrent property conflict "+write);
        set.add(write);
      }
    }
    seconds=kind==="sequence"?children.reduce((sum,c)=>sum+c.seconds,0):
      kind==="during"?children[0].seconds:Math.max(...children.map(x=>x.seconds));
    if(kind==="repeat"||kind==="loop"){
      if(children[0].seconds<=0||!Number.isFinite(children[0].seconds))
        error(kind,"body must consume positive time");
      seconds=kind==="loop"?Number.POSITIVE_INFINITY:children[0].seconds*repeatCount!;
    }
  }else if(kind==="wait"){
    if(data.length!==1)error(kind,"expects seconds");seconds=num(data[0],"seconds",true);
  }else if(kind==="tween"||kind==="animate"){
    if(data.length!==3)error(kind,"expects id, property, keyframes");
    const target=id(data[0],"target"),prop=property(data[1]);
    const object=known.get(target);
    if(!object)error(kind,"unknown target "+target);
    if(prop==="reveal"&&object.shape!=="line")error(kind,"reveal requires line");
    if(object.shape==="focus"&&!["x","y","opacity"].includes(prop))error(kind,"invalid focus property");
    if(kind==="tween"){
      const targetValue=list(data[2],"tween target");
      if(targetValue.length!==3||!["linear","smooth"].includes(String(targetValue[2])))
        error(kind,"bad easing/target");
      seconds=num(targetValue[0],"seconds",true);validateValue(prop,targetValue[1]);
    }else{
      const keyframes=list(data[2],"keyframes");
      if(keyframes.length<2)error(kind,"at least two keyframes");
      let previous=-1;
      for(const rawKey of keyframes){
        const key=list(rawKey,"keyframe");
        if(key.length!==2)error(kind,"invalid keyframe");
        const time=num(key[0],"time");
        if(time<=previous||(previous===-1&&time!==0))error(kind,"key times invalid");
        validateValue(prop,key[1]);previous=time;
      }
      seconds=previous;
    }
    ownWrites=["object/"+target+"/"+prop];
  }else if(kind==="image"){
    if(data.length!==2||!known.has(String(data[0]))||
      known.get(String(data[0]))?.shape!=="image")error(kind,"target is not an image");
    id(data[1],"image asset");ownWrites=["object/"+String(data[0])+"/asset"];
  }else if(kind==="say"||kind==="caption-for"){
    if(data.length!==(kind==="say"?2:4))error(kind,"speaker/text arguments");
    text(data[0],"speaker");text(data[1],"caption");
    seconds=kind==="caption-for"?num(data[2],"duration",true):Number.POSITIVE_INFINITY;
    if(kind==="caption-for"){
      if(!/^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(text(data[3],"caption color")))
        error(kind,"color invalid");
    }
    ownWrites=["caption"];
  }else if(kind==="clear-caption"){
    if(data.length)error(kind,"takes no arguments");
    ownWrites=["caption"];
  }else if(kind==="sound"){
    if(data.length!==5)error(kind,"sound requires id asset loop volume pan");
    id(data[0],"sound id");id(data[1],"sound asset");
    if(typeof data[2]!=="boolean")error(kind,"loop must be boolean");
    const v=num(data[3],"volume"),p=num(data[4],"pan");
    if(v<0||v>1||p< -1||p>1)error(kind,"volume/pan out of range");
    ownWrites=["sound/"+String(data[0])];
  }else if(kind==="stop-sound"){
    if(data.length!==1)error(kind,"sound id required");
    ownWrites=["sound/"+id(data[0],"sound id")];
  }else if(kind==="volume"){
    if(data.length!==3)error(kind,"id volume seconds");
    id(data[0],"sound id");
    const n=num(data[1],"volume");if(n<0||n>1)error(kind,"volume out of range");
    seconds=num(data[2],"seconds",true);
    ownWrites=["sound/"+String(data[0])];
  }else error(kind,"unknown command");
  if(background&&["say","caption-for","clear-caption"].includes(kind))
    error(kind,"background subtitle not allowed");
  if(seconds>120&&Number.isFinite(seconds)&&children.length===0)
    error(kind,"single cue exceeds 120 seconds");
  return {kind,data,children,seconds,writes:ownWrites,repeatCount};
}
export function parseTheatre(args:StoryValue[]):TheatreProgram {
  if(args.length!==2)error("input","requires scene and program");
  const scene=list(args[0],"scene");
  if(scene.length!==4)error("scene","expected width,height,background,objects");
  const width=num(scene[0],"width",true),height=num(scene[1],"height",true);
  const background=text(scene[2],"background");
  if(!/^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(background))
    error("scene","invalid background color");
  const known=new Map<string,TheatreObject>();
  const objects=list(scene[3],"objects").map(raw=>parseObject(raw,known));
  if(!objects.length)error("scene","empty object list");
  for(const object of objects)if(object.light&&known.get(object.light)?.shape!=="light")
    error(object.id,"referenced light not found");
  const cue=parseCue(args[1],known,{used:0},0,false);
  return {width,height,background,objects,cue};
}
