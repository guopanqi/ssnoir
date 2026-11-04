import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
const root=new URL('../',import.meta.url);
const source=readFileSync(new URL('node_modules/biwascheme/release/biwascheme.mjs',root),'utf8');
const expected='e34e2f7dcdb2e128d7457a2481c009bdb17d5d732af7de54fdc6cdf6b9b6f16b';
if(createHash('sha256').update(source).digest('hex')!==expected)
  throw new Error('BiwaScheme 0.8.3 source changed; review isolation/expansion adapter before upgrading');
let code=source.slice(0,source.indexOf('\n};',source.indexOf('var BiwaScheme$1 = {'))+3);
function replace(before,after){
  if(code.split(before).length!==2)throw new Error('VM build anchor changed: '+before);
  code=code.replace(before,after);
}
// Each factory owns TopEnv, CoreEnv, compiler, macros and interpreter state.
// Only value constructors/singletons are shared so native lists cross the bridge.
replace('const nil = {','const nil = shared?.nil ?? {');
replace('return "nil";','return "()";');
replace('const undef = new Object();','const undef = shared?.undef ?? new Object();');
replace('const Symbols = {};','const Symbols = shared?.Symbols ?? {};');
replace('class BiwaSymbol {','const BiwaSymbol = shared?.Symbol ?? class BiwaSymbol {');
replace('class Pair {','const Pair = shared?.Pair ?? class Pair {');
// Host display convention; Scheme write/display still use to_write/to_display.
replace('  toString() {\n    return "\'" + this.name;','  toString() {\n    return this.name;');
// Expand function sugar and local definitions BEFORE closure analysis. The
// normalizer is installed by CompiledSession before any authored code runs.
replace('ret = new Pair(Sym("define"), new Pair(left, expand(exp, flag)));',
  `ret = left instanceof Pair
    ? expand(List(Sym("define"), left.car, new Pair(Sym("lambda"), new Pair(left.cdr, exp))), flag)
    : new Pair(Sym("define"), new Pair(left, expand(exp, flag)));`);
replace('ret = new Pair(Sym("lambda"), new Pair(vars, expand(body, flag)));',
  `const normalized = Compiler.transform_internal_define(body);
        if (normalized !== body) body = List(normalized);
        ret = new Pair(Sym("lambda"), new Pair(vars, expand(body, flag)));`);
// Schemy and the original Web VM evaluate operator/arguments left to right.
// Biwa's default pushes them in reverse. Keep its calling convention but
// reorder the completed call frame once, before apply/tail-shift.
const callStart=code.indexOf('            var func = x.car;');
const callEnd=code.indexOf('            return this.is_tail(next) ? c : ["frame", c, next];',callStart);
if(callStart<0||callEnd<callStart)throw new Error('Application compiler anchor changed');
code=code.slice(0,callStart)+`
            var func = x.car, args = x.cdr;
            var count = args.length();
            var c = ["ordered-call", count, this.is_tail(next)
              ? ["shift", count, ["tco_hinted_apply"]] : ["apply"]];
            for (const arg of args.to_array().reverse())
              c = this.compile(arg, e, s, f, ["argument", c]);
            c = this.compile(func, e, s, f, ["argument", c]);
`+code.slice(callEnd);
replace('        case "halt":', `        case "ordered-call": {
          const count = x[1], base = s - count - 1;
          const proc = this.stack[base];
          for (let i = 0; i < Math.floor(count / 2); i++) {
            const left = base + 1 + i, right = s - 1 - i;
            [this.stack[left], this.stack[right]] = [this.stack[right], this.stack[left]];
          }
          for (let i = 0; i < count; i++) this.stack[base + i] = this.stack[base + 1 + i];
          this.stack[s - 1] = count;
          a = proc;
          x = x[2];
          break;
        }
        case "halt":`);
replace('const Console = {};','const Console = {puts:(text)=>console.info(text),p:(...items)=>console.info(...items)};');
const out=new URL('.generated/',root);mkdirSync(out,{recursive:true});
writeFileSync(new URL('biwa-isolated.mjs',out),
  '// Generated from pinned MIT BiwaScheme 0.8.3. See scripts/build-scheme-vm.mjs.\n'+
  'export function createRuntime(shared) {\n'+code+'\nreturn {...BiwaScheme$1,Symbols};\n}\nexport const values=createRuntime();\n');
writeFileSync(new URL('biwa-isolated.d.mts',out),'export * from "../src/scheme/biwa-api.mjs";\n');
