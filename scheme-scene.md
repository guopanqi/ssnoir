# Scheme 场景编写注意事项

## 字符串内嵌引号：一律用弯单引号

Schemy 只把 ASCII `"` (U+0022) 当字符串定界符。在 `notify!` / `observe-action` 等字符串里写对话时，**一律用弯单引号 `'` `'`（U+2018 / U+2019）**。

**禁止的写法：**
```scheme
(notify! "夜莺："你终于来了。"")   ; 弯双引号——括号解析出错
(notify! "夜莺:\"你终于来了。\"")  ; 反斜杠转义——Schemy 不支持
```

**正确写法——弯单引号：**
```scheme
(notify! "夜莺：'你终于来了。'")
(observe-action "老陈" "老陈：'你找到我了。接下来的事情……就看你的了。'")
```

弯单引号 `'` `'` 对 Schemy 是普通字符，永远不会截断字符串，也不依赖任何转义机制。

---

## encounter → world 状态传递：用 callback，不用 global

**错误模式：** encounter 直接写 global 来通知 world 场景任务推进。
```scheme
;; 追击.scm —— 不要这样
(set-global! 'theater-mission-stage 2)
(end-encounter)
```
问题：encounter 越权修改不属于它的状态，全局命名空间污染，所有权不清。

**正确模式：** `start-encounter` 传 callback，encounter 只汇报结果。
```scheme
;; world 场景
(start-encounter "追击黑衣人"
  (lambda (result)
    (when (equal? result 'success)
      (set! mission-stage 2))))

;; encounter 场景
(end-encounter 'success)   ; 只说结果，不知道外部是谁
```
callback 在 `LoadScene("world")` 之前执行，world interpreter 的闭包状态更新后，render tree 一次性以正确状态重建。

---

## 压力/伤害函数的调用场景

`stress-current-actor!` 依赖 action 执行上下文（`_gameState.CurrentContext`），只有在 `ExecuteAction` 期间才有效。encounter 结束 callback 可能由回合结束规则触发，此时没有 action context，调用会崩溃。

| 场景 | 正确用法 |
|---|---|
| roll 的 fail/neutral/success 回调（action lambda 内） | `(stress-current-actor! n)` |
| `start-encounter` 的结果 callback | `(add-actor-stress! 'player n)` |

---

## 节点函数扁平化：避免深嵌套 append/if

Schemy 在深嵌套的 `(append (if ...) (if ... (append ...) '()))` 结构中，某些分支会求值返回 `None` 而非 `'()`，导致 `append` 收到非 List 参数，报 `Cannot convert Schemy.None to type List`1`。

**错误模式——所有节点逻辑压在一个表达式里：**
```scheme
(define (node-theater-container)
  (container "剧院"
    (append
      (if (not gate-revealed) (list ...) '())
      (if gate-revealed
          (append (list (container "大门" ...))
                  (if (not talked) (list ...) (list ...)))
          '()))))
```

**正确模式——每段逻辑提取为独立 helper：**
```scheme
(define (node-check-gate) (instant-action "查看大门" ...))
(define (node-gate) (container "大门" ...))
(define (node-chase) (instant-action "追上黑衣人" ...))

(define (stage-one-nodes)
  (append
    (if (not gate-revealed) (list (node-check-gate)) '())
    (if gate-revealed (list (node-gate)) '())
    (if (and gate-revealed nightingale-talked) (list (node-chase)) '())))
```

规则：**每个 `define` 函数只构造一个节点或一段节点列表，不要在单个表达式里嵌套超过两层 `if/append`。**

---

## 地点状态分层原则

| 状态类型 | 存放位置 | 说明 |
|---|---|---|
| 任务阶段/跨场景进度 | 地点本地 `define` + save/load | 由地点自己管理，不泄露到 global |
| 地点内部 UI 状态 | 同上 | 门是否打开、NPC 是否说过话等 |
| 真正全局共享的数据 | `set-global!` / `get-global` | 声誉、跨地点共享资源等 |
