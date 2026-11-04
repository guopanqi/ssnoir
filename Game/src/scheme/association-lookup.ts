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
