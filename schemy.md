# Schemy 解释器特性与内置符号说明

本文档记录了项目中使用的轻量级 C# Scheme 解释器 `Schemy` 的特性、支持的内置符号与语法，以及我们在项目底层加载的 Scheme 脚本所做的拓展和补全。

为了保持工程结构的清晰，Scheme 自定义脚本被拆分为两个独立的文件：
1. **`stdlib.scm`**：仅包含纯标准 Scheme 语法的扩充与基础辅助函数（如 `caddr`、`and`、`or`、`filter` 等）。
2. **`engine.scm`**：包含游戏与引擎层面的业务定义与 DSL 框架（如 `node` 构造器、行动与修饰符构造、`make-clock` 计数器系统、规则系统、全局库存等）。

## 1. 变参语法限制 (Varargs Syntax Constraints)

### 不支持点号变参 (Dotted Rest Args)
在标准 Scheme (R5RS) 中，可以通过 `.` 来定义带固定参数和剩余可变参数的函数，例如：
```scheme
(define (my-func first . rest)
  rest)
```
或者使用 `lambda`：
```scheme
(lambda (first . rest) rest)
```
**但在 Schemy 解释器中，点号变参语法是不支持的**，使用 `.` 会导致语法解析错误或行为异常。

### 支持纯列表变参 (Pure Varargs List)
作为替代，Schemy 支持将整个参数列表捕获为一个 List：
```scheme
(define my-func
  (lambda args
    args))
```
或者简写为：
```scheme
(define (my-func args)
  args)
```
这样所有的参数都会被放入一个 List 传给该函数。我们在 `node` 的构造逻辑和 `and`、`or` 逻辑运算符中均使用了这种方式。

---

## 2. 核心语法与特殊形式 (Syntax & Special Forms)

| 语法 / 特殊形式 | 可用性 | 说明 |
| :--- | :---: | :--- |
| `(let ((x 1)) x)` | **YES** | 支持常规的 `let` 局部变量绑定 |
| `(let* ((x 1) (y (+ x 1))) y)` | **NO** | **不支持** `let*` 顺序绑定。如果需要，应使用嵌套的 `let` |
| `(begin expr1 expr2)` | **YES** | 支持多表达式顺序执行，并返回最后一个表达式的值 |
| `(if test then else)` | **YES** | 支持常规的条件判断 |
| `(cond (test expr) ... (else expr))` | **YES** | 支持 `cond` 分支结构，是编写复杂场景/行为逻辑时的推荐做法 |
| `(case val ...)` | **NO** | **不支持** `case` 模式匹配结构 |
| `(lambda args body)` | **YES** | 支持定义匿名函数和闭包 |
| `(define var val)` | **YES** | 支持定义局部与全局变量 |

---

## 3. 内置符号与函数支持矩阵 (Symbol Matrix)

下表列出了常见 Scheme 内置符号在 Raw Schemy（未经拓展的解释器）与项目解释器（加载了 `stdlib.scm` 及 C# 拓展）中的支持情况：

| 符号 / 函数 | Raw Schemy | 项目解释器 (带 stdlib) | 类别 & 备注 |
| :--- | :---: | :---: | :--- |
| `+`, `-`, `*`, `/` | **YES** | **YES** | 基础数学运算 |
| `=`, `<`, `>`, `<=`, `>=` | **YES** | **YES** | 基础数值比较 |
| `abs`, `modulo`, `remainder`, `quotient` | **NO** | **NO** | 其它数学运算 |
| `even?`, `odd?`, `zero?` | **NO** | **NO** | 数值类型断言 |
| `eq?`, `equal?` | **YES** | **YES** | 相等性比较 |
| `eqv?` | **NO** | **NO** | 比较 |
| `null?`, `list?`, `string?`, `symbol?`, `boolean?` | **YES** | **YES** | 类型断言 |
| `pair?`, `number?`, `procedure?` | **NO** | **NO** | 类型断言 |
| `cons`, `car`, `cdr` | **YES** | **YES** | 基础列表操作 |
| `cadr` | **YES** | **YES** | 快捷操作 (`car` of `cdr`) |
| `caddr`, `cadddr` | **NO** | **YES** | 由 `stdlib.scm` 实现并补充 |
| `list`, `length`, `append`, `reverse` | **YES** | **YES** | 列表工具函数 |
| `member`, `assoc` | **NO** | **NO** | 列表搜索 |
| `map`, `apply` | **YES** | **YES** | 高阶函数 |
| `filter` | **NO** | **YES** | 由 `stdlib.scm` 实现并补充 |
| `not` | **YES** | **YES** | 逻辑非 |
| `and`, `or` | **NO** | **YES** | 由 `stdlib.scm` 实现并补充（注意：通过普通过程模拟，不具备短路求值特性） |
| `string-append`, `number->string` | **YES** | **YES** | 由 C# 宿主宿环境注册的辅助函数 |
| `display`, `newline`, `error` | **NO** | **NO** | 标准 IO 和抛错函数 |

---

## 4. stdlib.scm 中补充的自定义实现

为了解决 Schemy 本身不支持 `and`、`or` 和高阶 `filter` 等常用操作符的问题，我们在 `stdlib.scm` 里提供了以下普通过程级别的模拟实现：

### and 与 or 运算
```scheme
(define and
  (lambda args
    (if (null? args)
        #t
        (if (car args)
            (apply and (cdr args))
            #f))))

(define or
  (lambda args
    (if (null? args)
        #f
        (if (car args)
            #t
            (apply or (cdr args))))))
```
*注：由于是作为普通过程（Procedure）传入，它们会被全部求值，因此**不具备短路求值特性**。*

### filter 运算
```scheme
(define (filter pred lst)
  (if (null? lst)
      '()
      (if (pred (car lst))
          (cons (car lst) (filter pred (cdr lst)))
          (filter pred (cdr lst)))))
```

### CADR 系列辅助
```scheme
(define (caddr xs)
  (car (cdr (cdr xs))))

(define (cadddr xs)
  (car (cdr (cdr (cdr xs)))))
```

---

## 5. engine.scm 中的游戏与 DSL 框架定义

游戏层面的所有 DSL 结构、动作构建与运行时状态管理代码存放在 `engine.scm` 中。主要定义了：
* **结构化节点 (`node`)**：支持子节点、时钟、前置要求及执行回调的树状节点。
* **时钟系统 (`make-clock`)**：用于控制行动冷却、关卡警报或敌人攻击计时的倒计时对象。
* **动作构造器 (`instant`, `roll`, `observe`)**：规范各类可供玩家交互的底层行动的动作数据结构。
* **规则与轮次生命周期 (`define-rule`, `define-turn-rule`, `on-action`, `on-turn-end`)**：控制事件触发机制的规则系统。
* **游戏内库存机制 (`get-item`, `consume-item!`)**：在 Scheme 环境下读取与增删全局 `money` 和各类道具的接口。

