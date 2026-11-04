import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { LipsSession } from "../src/scheme/lips-session.ts";

async function setup() {
  const vm = new LipsSession("association-lookup");
  await vm.bootstrapStandardLibrary(readFileSync("node_modules/lips/dist/std.xcb"));
  await vm.evaluate(readFileSync("../UnityClient/Assets/Resources/Content/scripts/stdlib.scm", "utf8"));
  await vm.evaluate("(define reference-assoc-get assoc-get)");
  vm.accelerateContentLookups();
  return vm;
}

test("accelerated lookup matches the authored helper and preserves Scheme values", async () => {
  const vm = await setup();
  const cases = [
    ['\'()', '"missing"', "17"],
    ['\'(("进度" 3) ("进度" 9))', '"进度"', "0"],
    ['\'(("" #f) ("" 9))', '""', "17"],
    ['\'(("empty" ()) ("false" #f))', '"empty"', "17"],
    ['\'((key 1) ("key" 2))', '"key"', "0"],
    ['\'(("key" 2) (key 1))', "'key", "0"],
    ['\'((1 2) (2 3))', "2", "0"],
    ['\'(((a b) (nested value)))', "'(a b)", "#f"],
    ['\'((#f 9))', "#f", "0"],
    ['\'(("value" 1 extra))', '"value"', "0"]
  ];
  for (const args of cases) {
    const input = args.join(" ");
    assert.equal(await vm.evaluate(`(equal? (assoc-get ${input}) (reference-assoc-get ${input}))`),
      true, input);
  }
  // Lookup returns the actual value, retaining identity for mutable Scheme data.
  assert.equal(await vm.evaluate(`(let* ((value (list 1 2)) (table (list (list "x" value))))
    (eq? value (assoc-get table "x" #f)))`), true);
});

test("lookup sees mutated keys, values and tails without stale indexes", async () => {
  const vm = await setup();
  await vm.evaluate(`(define mutable-key (string-copy "a"))
    (define table (list (list mutable-key 1)))`);
  assert.equal(await vm.evaluateNumber('(assoc-get table "a" 0)'), 1);
  await vm.evaluate('(string-set! mutable-key 0 #\\b)');
  assert.equal(await vm.evaluateNumber('(assoc-get table "a" 0)'), 0);
  assert.equal(await vm.evaluateNumber('(assoc-get table "b" 0)'), 1);
  await vm.evaluate('(set-car! (cdr (car table)) 8)');
  assert.equal(await vm.evaluateNumber('(assoc-get table "b" 0)'), 8);
  await vm.evaluate('(set-cdr! table (list (list "c" 9)))');
  assert.equal(await vm.evaluateNumber('(assoc-get table "c" 0)'), 9);
});

test("malformed traversed associations fail while matching rows retain short circuit semantics", async () => {
  const vm = await setup();
  for (const input of ["'((\"x\")) \"x\" 0", "'(bad) \"x\" 0", "'((\"a\" 1) . bad) \"x\" 0"]) {
    await assert.rejects(vm.evaluate(`(reference-assoc-get ${input})`));
    await assert.rejects(vm.evaluate(`(assoc-get ${input})`));
  }
  assert.equal(await vm.evaluateNumber('(assoc-get \'(("x" 7) . bad) "x" 0)'), 7);
  await vm.evaluate('(define cycle (list (list "a" 1))) (set-cdr! cycle cycle)');
  await assert.rejects(vm.evaluate('(assoc-get cycle "x" 0)'), /cyclic/);
  await assert.rejects(vm.evaluate('(assoc-get \'() "x")'), /3 arguments/);
});
