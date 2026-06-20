# SSNoir Scheme 内容编写指南

写 `.scm` 游戏内容(场景 / 动作 / 对白 / 规则)看这里。

> **底层解释器(Schemy)支持哪些语法和内置函数,以唯一来源 [schemy-master/](schemy-master/) 为准**:
> [README.md](schemy-master/README.md)(原版能力)+ [CHANGES.md](schemy-master/CHANGES.md)
> (我们 fork 的修复 / 新增)。
> 例如 dotted rest 变参 `(define (f a . rest) …)`、`let*`、`and`/`or` 短路、
> `abs` / `eqv?` / `pair?` / `display` / `error` 等**现在都已可用**;`case` 仍不支持。
> 本文件**不再重复**这些解释器事实,只讲 SSNoir 自己的脚本层。

---

## 1. 脚本分层

| 文件 | 内容 |
|---|---|
| `Content/scripts/stdlib.scm` | 纯标准 Scheme 的补充助手(`filter`、`for-each`、`assoc` 等) |
| `Content/scripts/engine.scm` | SSNoir 的 DSL 与游戏框架(节点、时钟、规则、状态桥) |
| `Content/scenes/world/*.scm` | 世界地点 |
| `Content/scenes/encounters/*.scm` | encounter(交锋)场景 |

整体架构见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。

---

## 2. DSL 快速参考（engine.scm）

完整定义以 [engine.scm](Content/scripts/engine.scm) 为准,这里只列常用构造。

**节点**
- `(container name children)` — 含子节点的容器
- `(node name :subtitle … :children … :clocks … :requires … :resolve …)` — 通用节点
- `(action name requires resolve)` / `(instant-action name effect)` /
  `(observe-action name text)` / `(roll-action name requires skill fail neutral success)`

**动作结算(resolve)**
- `(instant effect)`
- `(roll skill fail neutral success)`,带难度修饰:`(roll skill mod-fn fail neutral success)`
- `(observe text)`、`(clock clock-data)`
- `(outcome title subtitle effect ['light | 'heavy])` — 给效果附加结果表现
- `(modifier value reason)` — 难度修饰项

**时钟** `(make-clock label max style)` → 消息 `'tick!` `'reset!` `'full?` `'current` `'set!` `'render-data`

**要求(requires)** `(req-die)`、`(req-item name qty)`

**规则系统** `(define-rule name cond act)` / `(define-turn-rule …)`,由 `(on-action)` / `(on-turn-end)` 统一遍历。

**状态桥**
- 纯全局键:`(get-global k)` / `(set-global! k v)`(章节、声誉、剧情 flag)
- 队伍 / 库存 / 成长:`party-health`、`damage-party!`、`item-count`、`add-item!`、
  `growth-level`、`set-growth-level!`、`actor-stress` 等(见 engine.scm)
- 表现:`(notify! text)`、`(spotlight! title subtitle)`、`(play-narration! id)`

**encounter 切换** `(start-encounter name callback)` / `(end-encounter result)`

---

## 3. 编写约定（必须遵守）

### 3.1 字符串内嵌引号:一律用弯单引号

Schemy 只把 ASCII `"` (U+0022) 当字符串定界符。对白里要引号时,**一律用弯单引号 `'` `'`(U+2018 / U+2019)**。

```scheme
;; 错误:弯双引号 / 反斜杠转义都会让解析出错
(notify! "夜莺：“你终于来了。”")
(notify! "夜莺:\"你终于来了。\"")
;; 正确
(notify! "夜莺：'你终于来了。'")
(observe-action "老陈" "老陈：'你找到我了。'")
```

### 3.2 encounter → world 状态传递:用 callback,不用 global

encounter **不要**直接写 global 通知 world 推进任务(越权、污染全局命名空间)。
`start-encounter` 传 callback,encounter 只汇报结果:

```scheme
;; world 场景
(start-encounter "追击黑衣人"
  (lambda (result)
    (when (equal? result 'success) (set! mission-stage 2))))
;; encounter 场景
(end-encounter 'success)   ; 只说结果,不知道外部是谁
```

callback 在 `LoadScene("world")` 之前执行,world 闭包状态更新后,render tree 一次性以正确状态重建。

### 3.3 压力 / 伤害函数的调用场景

`stress-current-actor!` 依赖 action 执行上下文,只有 `ExecuteAction` 期间有效;
encounter 结束 callback 可能由回合结束规则触发,此时没有 action context。

| 场景 | 正确用法 |
|---|---|
| roll 的 fail/neutral/success 回调(action lambda 内) | `(stress-current-actor! n)` |
| `start-encounter` 的结果 callback | `(add-actor-stress! 'player n)` |

### 3.4 节点函数扁平化:避免深嵌套 append/if

Schemy 在深嵌套 `(append (if …) (if … (append …) '()))` 中,某些分支会求值成 `None`
而非 `'()`,导致 `append` 报 `Cannot convert Schemy.None to type List`1`。

**规则:每个 `define` 函数只构造一个节点或一段节点列表,单个表达式里 `if/append` 不超过两层。**

```scheme
;; 正确:每段逻辑抽成独立 helper
(define (node-check-gate) (instant-action "查看大门" …))
(define (node-gate)       (container "大门" …))
(define (stage-one-nodes)
  (append
    (if (not gate-revealed) (list (node-check-gate)) '())
    (if gate-revealed (list (node-gate)) '())))
```

### 3.5 地点状态分层

| 状态类型 | 存放位置 |
|---|---|
| 任务阶段 / 跨场景进度 | 地点本地 `define` + save/load(不泄露到 global) |
| 地点内部 UI 状态(门是否开、NPC 是否说过话) | 同上 |
| 真正全局共享(声誉、跨地点资源) | `set-global!` / `get-global` |
