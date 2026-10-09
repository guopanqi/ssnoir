import test from "node:test";
import assert from "node:assert/strict";
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
