import type {BiwaRuntime,SchemePair} from './biwa-api.mjs';

/** Lower SSNoir's mixed local definitions before closure/free-variable analysis.
 * Initializers stay at their authored positions; only binding slots are hoisted.
 * This avoids Biwa treating definitions after an expression as global writes.
 */
export function installInternalDefinitions(runtime:BiwaRuntime):void {
  const {Pair,nil,Sym,List}=runtime;
  runtime.Compiler.transform_internal_define=(body:unknown)=>{
    const names:unknown[]=[],forms:unknown[]=[];
    const visit=(items:unknown)=>{
      for(let p=items;p!==nil;){
        if(!(p instanceof Pair))throw new Error('Expected a proper Scheme body');
        const form=p.car;
        if(form instanceof Pair&&form.car===Sym('begin'))visit(form.cdr);
        else forms.push(form);
        p=p.cdr;
      }
    };
    const pair=(value:unknown):SchemePair=>{
      if(!(value instanceof Pair))throw new Error('Malformed local definition');
      return value;
    };
    visit(body);
    const lowered=forms.map(form=>{
      if(!(form instanceof Pair)||form.car!==Sym('define'))return form;
      const args=pair(form.cdr),target=args.car,rest=args.cdr;
      const name=target instanceof Pair?target.car:target;
      if(!runtime.isSymbol(name))throw new Error('Invalid local definition');
      if(!names.includes(name))names.push(name);
      const value=target instanceof Pair?new Pair(Sym('lambda'),new Pair(target.cdr,rest)):
        rest===nil?runtime.undef:pair(rest).car;
      if(!(target instanceof Pair)&&rest!==nil&&pair(rest).cdr!==nil)
        throw new Error('Local define expects one value');
      return List(Sym('set!'),name,value);
    });
    if(!names.length)return body;
    return new Pair(Sym('let'),new Pair(List(...names.map(n=>List(n,false))),List(...lowered)));
  };
}
