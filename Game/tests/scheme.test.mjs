import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import BiwaScheme from "biwascheme";

function evaluate(code) {
  let done = false;
  let result;
  const interpreter = new BiwaScheme.Interpreter(error => { throw error; });
  interpreter.evaluate(code, value => { done = true; result = value; });
  assert.equal(done, true, "Scheme probe must finish synchronously");
  return result;
}

test("BiwaScheme arithmetic and lexical scope", () => {
  assert.equal(evaluate("(let ((x 12)) ((lambda (y) (+ x y)) 30))"), 42);
});

test("BiwaScheme treats only #f as false", () => {
  assert.equal(evaluate("(if '() 42 0)"), 42);
  assert.equal(evaluate("(if 0 42 0)"), 42);
});

test("existing SSNoir stdlib.scm loads unchanged", () => {
  const code = readFileSync("../UnityClient/Assets/Resources/Content/scripts/stdlib.scm", "utf8");
  assert.equal(evaluate(code + "\n(assoc-get '((\"day\" 14)) \"day\" -1)"), 14);
  assert.equal(evaluate(code + "\n(length (filter (lambda (x) (> x 2)) '(1 2 3 4)))"), 2);
});

test("macro expansion and mutation", () => {
  assert.equal(evaluate("(begin (define x 0) (define-macro (inc!) '(set! x (+ x 1))) (inc!) x)"), 1);
});
