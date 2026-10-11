import {unbox} from "lips";
import {schemeList,convertClock,type RuntimeClock} from "./node-converter.ts";

export interface DossierStep {text:string;done:boolean}
export interface DossierLog {day:number;text:string}
export interface DossierEntry {
  id:string;kind:string;primary:boolean;status:string;now:string;where:string;
  clocks:RuntimeClock[];steps:DossierStep[];log:DossierLog[];
}
function string(value:unknown,where:string):string {
  const x=unbox(value);
  if(typeof x!=="string")throw new Error(where+": expected string");
  return x;
}
function symbol(value:unknown):string {return String(value);}
function number(value:unknown,where:string):number {
  const n=Number(String(value));
  if(!Number.isSafeInteger(n))throw new Error(where+": expected integer");
  return n;
}
function list(raw:unknown,where:string):unknown[]{return raw===false?[]:schemeList(raw,where);}
function entry(raw:unknown):DossierEntry {
  const p=schemeList(raw,"dossier entry");
  if(p.length<2||symbol(p[0])!=="dossier"||(p.length-2)%2)throw new Error("malformed dossier");
  const id=string(p[1],"dossier id");
  if(!id.trim())throw new Error("empty dossier id");
  const value:DossierEntry={id,kind:"委托",primary:false,status:"进行中",now:"",where:"",
    clocks:[],steps:[],log:[]};
  for(let i=2;i<p.length;i+=2){
    const kw=symbol(p[i]),rawValue=p[i+1];
    switch(kw){
      case ":kind":value.kind=String(rawValue);break;
      case ":primary":
        if(rawValue!==true&&rawValue!==false)throw new Error(id+": primary must be boolean");
        value.primary=rawValue;break;
      case ":status":value.status=String(rawValue);break;
      case ":now":value.now=string(rawValue,id+" now");break;
      case ":where":value.where=string(rawValue,id+" where");break;
      case ":clocks":value.clocks=list(rawValue,id+" clocks").map(convertClock);break;
      case ":steps":value.steps=list(rawValue,id+" steps").map(row=>{
        const item=schemeList(row,"dossier step");
        if(item.length!==3||symbol(item[0])!=="step"||
           (item[2]!==true&&item[2]!==false))throw new Error("malformed dossier step");
        const text=string(item[1],"step text");
        if(!text.trim())throw new Error("empty dossier step");
        return {text,done:item[2] as boolean};
      });break;
      case ":log":value.log=list(rawValue,id+" log").map(row=>{
        const item=schemeList(row,"dossier journal");
        if(item.length!==3||symbol(item[0])!=="journal-entry")throw new Error("malformed journal entry");
        return {day:number(item[1],"journal day"),text:string(item[2],"journal text")};
      });break;
      default:throw new Error(id+": unknown dossier keyword "+kw);
    }
  }
  return value;
}
/** Read-only, fresh per-frame authored dossier snapshot (not a second source of truth). */
export function convertDossier(raw:unknown):DossierEntry[] {
  const seen=new Set<string>();
  return list(raw,"dossier").map(row=>{
    const value=entry(row);
    if(seen.has(value.id))throw new Error("duplicate dossier id "+value.id);
    seen.add(value.id);
    return value;
  });
}
