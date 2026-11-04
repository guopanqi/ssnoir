import test from 'node:test';
import assert from 'node:assert/strict';
import {CompiledSession} from '../src/scheme/compiled-session.ts';

test('compiled VMs isolate definitions, native globals, macros and failed candidates',async()=>{
  const world=new CompiledSession('world',{'native-value':()=>11});
  const scenario=new CompiledSession('scenario',{'native-value':()=>22});
  await world.evaluate('(define phase 7) (define (next!) (set! phase (+ phase 1)) phase)');
  await scenario.evaluate('(define phase 90) (define + (lambda args 44))');
  assert.equal(await world.evaluate('(next!)'),8);
  assert.equal(await scenario.evaluate('phase'),90);
  assert.equal(await world.evaluate('(+ (native-value) 1)'),12);
  assert.equal(await scenario.evaluate('(native-value)'),22);
  await scenario.evaluate('(define-macro (seven) 7)');
  assert.equal(await scenario.evaluate('(seven)'),7);
  await assert.rejects(world.evaluate('(seven)'),/unbound/i);
  assert.equal(await world.evaluate('(next!)'),9);
  assert.equal(await world.evaluate("(if '() 1 0)"),1);
  assert.equal(await world.evaluate('(if 0 1 0)'),1);
});

test('mixed local definitions preserve initialization order, shared mutation and lexical ownership',async()=>{
  const vm=new CompiledSession('closures');
  await vm.evaluate(`(define log '())
    (define (mark x) (set! log (cons x log)) x)
    (define (make-counter start)
      (let ((n start))
        (mark 1)
        (define (add! x) (set! n (+ n x)))
        (mark 2)
        (begin (define (read) n) (mark 3))
        (lambda (message) (if message (begin (add! 1) (read)) (read)))))
    (define a (make-counter 0)) (define b (make-counter 10))`);
  assert.equal(await vm.evaluate('(a #t)'),1);
  assert.equal(await vm.evaluate('(a #t)'),2);
  assert.equal(await vm.evaluate('(b #t)'),11);
  assert.equal(await vm.evaluate('(a #f)'),2);
  assert.equal(await vm.evaluate("(equal? (reverse log) '(1 2 3 1 2 3))"),true);
  assert.equal(await vm.evaluate("(bound? 'add!)"),false,'Local functions cannot leak to TopEnv');
  assert.equal(await vm.evaluate('(let ((n 77)) (define (f n) (set! n 4) n) (f 1) n)'),77);
  assert.equal(await vm.evaluate(`(let () (define (even n) (if (= n 0) #t (odd (- n 1))))
    (define (odd n) (if (= n 0) #f (even (- n 1)))) (even 2000))`),true);
});

test('native async suspension, nested evaluation and rejection execute effects exactly once',async()=>{
  let calls=0;
  let vm;
  vm=new CompiledSession('async',{
    'load-probe':async()=>{calls++;await vm.evaluate('(define loaded 17)');return 5;},
    'reject-probe':async()=>{throw new Error('native rejected');}
  });
  assert.equal(await vm.evaluate('(define count 0) (set! count (+ count 1)) (+ (load-probe) count loaded)'),23);
  assert.equal(calls,1);
  await assert.rejects(vm.evaluate('(reject-probe) (set! count 999)'),/native rejected/);
  assert.equal(await vm.evaluate('count'),1);
  assert.equal(await vm.evaluate('(let loop ((n 100000) (sum 0)) (if (= n 0) sum (loop (- n 1) (+ sum 1))))'),100000);
});
