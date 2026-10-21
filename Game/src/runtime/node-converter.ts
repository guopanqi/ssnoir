import { unbox } from "lips";

interface SchemePair { car: unknown; cdr: unknown }
function isSchemePair(value: unknown): value is SchemePair {
  return !!value && typeof value === "object" && "car" in value && "cdr" in value;
}
function isEmptyList(value: unknown): boolean {
  return value !== null && value !== undefined && !isSchemePair(value) && String(value) === "()";
}

/**
 * Port of Engine/Runtime/Scripting/NodeConverter.cs for LIPS value objects.
 * No presentation, random choices or effects run while materializing a node.
 */
export interface ActionCost { type: "die" | "item"; itemId?: string; qty?: number }
export interface RuntimeClock {
  label:string; displayLabel:string; note:string; current:number; max:number;
  style:"gauge"|"pressure"|"countdown"|"readout";
  priority?:"primary"|"secondary"; beneficialDirection?:"increase"|"decrease"|"neutral";
}
export type ResolveKind = "instant" | "roll" | "observe" | "note";
export interface NodeResolve {
  kind: ResolveKind;
  skill?: string;
  text?: string;
  noteTitle?: string;
  noteText?: string;
  noteClock?: RuntimeClock;
  effect?: unknown;
  fail?: unknown;
  neutral?: unknown;
  success?: unknown;
}
export interface RuntimeArrival { id: string; effect: unknown }
export interface RuntimeNode {
  name: string;
  title: string;
  subtitle: string;
  anchor: string | null;
  isPlace: boolean;
  disabled: boolean;
  tags: string[];
  requires: ActionCost[];
  clocks: RuntimeClock[];
  carryItemId:string;
  supportId:string;
  children: RuntimeNode[];
  arrivals: RuntimeArrival[];
  resolve: NodeResolve | null;
}

/** Bound the conversion to avoid infinite traversal of cyclic Scheme content. */
export function schemeList(value: unknown, where = "list"): unknown[] {
  const items: unknown[] = [];
  let cursor: unknown = value;
  const visited = new Set<object>();
  while (!isEmptyList(cursor)) {
    if (!isSchemePair(cursor)) throw new Error(where + ": expected proper Scheme list");
    if (visited.has(cursor)) throw new Error(where + ": cyclic list");
    visited.add(cursor);
    items.push(cursor.car);
    if (items.length > 50000) throw new Error(where + ": list exceeds conversion limit");
    cursor = cursor.cdr;
  }
  return items;
}
function scalar(value: unknown): unknown { return unbox(value); }
function name(value: unknown, where: string): string {
  const v = scalar(value);
  if (typeof v === "string" && v.trim()) return v;
  throw new Error(where + ": expected nonempty string");
}
function symbol(value: unknown): string {
  // LIPS symbols have stable toString regardless of class-name minification.
  return String(value);
}
function optionalList(value: unknown, label: string): unknown[] {
  return value === false ? [] : schemeList(value, label);
}
function requiredText(value:unknown, context:string):string {
  const text=scalar(value);
  if(typeof text!=="string")throw new Error(context+": expected string");
  return text;
}
/**
 * Port of C# NodeConverter.ParseSingleClock. These are semantic styles,
 * not hardcoded visual progress bars. A readout never implies a deadline.
 */
export function convertClock(raw:unknown):RuntimeClock {
  const parts=schemeList(raw,"clock");
  if(parts.length<5||parts.length>9||symbol(parts[0])!=="clock")
    throw new Error("clock: expected (clock label current max style [note] [display label])");
  const label=requiredText(parts[1],"clock label");
  const current=Number(String(parts[2])),max=Number(String(parts[3]));
  if(!Number.isSafeInteger(current)||!Number.isSafeInteger(max))
    throw new Error("clock: current and max must be integers");
  const style=symbol(parts[4]);
  if(style!=="gauge"&&style!=="pressure"&&style!=="countdown"&&style!=="readout")
    throw new Error("clock "+label+": unknown style "+style);
  const note=parts.length>=6?requiredText(parts[5],"clock note"):"";
  const displayLabel=parts.length>=7?name(parts[6],"clock display label"):label;
  const priority=parts.length>=8?symbol(parts[7]):undefined;
  const beneficialDirection=parts.length===9?symbol(parts[8]):undefined;
  if(priority!==undefined && priority!=="primary" && priority!=="secondary")
    throw new Error("clock "+label+": invalid priority "+priority);
  if(beneficialDirection!==undefined && !["increase","decrease","neutral"].includes(beneficialDirection))
    throw new Error("clock "+label+": invalid beneficial direction "+beneficialDirection);
  return {
    label,current,max,style,note,displayLabel,
    ...(priority===undefined?{}:{priority:priority as "primary"|"secondary"}),
    ...(beneficialDirection===undefined?{}:{beneficialDirection:beneficialDirection as "increase"|"decrease"|"neutral"})
  };
}
function clocks(value:unknown, context:string):RuntimeClock[] {
  return optionalList(value,context).map(convertClock);
}
function outcome(value: unknown, context: string): unknown {
  if (isSchemePair(value)) {
    const parts = schemeList(value, context);
    if (parts.length !== 2 || symbol(parts[0]) !== "outcome") {
      throw new Error(context + ": expected (outcome effect)");
    }
    return parts[1];
  }
  if (value === false || value === undefined || value === null) throw new Error(context + ": missing effect");
  return value; // Schemy accepts direct procedure for older authored content.
}
function parseResolve(raw: unknown): NodeResolve | null {
  if (raw === false || isEmptyList(raw)) return null;
  const value = schemeList(raw, "resolve");
  const kind = symbol(value[0]);
  switch (kind) {
    case "instant":
      if (value.length !== 2) throw new Error("instant requires one outcome");
      return { kind, effect: outcome(value[1], "instant") };
    case "roll":
    case "recovery-roll": {
      if (value.length !== 6) throw new Error("roll requires skill, modifier and 3 outcomes");
      return {
        kind: "roll", skill: symbol(value[1]),
        // modifier proc is metadata-only; it will be run during action setup.
        fail: outcome(value[3], "roll fail"),
        neutral: outcome(value[4], "roll neutral"),
        success: outcome(value[5], "roll success")
      };
    }
    case "observe":
      if (value.length !== 2) throw new Error("observe expects text");
      return { kind: "observe", text: String(scalar(value[1])) };
    case "note":
      if (value.length < 3 || value.length > 4) throw new Error("note expects title/text/optional clock");
      const noteClock=value.length===4?convertClock(value[3]):undefined;
      const noteTitle=requiredText(value[1],"note title"),noteText=requiredText(value[2],"note body");
      if(!noteClock&&!noteTitle.trim()&&!noteText.trim())
        throw new Error("empty note without clock");
      return { kind:"note", noteTitle, noteText, noteClock };
    case "clock":
      if (value.length !== 2) throw new Error("clock note requires one clock");
      const c=convertClock(value[1]);
      return {kind:"note",noteTitle:c.displayLabel,noteText:c.note,noteClock:c};
    default: throw new Error("unsupported resolve kind: " + kind);
  }
}
function parseCosts(raw: unknown): ActionCost[] {
  return optionalList(raw, "requires").map(v => {
    const elements = schemeList(v, "requirement");
    const kind = symbol(elements[0]);
    if (kind === "die" && elements.length === 1) return { type: "die" };
    if (kind === "item" && elements.length === 3) {
      const qty = Number(String(elements[2]));
      if (!Number.isSafeInteger(qty) || qty <= 0) throw new Error("item requirement needs positive integer qty");
      return { type: "item", itemId: name(elements[1], "item id"), qty };
    }
    throw new Error("unknown requirement: " + kind);
  });
}
export function convertNode(value: unknown): RuntimeNode {
  const expr = schemeList(value, "node");
  if (expr.length < 2 || symbol(expr[0]) !== "node" || (expr.length - 2) % 2) {
    throw new Error("node: malformed keyword expression");
  }
  const id = name(expr[1], "node name");
  const node: RuntimeNode = {
    name: id, title: id, subtitle: "", anchor: null, isPlace: false, disabled: false,
    tags: [], requires: [], clocks: [], children: [], arrivals: [], resolve: null,
    carryItemId:"", supportId:""
  };
  for (let i = 2; i < expr.length; i += 2) {
    const kw = symbol(expr[i]);
    const val = expr[i + 1];
    switch (kw) {
      case ":title": node.title = name(val, id + " title"); break;
      case ":name": node.title = name(val, id + " display name"); break;
      case ":required-before-rest": requiredText(val, id + " rest requirement"); break;
      case ":button-text": requiredText(val, id + " button"); break;
      case ":subtitle": node.subtitle = String(scalar(val)); break;
      case ":anchor": node.anchor = name(val, id + " anchor"); break;
      case ":place": node.isPlace = val === true; break;
      case ":disabled": node.disabled = val === true; break;
      case ":tags": node.tags = optionalList(val, "tags").map(x => String(scalar(x))); break;
      case ":requires": node.requires = parseCosts(val); break;
      case ":clocks": node.clocks = clocks(val,"clocks"); break;
      case ":children": node.children = optionalList(val, "children").map(convertNode); break;
      case ":resolve": node.resolve = parseResolve(val); break;
      case ":arrivals": {
        node.arrivals=optionalList(val,"arrivals").map((raw)=>{
          const parts=schemeList(raw,id+" arrival");
          if(parts.length!==3||symbol(parts[0])!=="arrival")
            throw new Error(id+": malformed arrival");
          return {id:name(parts[1],"arrival id"),effect:parts[2]};
        });
        if(!node.isPlace && node.arrivals.length)
          throw new Error(id+": arrivals only allowed on places");
        const seen=new Set<string>();
        for(const arrival of node.arrivals){
          if(seen.has(arrival.id))throw new Error(id+": duplicate arrival "+arrival.id);
          seen.add(arrival.id);
        }
        break;
      }
      case ":carry-item": node.carryItemId=name(val,kw); break;
      case ":support": node.supportId=name(val,kw); break;
      default: throw new Error(id + ": unknown node keyword " + kw);
    }
  }
  if (node.arrivals.length&&!node.isPlace)throw new Error(id+": arrivals require place");
  if (node.resolve && node.children.length) throw new Error(id + ": cannot have both children and resolve");
  if (node.isPlace && node.resolve) throw new Error(id + ": place cannot be an action");
  return node;
}
export function convertNodes(value: unknown): RuntimeNode[] {
  const nodes = schemeList(value, "nodes").map(convertNode);
  const seen = new Set<string>();
  const check = (node: RuntimeNode) => {
    if (seen.has(node.name)) throw new Error("Duplicate node name: " + node.name);
    seen.add(node.name);
    node.children.forEach(check);
  };
  nodes.forEach(check);
  return nodes;
}
