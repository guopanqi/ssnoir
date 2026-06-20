# Schemy Fork — 改动记录

我们对这个 [Microsoft/schemy](https://github.com/Microsoft/schemy) fork 的所有改动记录:
按时间的改动日志 + 与上游的能力差异汇总。

> 记录约定与开发流程见 [AGENTS.md](AGENTS.md);原版项目说明见 [README.md](README.md)。

---

## 改动日志（按时间倒序）

### 2026-06-21 — 空 `(let)` / `(let*)` 作用域(**回归修复**)

**文件**:`src/schemy/init.ss`(`let` 两处定义 + `let*`)

**性质**:这是**我们 fork 自己引入的回归**,不是上游的 bug。
上游 Microsoft/schemy 的 `let` 宏对空绑定本来就展开成 `((lambda () ,@bodies))`(正确,会建立作用域);
我们在 fork 重写 `init.ss`(commit `173c83f` / `78b4476`「schemy fork改造」)时把它改成了 `(begin ,@bodies)`。
有意思的是 README 里抄过来的 `let` 宏示例仍是上游的正确写法,所以**文档是对的,实现 drift 了**。

**问题**:`(begin ...)` 不建立新环境帧,于是 `(let () (define ...) ...)` 里的内部 `define`
泄漏到外层(通常是全局)作用域。

**影响**:SSNoir 每个地点写成 `(define 地点 (let () (define helper...) (lambda args ...)))`,
helper 全部泄漏到全局。两个地点只要用了同名 helper(如 `stage-1-children`),后加载的覆盖先加载的,
导致地点串台渲染(`饭店` 渲染出 `废弃仓库` 的 `搜寻线索`/`搬运货物`,触发渲染树节点重名断言)。

**修复**:把空绑定改回上游的 `((lambda () ,@bodies))`,与非空 `let` 一致地建立新帧。内部 `define` 自此局部。

```scheme
;; 上游(正确) / 修复后:  (let () (define (f) 1) ...) -> ((lambda () (define (f) 1) ...))
;; 我们 fork 的回归:       (let () (define (f) 1) ...) -> (begin (define (f) 1) ...)   ; f 泄漏全局
```

**验证**:SSNoir `--validate` / `--test-saveload` / `--simulate` 全部通过。

---

## 与上游的能力差异（分类汇总）

> 这是 fork 相对上游的「能力差异总表」(原先放在 README 末尾,现移到此处)。
> 增量改动请写到上面的「改动日志」,这里只做分类汇总。

### Target framework

`netstandard2.0`(原 `net4.5.2`)。兼容 Unity 和现代 .NET。

### Breaking API changes

| What | Before | After |
|------|--------|-------|
| `ICallable` | `internal` interface | `public` — hosts can hold/check/invoke procedures |
| `Procedure.Parameters` type | `Union<Symbol, List<Symbol>>` | `LambdaParams` |
| `Environment.FromVariablesAndValues` | takes `Union<…>` | takes `LambdaParams` |

### New constructor parameter

```csharp
new Interpreter(
    environmentInitializers: …,   // unchanged
    fsAccessor: …,                // unchanged
    output: myTextWriter          // NEW — defaults to Console.Out
)
```

### New `Interpreter` members

```csharp
TextWriter interpreter.Output   // get/set
EvaluationResult interpreter.Evaluate(string expression)  // convenience overload
```

### Language: dotted rest parameters — FIXED

All three R5RS lambda parameter forms now work:

```scheme
(lambda args body)           ; all args captured as list
(lambda (a b) body)          ; fixed arity  (was already working)
(lambda (a b . rest) body)   ; fixed + variadic  ← was broken
```

Works in both `lambda` and `define`:
```scheme
(define (f x . rest) (cons x rest))
(f 1 2 3)  ; => (1 2 3)
```

### Language: new macros in `init.ss`

`letrec`, `let*`, named `let`, `and`, `or`, `when`, `unless`, plus `cXr` helpers
(`cadr`, `caddr`, `cadddr`, `cddr`, `caar`, `cdar`, `caadr`, `cdadr`, `cadar`,
`cddar`, `cdddr`).

```scheme
; named let
(let loop ((i 0) (acc 0))
  (if (= i 5) acc (loop (+ i 1) (+ acc i 1))))

; letrec — mutual recursion
(letrec ((even? (lambda (n) (if (= n 0) #t (odd?  (- n 1)))))
         (odd?  (lambda (n) (if (= n 0) #f (even? (- n 1))))))
  (even? 10))
```

### Language: empty `(let)` / `(let*)` scope — FIXED

见上面 2026-06-21 的改动日志。空绑定 `(let () body)` 现在展开成 `((lambda () body))`
(回到上游行为),内部 `define` 留在局部作用域,不再泄漏全局。

### Language: truthiness — FIXED

Only `#f` is false. `0`, `""`, `'()` are all truthy (R5RS-compliant).

```scheme
(not #f)  ; => #t
(not 0)   ; => #f  ← was a type error before
```

### Builtins: fixed

| Function | Fix |
|----------|-----|
| `not` | now accepts any value (was `bool`-only) |
| `append` | variadic — `(append l1 l2 l3 …)` |
| `apply` | full R5RS form — `(apply f a1 a2 … list)` |
| `map` | multiple lists — `(map f l1 l2)` |

### Builtins: new

`number?`, `procedure?`, `pair?`, `eqv?`,
`modulo`, `remainder`, `quotient`, `abs`, `min`, `max`,
`error`, `display`, `write`, `newline`

(`num?` kept as a backward-compatible alias for `number?`.)

### Initialisation: Unity-safe — FIXED

`Assembly.Location` calls are now wrapped in `try/catch`. Under Unity IL2CPP
`Assembly.Location` can throw or return an empty string; this previously crashed
the interpreter before any script ran. The embedded `init.ss` still loads
normally (it is a manifest resource, not a filesystem file).

### Optional standard library

`stdlib.scm` (at the repo root) provides `filter`, `for-each`, `assoc`,
`assq`, `member`, `memq`, `list-tail`, `fold-left`, `fold-right`, `every`,
`any`, `iota`, `gcd`, `lcm`, and more — all implemented in Scheme.

**It is not auto-loaded.** Load it explicitly when you need it:
```csharp
interpreter.Evaluate(File.OpenText("stdlib.scm"));
```
or from Scheme: `(load "stdlib.scm")`
