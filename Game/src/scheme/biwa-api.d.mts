export class SchemePair {
  constructor(car:unknown,cdr:unknown);
  car:unknown;
  cdr:unknown;
}
export interface SchemeSymbol {name:string}
export interface BiwaInterpreter {
  on_error:(error:unknown)=>void;
  evaluate(source:string,done:(value:unknown)=>void):unknown;
}
export interface BiwaPause {resume(value:unknown):void}
export interface BiwaRuntime {
  TopEnv:Record<string,unknown>;
  CoreEnv:Record<string,unknown>;
  Pair:typeof SchemePair;
  nil:unknown;
  undef:unknown;
  Symbols:Record<string,SchemeSymbol>;
  Symbol:new(name:string)=>SchemeSymbol;
  Sym(name:string):SchemeSymbol;
  List(...items:unknown[]):unknown;
  isSymbol(value:unknown):value is SchemeSymbol;
  to_display(value:unknown):string;
  Interpreter:new(onError:(error:unknown)=>void)=>BiwaInterpreter;
  Pause:new(start:(pause:BiwaPause)=>void)=>unknown;
  Compiler:{transform_internal_define:(body:unknown)=>unknown};
}
export function createRuntime(shared?:BiwaRuntime):BiwaRuntime;
export const values:BiwaRuntime;
