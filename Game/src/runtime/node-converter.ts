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
export type ResolveKind = "instant" | "roll" | "observe" | "note";
export interface NodeResolve {
  kind: ResolveKind;
  skill?: string;
  text?: string;
  noteTitle?: string;
  noteText?: string;
  effect?: unknown;
  fail?: unknown;
  neutral?: unknown;
  success?: unknown;
}
export interface RuntimeNode {
  name: string;
  title: string;
  subtitle: string;
  anchor: string | null;
  isPlace: boolean;
  disabled: boolean;
  tags: string[];
  requires: ActionCost[];
  clocks: unknown[];
  children: RuntimeNode[];
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
      return { kind: "note", noteTitle: String(scalar(value[1])), noteText: String(scalar(value[2])) };
    case "clock":
      if (value.length !== 2) throw new Error("clock note requires one clock");
      return { kind: "note", noteTitle: String(value[1]) };
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
    tags: [], requires: [], clocks: [], children: [], resolve: null
  };
  for (let i = 2; i < expr.length; i += 2) {
    const kw = symbol(expr[i]);
    const val = expr[i + 1];
    switch (kw) {
      case ":title": node.title = name(val, id + " title"); break;
      case ":subtitle": node.subtitle = String(scalar(val)); break;
      case ":anchor": node.anchor = name(val, id + " anchor"); break;
      case ":place": node.isPlace = val === true; break;
      case ":disabled": node.disabled = val === true; break;
      case ":tags": node.tags = optionalList(val, "tags").map(x => String(scalar(x))); break;
      case ":requires": node.requires = parseCosts(val); break;
      case ":clocks": node.clocks = optionalList(val, "clocks"); break;
      case ":children": node.children = optionalList(val, "children").map(convertNode); break;
      case ":resolve": node.resolve = parseResolve(val); break;
      case ":arrivals":
        // A future arrival executor must retain live closures; do not silently
        // accept an authored arrival script we cannot execute.
        if (optionalList(val, "arrivals").length) throw new Error(id + ": arrival executor not migrated");
        break;
      case ":carry-item":
      case ":support":
        // Carrier/support metadata is non-executable; represented separately
        // in the C# presentation model, tracked in a later migration.
        name(val, kw);
        break;
      default: throw new Error(id + ": unknown node keyword " + kw);
    }
  }
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
