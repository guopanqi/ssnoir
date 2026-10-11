// 把 src/ 拼成单文件 dist/portrait-theatre-story-claude-sonnet-5-5.html（无依赖，node >= 18）
// 用法: node build.mjs        （--check 只校验不写文件）
import { readFileSync, writeFileSync, readdirSync, mkdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const root = dirname(fileURLToPath(import.meta.url));
const src = p => join(root, 'src', p);
const read = p => readFileSync(src(p), 'utf8');
const sorted = dir => readdirSync(src(dir)).filter(f => f.endsWith('.js')).sort();

// 脚本拼接顺序（全局作用域，顺序即依赖顺序）：
// core → world → cast → runtime → fx → sets → 第1、9章对象定义(C1,C7) → SC 数组(chapters/*.js 按文件名排序) → boot
const top = sorted('js').filter(f => f < '50');
const boot = sorted('js').filter(f => f >= '60');
const chapters = sorted('js/chapters');

let js = top.map(f => read('js/' + f)).join('');
js += 'const SC=[\n' + chapters.map(f => read('js/chapters/' + f)).join('') + '];\n';
js += boot.map(f => read('js/' + f)).join('');

const html = read('index.template.html')
  .replace('/*@STYLE*/', () => read('style.css').trimEnd())
  .replace('/*@SCRIPT*/', () => js.trimEnd());

const out = join(root, 'dist', 'portrait-theatre-story-claude-sonnet-5-5.html');
if (process.argv.includes('--check')) {
  mkdirSync(join(root, '.tmp'), { recursive: true });
  writeFileSync(join(root, '.tmp', 'check.js'), js);
  execFileSync('node', ['--check', join(root, '.tmp', 'check.js')], { stdio: 'inherit' });
  console.log('syntax ok,', chapters.length, 'chapters');
} else {
  mkdirSync(dirname(out), { recursive: true });
  writeFileSync(out, html);
  console.log('built', out, (html.length / 1024).toFixed(1) + 'KB');
}
