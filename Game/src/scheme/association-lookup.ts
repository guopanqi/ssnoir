import { Pair, nil, type } from "lips";

type Lookup = (...args: unknown[]) => unknown;

/**
 * String keys dominate authored save/load. Avoid interpreting assoc-get's
 * recursion and LIPS's Scheme equal? for every row of every module.
 * Other key types retain the original helper's full equal? semantics.
 * No index is cached: Scheme may mutate keys, rows or list tails in place.
 */
export function accelerateAssociationLookup(original: Lookup): Lookup {
  return (...args: unknown[]): unknown => {
    if (args.length !== 3) throw new Error("assoc-get expects 3 arguments");
    const [alist, key, fallback] = args;
    if (type(key) !== "string") return original(...args);
    const text = String(key);
    const visited = new Set<Pair>();
    let cursor = alist;
    while (cursor !== nil) {
      if (!(cursor instanceof Pair)) throw new Error("assoc-get: expected proper association list");
      if (visited.has(cursor)) throw new Error("assoc-get: cyclic association list");
      visited.add(cursor);
      const row = cursor.car;
      if (!(row instanceof Pair)) throw new Error("assoc-get: expected association row");
      if (type(row.car) === "string" && String(row.car) === text) {
        if (!(row.cdr instanceof Pair)) throw new Error("assoc-get: missing association value");
        return row.cdr.car;
      }
      cursor = cursor.cdr;
    }
    return fallback;
  };
}


/**
 * Authored member? is a recursive Scheme list walk. On the world render path,
 * symbol/string membership checks run repeatedly while producing location and
 * character cards. Fast-path only scalar values whose Scheme equal? relation
 * is an exact same-kind text comparison; delegate all other types unchanged.
 * Reading live Pair links on each invocation keeps mutated authored data valid.
 */
export function accelerateMembershipLookup(original: Lookup): Lookup {
  return (...args: unknown[]): unknown => {
    if (args.length !== 2) return original(...args);
    const [needle, haystack] = args;
    const kind = type(needle);
    if (kind !== "string" && kind !== "symbol") return original(...args);
    const value = String(needle);
    const visited = new Set<Pair>();
    let cursor = haystack;
    while (cursor !== nil) {
      if (!(cursor instanceof Pair)) throw new Error("member?: expected proper list");
      if (visited.has(cursor)) throw new Error("member?: cyclic list");
      visited.add(cursor);
      const item = cursor.car;
      if (type(item) === kind && String(item) === value) return true;
      cursor = cursor.cdr;
    }
    return false;
  };
}
