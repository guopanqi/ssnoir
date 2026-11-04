import {createRuntime,values} from '../../.generated/biwa-isolated.mjs';
import {installInternalDefinitions} from './internal-definitions.ts';

/** One isolated compiler/global environment per world or scenario. Evaluation
 * stacks are per call, including a suspended native load-file. Definitions and
 * live closures persist in the owning VM; native state belongs to its caller.
 */
export class CompiledSession {
  private readonly runtime=createRuntime(values);

  readonly name:string;
  constructor(name:string,globals:Record<string,unknown>={}) {
    this.name=name;
    const b=this.runtime;
    installInternalDefinitions(b);
    for(const [key,value] of Object.entries(globals)){
      b.TopEnv[key]=typeof value==='function'?(args:unknown[],interpreter:{on_error:(error:unknown)=>void})=>{
        const result=value(...args);
        if(result instanceof Promise)return new b.Pause(pause=>{
          result.then(v=>pause.resume(v===undefined?b.undef:v),interpreter.on_error);
        });
        return result===undefined?b.undef:result;
      }:value;
    }
    b.TopEnv['error']=(args:unknown[])=>{
      if(!args.length||typeof args[0]!=='string')throw new Error('error expects a message string');
      throw new Error(args.map(x=>b.to_display(x)).join(' '));
    };
    b.TopEnv['bound?']=(args:unknown[])=>{
      if(args.length!==1||!b.isSymbol(args[0]))throw new Error('bound? expects one symbol');
      return Object.hasOwn(b.TopEnv,args[0].name)||Object.hasOwn(b.CoreEnv,args[0].name);
    };
  }

  async evaluate(source:string):Promise<unknown>{
    const b=this.runtime;
    return new Promise((resolve,reject)=>{
      const interpreter=new b.Interpreter(reject);
      try{interpreter.evaluate(source,v=>resolve(v===b.undef?undefined:v));}
      catch(error){reject(error);}
    });
  }
}
