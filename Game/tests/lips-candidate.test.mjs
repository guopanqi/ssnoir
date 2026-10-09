import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { Interpreter } from "lips";

function make(name) {
  return Interpreter(name);
}
async function value(interpreter, code) {
  const results = await interpreter.exec(code);
  return results[results.length - 1];
}

test("LIPS candidate: isolated world/encounter top-level globals", async () => {
  const world = make("SSNoir-world");
  const encounter = make("SSNoir-encounter");
  await world.exec("(define stage-local 17)");
  await encounter.exec("(define stage-local 2)");
  assert.equal(String(await value(world, "stage-local")), "17");
  assert.equal(String(await value(encounter, "stage-local")), "2");
});

test("LIPS candidate: persistent closure mutation", async () => {
  const world = make("SSNoir-closure");
  await world.exec("(define next! (let ((n 0)) (lambda () (set! n (+ n 1)) n)))");
  assert.equal(String(await value(world, "(next!)")), "1");
  assert.equal(String(await value(world, "(next!)")), "2");
});

test("LIPS candidate: standard Scheme truth and macro", async () => {
  const vm = make("SSNoir-semantic");
  assert.equal(String(await value(vm, "(if '() 42 0)")), "42");
  assert.equal(String(await value(vm, "(if 0 42 0)")), "42");
  await vm.exec("(define x 0)");
  await vm.exec("(define-macro (inc!) '(set! x (+ x 1)))");
  assert.equal(String(await value(vm, "(begin (inc!) x)")), "1");
});


test("LIPS candidate: real SSNoir stdlib unmodified", async () => {
  const vm = make("SSNoir-stdlib");
  // LIPS documents that its Scheme stdlib must be bootstrapped explicitly.
  // Load from the installed NPM package, not from a network URL.
  const standardLibrary = resolve("node_modules/lips/dist/std.xcb").replaceAll("\\", "/");
  await vm.exec(`(let-env lips.env.__parent__ (load "${standardLibrary}"))`);
  const source = readFileSync("../UnityClient/Assets/Resources/Content/scripts/stdlib.scm", "utf8");
  await vm.exec(source);
  assert.equal(String(await value(vm, '(assoc-get (list (list "day" 14)) "day" -1)')), "14");
  assert.equal(String(await value(vm, "(length (filter (lambda (x) (> x 2)) '(1 2 3 4)))")), "2");
});

test("LIPS candidate: world and encounter share native state but not bindings", async () => {
  const world = make("SSNoir-world-isolation-2");
  const encounter = make("SSNoir-encounter-isolation-2");
  await world.exec("(define event-stage 11)");
  await encounter.exec("(define event-stage 7)");
  await world.exec("(set! event-stage 12)");
  assert.equal(String(await value(world, "event-stage")), "12");
  assert.equal(String(await value(encounter, "event-stage")), "7");
});
