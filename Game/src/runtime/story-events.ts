import { unbox } from "lips";
import { schemeList } from "./node-converter.ts";

/** Presentation intent, not rendered UI. Used by Web, Electron and both Mini Game hosts. */
export type StoryValue=string|number|boolean|null|StoryValue[];
export type StoryKind="narration"|"spotlight"|"dialogue"|"remote-dialogue"|
  "banter"|"remote-banter"|"theatre"|"stage"|"video"|"motion"|"supplement";
export interface StoryEvent {
  kind:StoryKind;
  args:StoryValue[];
  blocking:boolean;
  immediate:boolean;
}

function convert(value:unknown,depth=0):StoryValue {
  if(depth>32)throw new Error("Scheme story structure too deep");
  if(value===null||value===undefined)return null;
  if(value===false||value===true)return value;
  const raw=unbox(value);
  if(typeof raw==="string"||typeof raw==="boolean"||typeof raw==="number") {
    if(typeof raw==="number"&&!Number.isFinite(raw))throw new Error("story number must be finite");
    return raw;
  }
  if(typeof raw==="bigint") {
    const n=Number(raw);if(!Number.isSafeInteger(n))throw new Error("story bigint unsafe");
    return n;
  }
  if(String(value)==="()")return [];
  if(typeof value==="object"&&value!==null&&"car" in value&&"cdr" in value) {
    return schemeList(value,"story sequence").map(v=>convert(v,depth+1));
  }
  // Scheme symbols are sent as stable human-readable author tags.
  const name=String(value);
  if(name==="[object Object]")throw new Error("unsupported story value");
  return name;
}
export function storyArguments(args:unknown[]):StoryValue[] {
  if(args.length>10000)throw new Error("story payload too long");
  return args.map(a=>convert(a));
}
