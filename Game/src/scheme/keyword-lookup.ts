import {Pair, nil, type} from "lips";

type SchemeFn=(...args:unknown[])=>unknown;

/**
 * Exact fast path for the author's (get-kwarg kwargs key default).
 * During world materialization every (node ...) probes many keyword positions.
 * The original recursive Scheme helper takes multiple interpreted frames per
 * probe. This version walks the SAME live Pair list; it does not cache
 * values, evaluate outcomes, or alter the authored node tree.
 *
 * Non-symbol keys and malformed lists delegate to the original helper, so
 * unusual equal? relations and original error handling remain authoritative.
 */
export function accelerateKeywordLookup(original:SchemeFn):SchemeFn {
  return (...args:unknown[]):unknown=>{
    if(args.length!==3)return original(...args);
    const [kwargs,key,fallback]=args;
    if(type(key)!=="symbol")return original(...args);
    const wanted=String(key);
    let cursor=kwargs;
    const visited=new Set<Pair>();
    while(cursor!==nil){
      if(!(cursor instanceof Pair))return original(...args);
      if(visited.has(cursor))return original(...args);
      visited.add(cursor);
      const next=cursor.cdr;
      if(next===nil)return fallback; // odd trailing keyword
      if(!(next instanceof Pair))return original(...args);
      if(type(cursor.car)==="symbol"&&String(cursor.car)===wanted)
        return next.car;
      cursor=next.cdr;
    }
    return fallback;
  };
}
