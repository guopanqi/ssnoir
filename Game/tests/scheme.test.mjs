import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import BiwaScheme from "biwascheme";

function interpreter() {
  return new BiwaScheme.Interpreter(error => { throw error; });
}
function evaluate(vm, code) {
  let done = false;
  let result;
  vm.evaluate(code, value => { done = true; result = value; });
  assert.equal(done, true, "Scheme probe must finish synchronously");
  return result;
}

test("BiwaScheme arithmetic and closure semantics", () => {
  assert.equal(evaluate(interpreter(), "(let ((x 12)) ((lambda (y) (+ x y)) 30))"), 42);
});

test("BiwaScheme truth: only #f is false", () => {
  assert.equal(evaluate(interpreter(), "(if '() 42 0)"), 42);
  assert.equal(evaluate(interpreter(), "(if 0 42 0)"), 42);
});

test("SSNoir real stdlib loads unchanged", () => {
  const vm = interpreter();
  const script = readFileSync("../UnityClient/Assets/Resources/Content/scripts/stdlib.scm", "utf8");
  evaluate(vm, script);
  assert.equal(evaluate(vm, '(assoc-get (list (list "day" 14)) "day" -1)'), 14);
  assert.equal(evaluate(vm, "(length (filter (lambda (x) (> x 2)) '(1 2 3 4)))"), 2);
  assert.equal(evaluate(vm, "(member? 'x '(a x b))"), true);
});

test("persistent lexical state and callbacks", () => {
  const vm = interpreter();
  evaluate(vm, "(define counter (let ((x 0)) (lambda () (set! x (+ x 1)) x)))");
  assert.equal(evaluate(vm, "(counter)"), 1);
  assert.equal(evaluate(vm, "(counter)"), 2);
});

test("macro expansion and Scheme mutation", () => {
  const vm = interpreter();
  assert.equal(evaluate(vm, "(begin (define x 0) (define-macro (inc!) '(set! x (+ x 1))) (inc!) x)"), 1);
});

test("named let, rest arguments and nested list operations", () => {
  const vm = interpreter();
  assert.equal(evaluate(vm, "(let loop ((n 5) (s 0)) (if (= n 0) s (loop (- n 1) (+ s n))))"), 15);
  assert.equal(evaluate(vm, "((lambda (a . rest) (+ a (apply + rest))) 1 2 3 4)"), 10);
});

test("world and encounter interpreters have independent local bindings", () => {
  const world = interpreter();
  const encounter = interpreter();
  evaluate(world, "(define local-stage 17)");
  evaluate(encounter, "(define local-stage 2)");
  assert.equal(evaluate(world, "local-stage"), 17);
  assert.equal(evaluate(encounter, "local-stage"), 2);
});
